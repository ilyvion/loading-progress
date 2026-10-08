using System.Diagnostics;
using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

// Some of these tests wait through the startup's tail, while other mods' post-load events run.
[TestFixture(TestType.MainMenu)]
[WarningsAllowed(TestStartup.OtherModsWarnings)]
internal sealed class CallAllHookTimingTests
{
    private const string TestHarmonyId = "ilyvion.LoadingProgress.Tests.CallAllHookTiming";
    private const string TestBaseCategory = "LoadingProgress.Tests.CallAllHookTiming.Call";
    private const string HookCategoryPrefix =
        $"{CallAllHookTiming.Category}|{nameof(CallAllHookTimingTests)}.";

    // The method under test, with a body for Harmony to patch. Not inlined, so the call from
    // the helper below still reaches the patched method.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Target() => _ = Stopwatch.GetTimestamp();

    // Spins for a few milliseconds, as a hook with real work would.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SlowPostfix()
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5) { }
    }

    // A few bytes of IL around the real work, the shape of many mods' hooks, and small enough
    // for the runtime to inline into the patched method's replacement.
    public static void ThinPostfix() => SlowPostfix();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowingPostfix() =>
        throw new InvalidOperationException("A hook that fails, for the test.");

    private static MethodInfo TargetMethod =>
        AccessTools.Method(typeof(CallAllHookTimingTests), nameof(Target));

    private static MethodInfo HookMethod =>
        AccessTools.Method(typeof(CallAllHookTimingTests), nameof(SlowPostfix));

    private static Profiler BaseGame => LoadingProgressMod.instance.StartupImpact.BaseGameProfiler;

    // One heading naming every mod with a hook on the call cannot be hidden with the mod it
    // belongs to; a hook timed under its own mod can.
    [Test]
    public static IEnumerator AHookIsTimedUnderTheModThatOwnsIt()
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

        Expect.GreaterThanOrEqualTo(TimedUnderOwnMod(nameof(SlowPostfix)), 4f);
    }

    // The replacement Harmony compiles for a patched method takes a copy of a hook this small
    // instead of a call to it, and a detour put on the hook afterwards is never reached from
    // there unless the replacement is built again once the detour is in place.
    [Test]
    public static IEnumerator AHookSmallEnoughToBeInlinedIsStillTimed()
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

        Expect.GreaterThanOrEqualTo(TimedUnderOwnMod(nameof(ThinPostfix)), 4f);
    }

    // The call's own category stops while a hook runs, or every hook's time would be counted
    // twice: on its mod, and again under the call.
    [Test]
    public static IEnumerator TheCallsCategoryPausesWhileAHookRuns()
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

        var harmony = new Harmony(TestHarmonyId);
        _ = harmony.Patch(
            TargetMethod,
            postfix: new HarmonyMethod(typeof(CallAllHookTimingTests), nameof(SlowPostfix))
        );
        try
        {
            TimedCall();

            Expect.IsTrue(BaseGame.Metrics.TryGetValue(TestBaseCategory, out var underCall));
            Expect.LessThanOrEqualTo(underCall, 3f);
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
        }
    }

    // A hook that throws still has its timing closed and the call's category restarted, so
    // the call's category stops cleanly; stopping one that is not running logs an error,
    // which fails the test.
    [Test]
    public static IEnumerator AHookThatThrowsLeavesTheTimingIntact()
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

        var harmony = new Harmony(TestHarmonyId);
        _ = harmony.Patch(
            TargetMethod,
            postfix: new HarmonyMethod(typeof(CallAllHookTimingTests), nameof(ThrowingPostfix))
        );
        try
        {
            var threw = false;
            TimedCall(() =>
            {
                try
                {
                    Target();
                }
                catch (InvalidOperationException)
                {
                    threw = true;
                }
            });

            Expect.IsTrue(threw);
            Expect.IsTrue(BaseGame.Metrics.ContainsKey(TestBaseCategory));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
        }
    }

    [Test]
    public static void TheTimingPatchesComeOffAgainWithTheCall()
    {
        var harmony = new Harmony(TestHarmonyId);
        _ = harmony.Patch(
            TargetMethod,
            postfix: new HarmonyMethod(typeof(CallAllHookTimingTests), nameof(SlowPostfix))
        );
        try
        {
            var timing = CallAllHookTiming.Install(TargetMethod);
            Expect.IsNotNull(Harmony.GetPatchInfo(HookMethod));
            Expect.IsTrue(
                Harmony.GetPatchInfo(TargetMethod).Owners.Contains(CallAllHookTiming.HarmonyId)
            );

            timing.Remove();

            var onHook = Harmony.GetPatchInfo(HookMethod);
            Expect.IsTrue(onHook == null || onHook.Owners.Count == 0);
            var onTarget = Harmony.GetPatchInfo(TargetMethod);
            Expect.IsFalse(onTarget.Owners.Contains(CallAllHookTiming.HarmonyId));
            Expect.IsTrue(onTarget.Owners.Contains(harmony.Id));
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // The pass's category used to be handed to the hook timing before it was started. When
    // starting it failed, each hook still stopped it and started it again, and it stayed open
    // over the rest of loading.
    [Test]
    [WarningsAllowed("Could not time the static constructor pass")]
    public static void APassWhoseTimingFailsGivesTheHooksNothingToPause()
    {
        CallAllHookTiming.BaseCategory = null;
        try
        {
            var timed = StaticConstructorOnStartupUtilityReplacement.StartPassTiming(
                TestBaseCategory,
                static _ => throw new InvalidOperationException("A start that fails, for the test.")
            );

            Expect.IsFalse(timed);
            Expect.IsNull(CallAllHookTiming.BaseCategory);
        }
        finally
        {
            CallAllHookTiming.BaseCategory = null;
        }
    }

    [Test]
    public static void APassWhoseTimingStartsIsHandedToTheHooks()
    {
        try
        {
            var timed = StaticConstructorOnStartupUtilityReplacement.StartPassTiming(
                TestBaseCategory,
                static _ => { }
            );

            Expect.IsTrue(timed);
            Expect.AreEqual(TestBaseCategory, CallAllHookTiming.BaseCategory);
        }
        finally
        {
            CallAllHookTiming.BaseCategory = null;
        }
    }

    // The usual case: every hook is timed under its own mod, and the call names no one.
    [Test]
    public static void ThePassNamesNoOneWhenEveryHookIsTimed()
    {
        var category = StaticConstructorOnStartupUtilityReplacement.CallAllPassCategoryFor([]);

        Expect.IsFalse(category.Contains('|', StringComparison.Ordinal));
        Expect.IsTrue(category.CanTranslate());
    }

    [Test]
    public static void ThePassNamesTheModsWhoseHooksCouldNotBeTimed()
    {
        var text = StartupImpactProfilerUtil.TranslateCategory(
            StaticConstructorOnStartupUtilityReplacement.CallAllPassCategoryFor(["Mod A", "Mod B"])
        );

        Expect.IsTrue(text.Contains("Mod A, Mod B", StringComparison.Ordinal));
        Expect.IsFalse(text.Contains("{0}", StringComparison.Ordinal));
    }

    [Test]
    public static void AModsHookIsTimed() =>
        Expect.AreEqual(
            CallAllHookTiming.HookHandling.Time,
            CallAllHookTiming.HandlingFor(typeof(CallAllHookTimingTests).Assembly, false, OwnMod())
        );

    [Test]
    public static void LoadingProgressesOwnHooksAreLeftAlone() =>
        Expect.AreEqual(
            CallAllHookTiming.HookHandling.Skip,
            CallAllHookTiming.HandlingFor(typeof(CallAllHookTiming).Assembly, false, OwnMod())
        );

    [Test]
    public static void AHookNoModOwnsIsNamedAsUntimed() =>
        Expect.AreEqual(
            CallAllHookTiming.HookHandling.NameAsUntimed,
            CallAllHookTiming.HandlingFor(typeof(Harmony).Assembly, false, null)
        );

    // A hook generated at run time has no declaring type to find its mod by. Its time stays
    // under the call's heading, which used to leave it unnamed.
    [Test]
    public static void AHookWithNoTypeIsNamedAsUntimed() =>
        Expect.AreEqual(
            CallAllHookTiming.HookHandling.NameAsUntimed,
            CallAllHookTiming.HandlingFor(null, false, null)
        );

    // The engine's CallAll pass runs only to fire other mods' hooks, so a failure while timing
    // them must not keep it from running. An exception out of the timing used to skip the
    // call, and could leave timing patches on hook methods for the rest of the session. The
    // hooks then all run untimed, so the heading names their owners.
    [Test]
    [WarningsAllowed("Could not time the hooks on")]
    public static void ATimingThatFailsTakesItsPatchesOffAgain()
    {
        var harmony = new Harmony(TestHarmonyId);
        _ = harmony.Patch(
            TargetMethod,
            prefix: new HarmonyMethod(typeof(CallAllHookTimingTests), nameof(SlowPostfix)),
            postfix: new HarmonyMethod(typeof(CallAllHookTimingTests), nameof(ThinPostfix))
        );
        CallAllHookTiming? timing = null;
        try
        {
            // The first hook is patched before the lookup for the second one fails; every
            // lookup after that fails too, so the owner is named by its Harmony id.
            var lookups = 0;
            timing = CallAllHookTiming.Install(
                TargetMethod,
                assembly =>
                    ++lookups == 1
                        ? Utilities.FindModByAssembly(assembly)
                        : throw new InvalidOperationException(
                            "A mod lookup that fails, for the test."
                        )
            );

            Expect.GreaterThanOrEqualTo(lookups, 2);
            Expect.AreEqual(1, timing.UntimedOwners.Count);
            Expect.AreEqual(TestHarmonyId, timing.UntimedOwners[0]);
            var onHook = Harmony.GetPatchInfo(HookMethod);
            Expect.IsTrue(onHook == null || !onHook.Owners.Contains(CallAllHookTiming.HarmonyId));
        }
        finally
        {
            // Takes off whatever a regressed install left behind, so the tests after this one
            // start clean.
            timing?.Remove();
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // A method listed as both a prefix and a postfix is timed once, from its first entry. The
    // second entry used to name its mod as untimed, though all its time sat on its own row.
    [Test]
    public static void AHookListedTwiceIsTimedAndNotNamedAsUntimed()
    {
        var harmony = new Harmony(TestHarmonyId);
        var hook = new HarmonyMethod(typeof(CallAllHookTimingTests), nameof(SlowPostfix));
        _ = harmony.Patch(TargetMethod, prefix: hook, postfix: hook);
        try
        {
            var timing = CallAllHookTiming.Install(TargetMethod);
            try
            {
                Expect.IsEmpty(timing.UntimedOwners);
                Expect.IsTrue(
                    Harmony.GetPatchInfo(HookMethod).Owners.Contains(CallAllHookTiming.HarmonyId)
                );
            }
            finally
            {
                timing.Remove();
            }
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
        }
    }

    // A hook's start that throws while recording a category the mod has open below it has
    // opened nothing. The finalizer used to stop the hook's category anyway, which stopped the
    // open one in its place, and the test's own stop of that category then found it gone.
    // Either stop logs an error, which fails the test.
    [Test]
    [WarningsAllowed("Timing a hook on the static constructor pass failed")]
    public static IEnumerator AHookWhoseTimingFailsToStartHasNothingStopped()
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

        const string Open = "LoadingProgress.Tests.CallAllHookTiming.Open";
        var mod = OwnMod();
        var info = OwnModInfo();
        Expect.IsNotNull(info);
        var harmony = new Harmony(TestHarmonyId);
        _ = harmony.Patch(
            TargetMethod,
            postfix: new HarmonyMethod(typeof(CallAllHookTimingTests), nameof(SlowPostfix))
        );
        var timing = CallAllHookTiming.Install(TargetMethod);
        StartupImpactProfilerUtil.StartModProfiler(mod, Open);
        var openStopped = false;
        try
        {
            RecordingFailure.During(Target);
            StartupImpactProfilerUtil.StopModProfiler(mod, Open);
            openStopped = true;

            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(Open));
            Expect.IsFalse(
                info.Profiler.Metrics.ContainsKey($"{HookCategoryPrefix}{nameof(SlowPostfix)}")
            );
        }
        finally
        {
            if (!openStopped)
            {
                StartupImpactProfilerUtil.StopModProfiler(mod, Open);
            }
            timing.Remove();
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
            TestStartup.Forget(info!.Profiler, Open);
        }
    }

    // Times one call of the target under the test's own base-game category, the way the
    // startup times the engine's pass, with the timing patches on for the call only.
    private static void TimedCall(Action? call = null)
    {
        var timing = CallAllHookTiming.Install(TargetMethod);
        CallAllHookTiming.BaseCategory = TestBaseCategory;
        StartupImpactProfilerUtil.StartBaseGameProfiler(TestBaseCategory);
        try
        {
            (call ?? Target)();
        }
        finally
        {
            StartupImpactProfilerUtil.StopBaseGameProfiler(TestBaseCategory);
            timing.Remove();
        }
    }

    // Patches the target with the named hook of this class, times one call through the timing
    // patches and returns the milliseconds credited to this assembly's mod under the hook's
    // category.
    private static float TimedUnderOwnMod(string hook)
    {
        var harmony = new Harmony(TestHarmonyId);
        _ = harmony.Patch(
            TargetMethod,
            postfix: new HarmonyMethod(typeof(CallAllHookTimingTests), hook)
        );
        try
        {
            var info = OwnModInfo();
            Expect.IsNotNull(info);
            var category = $"{HookCategoryPrefix}{hook}";
            _ = info!.Profiler.Metrics.TryGetValue(category, out var before);

            var timing = CallAllHookTiming.Install(TargetMethod);
            Expect.IsEmpty(timing.UntimedOwners);
            Target();
            timing.Remove();

            Expect.IsTrue(info.Profiler.Metrics.TryGetValue(category, out var after));
            return after - before;
        }
        finally
        {
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
        }
    }

    private static ModContentPack? OwnMod() =>
        Utilities.FindModByAssembly(typeof(CallAllHookTimingTests).Assembly);

    private static ModInfo? OwnModInfo() =>
        OwnMod() is { } mod
            ? LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod)
            : null;

    // Takes everything these tests timed back out of the live session, totals included: the
    // test's own base-game category, and its hooks' categories on this assembly's mod. The
    // session is saved when the menu is reached, and a test run starts before that.
    private static void ForgetTestTime()
    {
        TestStartup.Forget(BaseGame, TestBaseCategory);
        if (OwnModInfo() is not { } info)
        {
            return;
        }
        foreach (
            var category in info
                .Profiler.Metrics.Keys.Where(key =>
                    key.StartsWith(HookCategoryPrefix, StringComparison.Ordinal)
                )
                .ToList()
        )
        {
            TestStartup.Forget(info.Profiler, category);
        }
    }
}
