using System.Reflection.Emit;
using RimWorld.Planet;
using Verse.Profile;

namespace ilyvion.LoadingProgress;

// Extends the same "run the deferred block as a yielding enumerator instead of one blocking
// call" technique LongEventHandler_ExecuteToExecuteWhenFinished_Patches already uses for startup
// to in-game sessions, but without that technique's ordering change: the two closures handled
// here are opaque delegates created before this mod could intervene (like the ones
// StaticConstructorOnStartupUtilityReplacement and ReloadContentIntReplacement already handle
// for startup), so their bodies are reimplemented here, calling through to the same real, public
// per-unit work (a map section's RegenerateAllLayers(), a world layer's Regenerate()) with a
// yield between units instead of running it all in one call. See
// LongEventHandler_ExecuteToExecuteWhenFinished_Patches.Prefix for how the callback that would
// otherwise fire before this finishes is deferred to the end of ExecuteToExecuteWhenFinished
// below.
internal static class InGameDeferredActionReplacement
{
    private static readonly MethodInfo? _mapRegenClosure = FindClosure(
        typeof(Map),
        AccessTools.Method(typeof(MapDrawer), nameof(MapDrawer.RegenerateEverythingNow)),
        "Map.FinalizeInit's mapDrawer.RegenerateEverythingNow() closure"
    );

    private static readonly MethodInfo? _worldRegenClosure = FindClosure(
        typeof(Page_CreateWorldParams),
        AccessTools.Method(typeof(WorldRenderer), nameof(WorldRenderer.RegenerateAllLayersNow)),
        "Page_CreateWorldParams.CanDoNext's renderer.RegenerateAllLayersNow() closure"
    );

    private static MethodInfo? FindClosure(
        Type containingType,
        MethodInfo calledMethod,
        string description
    )
    {
        CodeMatch[] toMatch = [new(OpCodes.Callvirt, calledMethod)];
        var matches = Utilities.FindMethodsDoing(containingType, toMatch).ToList();
        if (matches.Count == 1)
        {
            return matches[0];
        }

        LoadingProgressMod.Error(
            $"Could not find {description} (found {matches.Count} candidates instead of "
                + "exactly 1); in-game deferred-block repaint will not chunk this action."
        );
        return null;
    }

    internal static bool ContainsKnownSlowAction(List<Action> actions)
    {
        foreach (var action in actions)
        {
            if (
                (_mapRegenClosure != null && action.Method == _mapRegenClosure)
                || (_worldRegenClosure != null && action.Method == _worldRegenClosure)
            )
            {
                return true;
            }
        }
        return false;
    }

    // Mirrors LongEventHandler.ExecuteToExecuteWhenFinished()'s own loop (same DeepProfiler
    // wrapping, same per-action try/catch); only entries matching one of the two closures found
    // above get chunked instead of called directly. callback is invoked only once every entry
    // has actually finished, however many frames that took.
    internal static IEnumerable ExecuteToExecuteWhenFinished(Action? callback)
    {
        LongEventHandler.executingToExecuteWhenFinished = true;
        if (LongEventHandler.toExecuteWhenFinished.Count > 0)
        {
            DeepProfiler.Start("ExecuteToExecuteWhenFinished()");
        }
        for (var i = 0; i < LongEventHandler.toExecuteWhenFinished.Count; i++)
        {
            var action = LongEventHandler.toExecuteWhenFinished[i];
            if (action.Method == _mapRegenClosure && action.Target is Map map)
            {
                foreach (var _ in RegenerateMapEverythingNow(map))
                {
                    yield return null;
                }
            }
            else if (
                action.Method == _worldRegenClosure
                && action.Target is Page_CreateWorldParams page
            )
            {
                foreach (var _ in RegeneratePageWorldRenderer(page))
                {
                    yield return null;
                }
            }
            else
            {
                DeepProfiler.Start(
                    action.Method.DeclaringType.ToString() + " -> " + action.Method.ToString()
                );
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.Error("Could not execute post-long-event action. Exception: " + ex);
                }
                finally
                {
                    DeepProfiler.End();
                }
            }
            yield return null;
        }
        if (LongEventHandler.toExecuteWhenFinished.Count > 0)
        {
            DeepProfiler.End();
        }
        LongEventHandler.toExecuteWhenFinished.Clear();
        LongEventHandler.executingToExecuteWhenFinished = false;

        callback?.Invoke();
    }

    // Mirrors MapDrawer.RegenerateEverythingNow()'s own body: EnsureGlobalLayersInitialized,
    // the (usually tiny) global-layer loop, then the per-section loop - yielding once per
    // section, since Section.RegenerateAllLayers() itself has no yield points of its own to
    // interleave with.
    private static IEnumerable RegenerateMapEverythingNow(Map map)
    {
        var mapDrawer = map.mapDrawer;

        mapDrawer.EnsureGlobalLayersInitialized();
        foreach (var globalLayer in mapDrawer.global)
        {
            if (!globalLayer.Visible)
            {
                continue;
            }
            try
            {
                globalLayer.Regenerate();
                globalLayer.RefreshSubMeshBounds();
            }
            catch (Exception ex)
            {
                Log.Error("Could not regenerate map draw layer: " + ex);
            }
        }

        var sectionCount = mapDrawer.SectionCount;
        InGameLoadingSession.OnDeferredRegenerationStarted(sectionCount.x * sectionCount.z);

        // mapDrawer.sections is a vanilla field typed Section[,]; matching its exact type here
        // (rather than a jagged array) is required, not a style choice.
#pragma warning disable CA1814
        mapDrawer.sections ??= new Section[sectionCount.x, sectionCount.z];
#pragma warning restore CA1814
        for (var x = 0; x < sectionCount.x; x++)
        {
            for (var z = 0; z < sectionCount.z; z++)
            {
                mapDrawer.sections[x, z] ??= new Section(new IntVec3(x, 0, z), map);
                try
                {
                    mapDrawer.sections[x, z].RegenerateAllLayers();
                }
                catch (Exception ex)
                {
                    Log.Error($"Could not regenerate map section ({x}, {z}): {ex}");
                }
                InGameLoadingSession.OnMapDrawerSectionRegenerated();
                yield return null;
            }
        }
    }

    // Mirrors the ExecuteWhenFinished closure Page_CreateWorldParams.CanDoNext registers after
    // world generation finishes: open the next page, unload now-unused assets, regenerate the
    // world renderer, then close this page. Only the renderer regen is chunked, per visible
    // layer, by driving each layer's own Regenerate() enumerable directly instead of going
    // through RegenerateNow()'s blocking ExecuteEnumerable(); WorldDrawLayerBase_Regenerate_
    // Patches' existing prefix (already shipped for PlanetRegeneration) picks up the per-layer
    // progress from that call, since it fires the same way regardless of caller.
    private static IEnumerable RegeneratePageWorldRenderer(Page_CreateWorldParams page)
    {
        if (page.next != null)
        {
            Find.WindowStack.Add(page.next);
        }
        MemoryUtility.UnloadUnusedUnityAssets();
        yield return null;

        var visibleLayers = Find.World.renderer.AllDrawLayers.Where(l => l.Visible).ToList();
        InGameLoadingSession.OnDeferredRegenerationStarted(visibleLayers.Count);

        foreach (var layer in visibleLayers)
        {
            foreach (var _ in layer.Regenerate())
            {
                yield return null;
            }
        }

        page.Close(doCloseSound: true);
    }
}
