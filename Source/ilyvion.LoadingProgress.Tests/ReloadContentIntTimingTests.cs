using System.Collections.Concurrent;
using System.Diagnostics;
using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;
using ilyvion.LoadingProgress.StartupImpact.Patches;

namespace ilyvion.LoadingProgress.Tests;

// The off-thread test turns hook timing on for itself, after the startup has turned it off.
[TestFixture(TestType.MainMenu)]
[WarningsAllowed(TestStartup.OtherModsWarnings)]
internal sealed class ReloadContentIntTimingTests
{
    private const string TestHarmonyId = "ilyvion.LoadingProgress.Tests.ReloadContentIntTiming";

    // Stands in for the texture loading a destructive prefix replaces.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void LoadTexture() => _ = Stopwatch.GetTimestamp();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ReplacingPrefix()
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5) { }
        return false;
    }

    // Each reload step runs through its timed helper in the method itself, so a caller other
    // than this mod's own loader, such as another mod's early content loader, is timed too.
    // The patch is applied only until the startup ends, so the transpiler is run directly.
    [Test]
    public static void ReloadContentIntRunsEachStepThroughItsTimedHelper()
    {
        var original = PatchProcessor.GetOriginalInstructions(
            AccessTools.Method(typeof(ModContentPack), nameof(ModContentPack.ReloadContentInt))
        );
        var instructions = ModContentPack_ReloadContentInt_Patches.Transpiler(original).ToList();

        Expect.IsFalse(
            instructions.Any(i =>
                i.operand is MethodInfo { Name: nameof(ModAssetBundlesHandler.ReloadAll) }
            ),
            "A reload step is still called directly."
        );
        foreach (
            var helper in new[]
            {
                nameof(ModContentPack_ReloadContentInt_Patches.ReloadAudioClips),
                nameof(ModContentPack_ReloadContentInt_Patches.ReloadTextures),
                nameof(ModContentPack_ReloadContentInt_Patches.ReloadStrings),
                nameof(ModContentPack_ReloadContentInt_Patches.ReloadAssetBundles),
            }
        )
        {
            var method = AccessTools.Method(
                typeof(ModContentPack_ReloadContentInt_Patches),
                helper
            );
            Expect.IsTrue(instructions.Any(i => i.Calls(method)), $"No call to {helper}.");
        }
    }

    // A mod's early content loader reloads textures on a thread other than the active one,
    // where a mod's destructive prefix on texture loading had no category open below it and
    // its time was credited to the prefix's own mod as hook time.
    [Test]
    public static IEnumerator AReplacedTextureLoadOffTheActiveThreadIsCreditedToTheTexturesStep()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TestStartup.TrackingOff);
            yield break;
        }

        var framesWaited = 0;
        while (TestStartup.StillStartingUp(ref framesWaited))
        {
            yield return null;
        }

        var mod = Utilities.FindModByAssembly(typeof(ReloadContentIntTimingTests).Assembly);
        Expect.IsNotNull(mod);
        var info = LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod);
        Expect.IsNotNull(info);
        const string Textures = ModContentPack_ReloadContentInt_Patches.TexturesCategory;
        var replaced = StartupImpactProfilerUtil.ReplacedBy(Textures, mod!.Name);
        var hookCategory =
            $"{HookTiming.Category}|{nameof(ReloadContentIntTimingTests)}.{nameof(LoadTexture)}";

        // The live session's own off-thread time for these categories, put back afterwards.
        var offThread = info!.Profiler.OffThreadMetrics;
        var hadTextures = offThread.TryGetValue(Textures, out var texturesBefore);
        var hadReplaced = offThread.TryGetValue(replaced, out var before);

        var harmony = new Harmony(TestHarmonyId);
        HookTiming.Activate(LoadingProgressMod.instance.Content);
        try
        {
            _ = harmony.Patch(
                AccessTools.Method(typeof(ReloadContentIntTimingTests), nameof(LoadTexture)),
                prefix: new HarmonyMethod(
                    typeof(ReloadContentIntTimingTests),
                    nameof(ReplacingPrefix)
                )
            );

            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    StartupImpactProfilerUtil.StartModProfiler(mod, Textures);
                    LoadTexture();
                    StartupImpactProfilerUtil.StopModProfiler(mod, Textures);
                }
                catch (Exception e)
                {
                    failure = e;
                }
            });
            thread.Start();
            thread.Join();

            Expect.IsNull(failure);
            _ = offThread.TryGetValue(replaced, out var after);
            Expect.GreaterThanOrEqualTo(after - before, 4f);
            Expect.IsFalse(offThread.ContainsKey(hookCategory));
            Expect.IsFalse(info.Profiler.Metrics.ContainsKey(replaced));
        }
        finally
        {
            HookTiming.Deactivate();
            harmony.UnpatchAll(harmony.Id);
            Restore(offThread, Textures, hadTextures, texturesBefore);
            Restore(offThread, replaced, hadReplaced, before);
            TestStartup.Forget(info.Profiler, hookCategory);
        }
    }

    private static void Restore(
        ConcurrentDictionary<string, float> metrics,
        string category,
        bool had,
        float value
    )
    {
        if (had)
        {
            metrics[category] = value;
        }
        else
        {
            _ = metrics.TryRemove(category, out _);
        }
    }
}
