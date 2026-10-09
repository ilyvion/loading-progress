using System.Reflection.Emit;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress;

internal sealed class StaticConstructorOnStartupUtilityReplacement
{
    internal static void Interject() =>
        Utilities.LongEventHandlerPrependQueue(() =>
        {
            LongEventHandler.QueueLongEvent(CallAllAndRest(), "LoadingProgress.CallAll");
            // When we're done, resume the original ExecuteToExecuteWhenFinished method to
            // process whatever toExecuteWhenFinished entries are left.
            LongEventHandler.QueueLongEvent(
                LongEventHandler_ExecuteToExecuteWhenFinished_Patches.ExecuteToExecuteWhenFinished(),
                "LoadingProgress.ExecuteToExecuteWhenFinished"
            );
        });

    internal static bool _callAllCalled;

    // Vanilla's PlayDataLoader.DoPlayLoad() bundles StaticConstructorOnStartupUtility.CallAll(),
    // FloatMenuMakerMap.Init(), GlobalTextureAtlasManager.BakeStaticAtlases(), cache clearing,
    // a forced GC.Collect() and Resources.UnloadUnusedAssets() into a single
    // ExecuteWhenFinished delegate. We can't patch DoPlayLoad itself (it's already run by the
    // time our mod is alive), so the whole closure arrives as one opaque, unyielded unit. We
    // intercept it (see StaticConstructorOnStartupCallAllFinder) and run every one of those
    // steps here ourselves instead, with a yield between each, so the loading screen gets a
    // chance to repaint between them instead of freezing for the combined duration of all of
    // them. The original closure entry is removed from toExecuteWhenFinished (see
    // LongEventHandler_ExecuteToExecuteWhenFinished_Patches) so it never runs a second time.
    private static IEnumerable CallAllAndRest()
    {
        _callAllCalled = true;
        DeepProfiler.Start("StaticConstructorOnStartupUtilityReplacement.CallAll()");
        var list = GenTypes.AllTypesWithAttribute<StaticConstructorOnStartup>();
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];

            LoadingProgressWindow.SetCurrentLoadingActivityRaw(item.ToString());
            LoadingProgressWindow.StageProgress = (i + 1, list.Count);
            yield return null;

            var info = LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(
                Utilities.FindModByAssembly(item.Assembly)
            );
            info?.Start("LoadingProgress.StartupImpact.StaticConstructorOnStartupUtilityCallAll");

            try
            {
                var now = DateTime.Now;
                //LoadingProgressMod.Debug($"About to run static constructor for {item} @ {now:HH:mm:ss.fff}");
                RuntimeHelpers.RunClassConstructor(item.TypeHandle);
                //LoadingProgressMod.Debug($"Finished running static constructor for {item} @ {DateTime.Now:HH:mm:ss.fff}; took {DateTime.Now - now:mm\\:ss\\.fff}");
            }
            catch (Exception ex)
            {
                Log.Error("Error in static constructor of " + item?.ToString() + ": " + ex);
            }

            _ = info?.Stop(
                "LoadingProgress.StartupImpact.StaticConstructorOnStartupUtilityCallAll"
            );

            // RimWorld's UpdateCurrentEnumeratorEvent calls MoveNext() in a tight loop with
            // no rendering in between, only checking its 100ms time budget after each
            // MoveNext() returns. Without this yield, a slow constructor above blows through
            // that budget from inside a single MoveNext() call, and the very next thing that
            // happens before control returns is this loop setting the label for the *next*
            // item. That's what ends up on screen, so the stall gets misattributed to the
            // following mod. Yielding here ensures the label still showing when we finally
            // repaint is the one for the constructor that actually ran.
            yield return null;
        }
        DeepProfiler.End();
        StaticConstructorOnStartupUtility.coreStaticAssetsLoaded = true;

        // Run the real StaticConstructorOnStartupUtility.CallAll() too, purely so any
        // third-party Harmony prefixes/postfixes on it still fire like they normally would.
        // The constructors themselves are cheap the second time around (the CLR no-ops repeat
        // RunClassConstructor calls for a type that's already been initialized).
        LoadingProgressWindow.SetCurrentLoadingActivityRaw(string.Empty);
        yield return null;
        DeepProfiler.Start("Static constructor calls");
        try
        {
            // Timing the hooks means patching other mods' methods, which only a player who
            // asked for startup impact tracking has agreed to.
            if (LoadingProgressMod.instance.StartupImpact.WasTrackingEnabledAtStartup)
            {
                CallAllWithHooksTimed();
            }
            else
            {
                StaticConstructorOnStartupUtility.CallAll();
            }

            if (Prefs.DevMode)
            {
                StartupImpactProfilerUtil.StartBaseGameProfiler(ReportMissingAttributesCategory);
                try
                {
                    StaticConstructorOnStartupUtility.ReportProbablyMissingAttributes();
                }
                finally
                {
                    StartupImpactProfilerUtil.StopBaseGameProfiler(ReportMissingAttributesCategory);
                }
            }
        }
        finally
        {
            DeepProfiler.End();
        }
        yield return null;

        FloatMenuMakerMap.Init();
        yield return null;

        DeepProfiler.Start("Atlas baking.");
        try
        {
            GlobalTextureAtlasManager.BakeStaticAtlases();
        }
        finally
        {
            DeepProfiler.End();
        }
        yield return null;

        // The collect and the unload are the engine's; they get a heading of their own under
        // the base game. The unload is asynchronous and blocks a later frame. The yield that
        // follows ends this frame once the frame's time budget for long events is spent, as it
        // is after a long collect on a long mod list, or always when forced repaints are on,
        // since the stage change above asks for one. The category then stays open into the
        // frame the unload blocks and takes it in. After a short collect with forced repaints
        // off, the frame goes on, the category closes first, and the unload's stall falls in
        // the remaining time.
        foreach (
            var step in TimedIntoTheNextFrame(
                GarbageCollectionCategory,
                "Garbage Collection",
                CollectGarbage
            )
        )
        {
            yield return step;
        }
    }

    private static void CollectGarbage()
    {
        RimWorld.IO.AbstractFilesystem.ClearAllCache();
        GC.Collect(int.MaxValue, GCCollectionMode.Forced);
        _ = Resources.UnloadUnusedAssets();
    }

    /// <summary>
    /// Runs <paramref name="work"/> under the base game's <paramref name="category"/>, inside the
    /// engine profiler's <paramref name="label"/>, and yields once before the category stops,
    /// so it also takes in a frame the work blocks. The category stops however the iterator
    /// ends: after the yield, when the work throws, or when the iterator is disposed before it
    /// finishes.
    /// </summary>
    internal static IEnumerable TimedIntoTheNextFrame(string category, string label, Action work)
    {
        StartupImpactProfilerUtil.StartBaseGameProfiler(category);
        try
        {
            DeepProfiler.Start(label);
            try
            {
                work();
            }
            finally
            {
                DeepProfiler.End();
            }
            yield return null;
        }
        finally
        {
            StartupImpactProfilerUtil.StopBaseGameProfiler(category);
        }
    }

    internal const string GarbageCollectionCategory =
        "LoadingProgress.StartupImpact.GarbageCollection";

    internal const string ReportMissingAttributesCategory =
        "LoadingProgress.StartupImpact.ReportProbablyMissingAttributes";

    // The engine's own CallAll pass: the call itself, with every hook on it timed elsewhere.
    internal const string CallAllPassCategory =
        "LoadingProgress.StartupImpact.StaticConstructorOnStartupUtilityCallAllPass";

    // The same, when some hooks could not be timed on their own: their owners follow the '|'.
    internal const string CallAllPassWithUntimedHooksKey =
        "LoadingProgress.StartupImpact.StaticConstructorOnStartupUtilityCallAllPassWithUntimedHooks";

    private static readonly MethodInfo CallAllMethod = AccessTools.Method(
        typeof(StaticConstructorOnStartupUtility),
        nameof(StaticConstructorOnStartupUtility.CallAll)
    );

    /// <summary>
    /// Runs the engine's CallAll with every other mod's hook on it timed under that mod.
    /// </summary>
    /// <remarks>
    /// Every constructor has already run, so what this pass costs is the other mods' hooks on
    /// it. Each hook is timed under its own mod for the duration of the call; whatever cannot
    /// be is named on the call's own category instead.
    /// </remarks>
    private static void CallAllWithHooksTimed()
    {
        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        CallAllHookTiming hookTiming = null!;
        startupImpact.RunAsStage(
            CallAllHookTiming.Stage,
            () => hookTiming = CallAllHookTiming.Install(CallAllMethod)
        );
        var passCategory = CallAllPassCategoryFor(hookTiming.UntimedOwners);
        var timed = false;
        try
        {
            // The call runs whatever its timing does, or no other mod's hook on it would fire.
            timed = PassTimingStep(() =>
                StartupImpactProfilerUtil.StartBaseGameProfiler(passCategory)
            );
            StaticConstructorOnStartupUtility.CallAll();
        }
        finally
        {
            // The patches come off whatever happens, or they would stay on other mods' hook
            // methods for the rest of the session.
            try
            {
                if (timed)
                {
                    _ = PassTimingStep(() =>
                        StartupImpactProfilerUtil.StopBaseGameProfiler(passCategory)
                    );
                }
            }
            finally
            {
                startupImpact.RunAsStage(CallAllHookTiming.Stage, hookTiming.Remove);
            }
        }
    }

    // Starts or stops the pass's category, logging a failure instead of letting it through.
    // Returns whether the step ran without throwing.
    private static bool PassTimingStep(Action step)
    {
        try
        {
            step();
            return true;
        }
        catch (Exception e)
        {
            LoadingProgressMod.Warning($"Could not time the static constructor pass: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// The category for the engine's own CallAll pass: the call itself, naming whichever hooks
    /// could not be timed under their own mods, so their time still has an address.
    /// </summary>
    internal static string CallAllPassCategoryFor(IReadOnlyList<string> untimedOwners) =>
        untimedOwners.Count == 0
            ? CallAllPassCategory
            : $"{CallAllPassWithUntimedHooksKey}|{string.Join(", ", untimedOwners)}";
}

internal static partial class LongEventHandler_ExecuteToExecuteWhenFinished_Patches
{
    private static class StaticConstructorOnStartupCallAllFinder
    {
        private static readonly MethodInfo _method_StaticConstructorOnStartupUtility_CallAll =
            AccessTools.Method(
                typeof(StaticConstructorOnStartupUtility),
                nameof(StaticConstructorOnStartupUtility.CallAll)
            );

        private static readonly CodeMatch[] toMatch =
        [
            new(OpCodes.Call, _method_StaticConstructorOnStartupUtility_CallAll),
        ];

        public static IEnumerable<MethodInfo> FindMethodCalling()
        {
            // Find all possible candidates, both from the wrapping type and all nested types.
            var candidates = Utilities.FindInTypeAndInnerTypeMethods(typeof(PlayDataLoader));

            //check all candidates for the target instructions, return those that match.
            foreach (var method in candidates)
            {
                var instructions = PatchProcessor.GetCurrentInstructions(method);
                var matched = instructions.Matches(toMatch);
                if (matched)
                {
                    yield return method;
                }
            }
            yield break;
        }
    }
}
