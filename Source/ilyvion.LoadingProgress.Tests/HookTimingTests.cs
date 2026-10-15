using System.Diagnostics;
using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

// Each test turns hook timing on for itself, after the startup has turned it off.
[TestFixture(TestType.MainMenu)]
[WarningsAllowed(TestStartup.OtherModsWarnings)]
internal sealed class HookTimingTests
{
    // The method under test, with a body for Harmony to patch.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Target() => _ = Stopwatch.GetTimestamp();

    private static MethodInfo TargetMethod =>
        AccessTools.Method(typeof(HookTimingTests), nameof(Target));

    // The Harmony method hook timing times hook calls in, which a postfix can fail after.
    private static MethodInfo RewriteMethod =>
#if v1_7
        AccessTools.Method(typeof(MethodCreatorTools), nameof(MethodCreatorTools.EmitCodes));
#else
        AccessTools.Method(typeof(FaultBlockRewriter), nameof(FaultBlockRewriter.Rewrite));
#endif

    private const string TestHarmonyId = "ilyvion.LoadingProgress.Tests.HookTiming";
    private const string HookCategory =
        $"{HookTiming.Category}|{nameof(HookTimingTests)}.{nameof(Target)}";

    // Spins for a few milliseconds, as a hook with real work would.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void SlowPostfix()
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 5) { }
    }

    // Small enough for the runtime to inline into whatever calls it.
    public static void ThinPostfix() => SlowPostfix();

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool SkippingPrefix()
    {
        SlowPostfix();
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool RunningPrefix()
    {
        SlowPostfix();
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowingPostfix() => FailBelowTheHook();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FailBelowTheHook() =>
        throw new InvalidOperationException("A hook that fails, for the test.");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void InnerTarget() => _ = Stopwatch.GetTimestamp();

    // Calls InnerTarget, whose postfix throws, and catches what it throws.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CatchingPostfix()
    {
        try
        {
            InnerTarget();
        }
        catch (InvalidOperationException) { }
    }

    private static int _countingPostfixCalls;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void CountingPostfix() => _countingPostfixCalls++;

    private static int _rewritesToFail;

    // Fails the next _rewritesToFail replacements Harmony builds, after their hook calls
    // were timed.
    public static void FailingRewritePostfix()
    {
        if (_rewritesToFail > 0)
        {
            _rewritesToFail--;
            throw new InvalidOperationException("A replacement that fails to build, for the test.");
        }
    }

    // A method that fails to build however it is patched.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void UnbuildableTarget() => _ = Stopwatch.GetTimestamp();

    private static bool _prefixRunsOriginal;

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool ConditionalPrefix() => _prefixRunsOriginal;

    // Calls its own prefix from its body, where the prefix returns false.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void TargetCallingItsPrefix()
    {
        _prefixRunsOriginal = false;
        _ = ConditionalPrefix();
    }

    [Test]
    public static void TimingAcceptsTheRunningHarmony() =>
        Expect.IsNull(HookTiming.UnsupportedHarmony(typeof(Harmony).Assembly.GetName().Version));

    [Test]
    public static void TimingRefusesAnotherMajorVersionOfHarmony()
    {
        var running = typeof(Harmony).Assembly.GetName().Version?.Major ?? 0;
        Expect.IsNotNull(HookTiming.UnsupportedHarmony(new Version(running - 1, 0, 0, 0)));
        Expect.IsNotNull(HookTiming.UnsupportedHarmony(new Version(running + 1, 0, 0, 0)));
        Expect.IsNotNull(HookTiming.UnsupportedHarmony(null));
    }

    [Test]
    public static void TimingRefusesHarmonyInternalsOfAnotherShape()
    {
        var version = typeof(Harmony).Assembly.GetName().Version;
        Expect.IsNotNull(HookTiming.UnsupportedHarmony(version, null, null));
        Expect.IsNotNull(HookTiming.UnsupportedHarmony(version, TargetMethod, TargetMethod));
    }

    [Test]
    public static void TimingRefusesHarmonyWithoutTheInternals()
    {
        static (MethodInfo?, MethodInfo?) Missing()
        {
            throw new TypeLoadException("Harmony without the internals, for the test.");
        }

        Expect.AreEqual(
            HookTiming.UnsupportedHarmony(new Version(1, 2, 0, 0), Missing),
            HookTiming.UnsupportedHarmony(new Version(1, 2, 0, 0), null, null)
        );
        var version = typeof(Harmony).Assembly.GetName().Version;
        Expect.AreEqual(
            HookTiming.UnsupportedHarmony(version, Missing),
            HookTiming.UnsupportedHarmony(version, null, null)
        );
    }

    // The patch succeeds with its hook untimed, and timing stays off after it.
    [Test]
    [WarningsAllowed("no longer timed on their own")]
    public static IEnumerator APatchThatFailsToBuildWithTimedHooksIsBuiltWithoutThem()
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
        HookTiming.Activate(LoadingProgressMod.instance.Content);
        try
        {
            _ = harmony.Patch(
                RewriteMethod,
                postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(FailingRewritePostfix))
            );
            _rewritesToFail = 1;
            _ = harmony.Patch(
                TargetMethod,
                postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(CountingPostfix))
            );
            Expect.AreEqual(_rewritesToFail, 0);

            var calls = _countingPostfixCalls;
            Target();
            Expect.AreEqual(_countingPostfixCalls, calls + 1);

            var info = OwnModInfo();
            Expect.IsNotNull(info);
            Expect.IsFalse(info!.Profiler.Metrics.ContainsKey(HookCategory));
        }
        finally
        {
            _rewritesToFail = 0;
            HookTiming.Deactivate();
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
        }
    }

    // The patch fails as it would without timing, and timing stays on after it.
    [Test]
    public static IEnumerator APatchThatFailsToBuildWithoutTimedHooksLeavesTimingOn()
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
        HookTiming.Activate(LoadingProgressMod.instance.Content);
        try
        {
            _ = harmony.Patch(
                RewriteMethod,
                postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(FailingRewritePostfix))
            );
            _rewritesToFail = 2;
            Exception? failure = null;
            try
            {
                _ = harmony.Patch(
                    AccessTools.Method(typeof(HookTimingTests), nameof(UnbuildableTarget)),
                    postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(CountingPostfix))
                );
            }
            catch (Exception e)
            {
                failure = e;
            }
            Expect.IsNotNull(failure);
            Expect.AreEqual(_rewritesToFail, 0);

            _ = harmony.Patch(
                TargetMethod,
                postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(CountingPostfix))
            );
            Target();

            var info = OwnModInfo();
            Expect.IsNotNull(info);
            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(HookCategory));
        }
        finally
        {
            _rewritesToFail = 0;
            HookTiming.Deactivate();
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
        }
    }

    // The call in the body is the original's own, so it is neither timed as the prefix nor
    // taken as the prefix skipping the original.
    [Test]
    public static IEnumerator ACallToAHookFromTheOriginalsBodyIsNotTimedAsTheHook()
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

        const string Open = "LoadingProgress.Tests.HookTiming.Open";
        var mod = OwnMod();
        var info = OwnModInfo();
        Expect.IsNotNull(info);
        var replaced = StartupImpactProfilerUtil.ReplacedBy(Open, mod!.Name);
        var hookCategory =
            $"{HookTiming.Category}|{nameof(HookTimingTests)}.{nameof(TargetCallingItsPrefix)}";

        var harmony = new Harmony(TestHarmonyId);
        HookTiming.Activate(LoadingProgressMod.instance.Content);
        try
        {
            _ = harmony.Patch(
                AccessTools.Method(typeof(HookTimingTests), nameof(TargetCallingItsPrefix)),
                prefix: new HarmonyMethod(typeof(HookTimingTests), nameof(ConditionalPrefix))
            );
            _prefixRunsOriginal = true;
            StartupImpactProfilerUtil.StartModProfiler(mod, Open);
            TargetCallingItsPrefix();
            StartupImpactProfilerUtil.StopModProfiler(mod, Open);

            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(hookCategory));
            Expect.IsFalse(info.Profiler.Metrics.ContainsKey(replaced));
        }
        finally
        {
            HookTiming.Deactivate();
            harmony.UnpatchAll(harmony.Id);
            TestStartup.Forget(info!.Profiler, hookCategory);
            TestStartup.Forget(info.Profiler, Open);
            TestStartup.Forget(info.Profiler, replaced);
        }
    }

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

    // The hook's category, left open when the hook throws, is stopped by the stop of the
    // category open below it, which then stops cleanly; stopping one that is not on top logs
    // an error, which fails the test. The exception keeps the frame it was thrown in.
    [Test]
    public static IEnumerator AHookThatThrowsStillHasItsTimingStopped()
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

        const string Open = "LoadingProgress.Tests.HookTiming.Open";
        var mod = OwnMod();
        var info = OwnModInfo();
        Expect.IsNotNull(info);

        var harmony = new Harmony(TestHarmonyId);
        HookTiming.Activate(LoadingProgressMod.instance.Content);
        StartupImpactProfilerUtil.StartModProfiler(mod, Open);
        try
        {
            _ = harmony.Patch(
                TargetMethod,
                postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(ThrowingPostfix))
            );

            string? trace = null;
            try
            {
                Target();
            }
            catch (InvalidOperationException e)
            {
                trace = e.StackTrace;
            }
            StartupImpactProfilerUtil.StopModProfiler(mod, Open);

            Expect.IsTrue(trace?.IndexOf(nameof(FailBelowTheHook), StringComparison.Ordinal) >= 0);
            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(HookCategory));
            Expect.IsTrue(info.Profiler.Metrics.ContainsKey(Open));
        }
        finally
        {
            HookTiming.Deactivate();
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
            TestStartup.Forget(info!.Profiler, Open);
        }
    }

    // The inner hook's category, left open when it throws, is stopped with the outer hook's,
    // so the category open below both stops cleanly; stopping one that is not on top logs an
    // error, which fails the test.
    [Test]
    public static IEnumerator AHookThatThrowsInsideAnotherHookIsStoppedWithIt()
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

        const string Open = "LoadingProgress.Tests.HookTiming.Open";
        const string InnerCategory =
            $"{HookTiming.Category}|{nameof(HookTimingTests)}.{nameof(InnerTarget)}";
        var mod = OwnMod();
        var info = OwnModInfo();
        Expect.IsNotNull(info);

        var harmony = new Harmony(TestHarmonyId);
        HookTiming.Activate(LoadingProgressMod.instance.Content);
        try
        {
            _ = harmony.Patch(
                AccessTools.Method(typeof(HookTimingTests), nameof(InnerTarget)),
                postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(ThrowingPostfix))
            );
            _ = harmony.Patch(
                TargetMethod,
                postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(CatchingPostfix))
            );

            StartupImpactProfilerUtil.StartModProfiler(mod, Open);
            Target();
            StartupImpactProfilerUtil.StopModProfiler(mod, Open);

            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(HookCategory));
            Expect.IsTrue(info.Profiler.Metrics.ContainsKey(InnerCategory));
            Expect.IsTrue(info.Profiler.Metrics.ContainsKey(Open));
        }
        finally
        {
            HookTiming.Deactivate();
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
            TestStartup.Forget(info!.Profiler, InnerCategory);
            TestStartup.Forget(info.Profiler, Open);
        }
    }

    [Test]
    public static IEnumerator APrefixThatSkipsTheOriginalIsCreditedToWhatItReplaced()
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

        var (hookMs, replacedMs) = TimedInsideOpenCategory(nameof(SkippingPrefix));
        Expect.GreaterThanOrEqualTo(replacedMs, 4f);
        Expect.LessThan(hookMs, 4f);
    }

    [Test]
    public static IEnumerator APrefixThatRunsTheOriginalIsCreditedToItsMod()
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

        var (hookMs, replacedMs) = TimedInsideOpenCategory(nameof(RunningPrefix));
        Expect.GreaterThanOrEqualTo(hookMs, 4f);
        Expect.AreEqual(replacedMs, 0f);
    }

    [Test]
    public static void ReplacedTimeIsLabelledByWhatItReplacedAndTheReplacer()
    {
        var category = StartupImpactProfilerUtil.ReplacedBy($"{HookTiming.Category}|T.M", "Mod");

        Expect.AreEqual<string>(
            StartupImpactProfilerUtil.TranslateCategory(category),
            StartupImpactProfilerUtil.ReplacedByKey.Translate(
                StartupImpactProfilerUtil.TranslateCategory($"{HookTiming.Category}|T.M"),
                "Mod"
            )
        );
        Expect.AreEqual(
            StartupImpactPhaseViewData.PhaseKey(category),
            StartupImpactProfilerUtil.ReplacedBy(HookTiming.Category, "Mod")
        );
    }

    // Stopping the hook's category calls object.Equals, so a timed hook there runs inside the
    // timer's own work, where it runs untimed.
    [Test]
    public static IEnumerator AHookOnAMethodTheTimerCallsRunsUntimedInsideTheTimer()
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

        const string Open = "LoadingProgress.Tests.HookTiming.Open";
        const string EqualsCategory = $"{HookTiming.Category}|Object.Equals";
        var mod = OwnMod();
        var info = OwnModInfo();
        Expect.IsNotNull(info);

        var harmony = new Harmony(TestHarmonyId);
        HookTiming.Activate(LoadingProgressMod.instance.Content);
        try
        {
            _ = harmony.Patch(
                TargetMethod,
                postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(SlowPostfix))
            );
            _ = harmony.Patch(
                AccessTools.Method(typeof(object), nameof(Equals), [typeof(object)]),
                postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(CountingPostfix))
            );

            var calls = _countingPostfixCalls;
            StartupImpactProfilerUtil.StartModProfiler(mod, Open);
            Target();
            StartupImpactProfilerUtil.StopModProfiler(mod, Open);

            Expect.GreaterThan(_countingPostfixCalls, calls);
            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(HookCategory));
        }
        finally
        {
            HookTiming.Deactivate();
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
            TestStartup.Forget(info!.Profiler, Open);
            TestStartup.Forget(info.Profiler, EqualsCategory);
        }
    }

    [Test]
    public static void TimingHooksIsOffByDefault() =>
        Expect.IsFalse(new Settings().TimeHarmonyHooks);

    // A replacement built while timing was on keeps calling its hooks once timing is off.
    [Test]
    public static IEnumerator AHookStillRunsOnceTimingIsOff()
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
        HookTiming.Activate(LoadingProgressMod.instance.Content);
        try
        {
            _ = harmony.Patch(
                TargetMethod,
                postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(CountingPostfix))
            );
            HookTiming.Deactivate();

            var calls = _countingPostfixCalls;
            Target();
            Expect.AreEqual(_countingPostfixCalls, calls + 1);
        }
        finally
        {
            HookTiming.Deactivate();
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
        }
    }

    // A replacement built while timing was on times nothing once timing is off.
    [Test]
    public static IEnumerator NothingIsTimedOnceTimingIsOff()
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
        HookTiming.Activate(LoadingProgressMod.instance.Content);
        try
        {
            _ = harmony.Patch(
                TargetMethod,
                postfix: new HarmonyMethod(typeof(HookTimingTests), nameof(SlowPostfix))
            );
            HookTiming.Deactivate();
            Target();

            var info = OwnModInfo();
            Expect.IsNotNull(info);
            Expect.IsFalse(info!.Profiler.Metrics.ContainsKey(HookCategory));
        }
        finally
        {
            HookTiming.Deactivate();
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
        }
    }

    // Patches the target with the named hook of this class while timing is on, calls it once
    // and returns the milliseconds credited to this assembly's mod under the hook's category.
    private static float TimedUnderOwnMod(string hook)
    {
        var harmony = new Harmony(TestHarmonyId);
        HookTiming.Activate(LoadingProgressMod.instance.Content);
        try
        {
            _ = harmony.Patch(
                TargetMethod,
                postfix: new HarmonyMethod(typeof(HookTimingTests), hook)
            );
            var info = OwnModInfo();
            Expect.IsNotNull(info);
            _ = info!.Profiler.Metrics.TryGetValue(HookCategory, out var before);

            Target();

            Expect.IsTrue(info.Profiler.Metrics.TryGetValue(HookCategory, out var after));
            return after - before;
        }
        finally
        {
            HookTiming.Deactivate();
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
        }
    }

    // Patches the target with the named prefix of this class while timing is on, calls it once
    // inside a category open on this assembly's mod, and returns the milliseconds credited to
    // the prefix's category and to that category as replaced by the prefix's mod.
    private static (float HookMs, float ReplacedMs) TimedInsideOpenCategory(string prefix)
    {
        const string Open = "LoadingProgress.Tests.HookTiming.Open";
        var mod = OwnMod();
        var info = OwnModInfo();
        Expect.IsNotNull(info);
        var replaced = StartupImpactProfilerUtil.ReplacedBy(Open, mod!.Name);

        var harmony = new Harmony(TestHarmonyId);
        HookTiming.Activate(LoadingProgressMod.instance.Content);
        try
        {
            _ = harmony.Patch(
                TargetMethod,
                prefix: new HarmonyMethod(typeof(HookTimingTests), prefix)
            );
            _ = info!.Profiler.Metrics.TryGetValue(HookCategory, out var hookBefore);
            _ = info.Profiler.Metrics.TryGetValue(replaced, out var replacedBefore);

            StartupImpactProfilerUtil.StartModProfiler(mod, Open);
            Target();
            StartupImpactProfilerUtil.StopModProfiler(mod, Open);

            _ = info.Profiler.Metrics.TryGetValue(HookCategory, out var hookAfter);
            _ = info.Profiler.Metrics.TryGetValue(replaced, out var replacedAfter);
            return (hookAfter - hookBefore, replacedAfter - replacedBefore);
        }
        finally
        {
            HookTiming.Deactivate();
            harmony.UnpatchAll(harmony.Id);
            ForgetTestTime();
            TestStartup.Forget(info!.Profiler, Open);
            TestStartup.Forget(info.Profiler, replaced);
        }
    }

    private static ModContentPack? OwnMod() =>
        Utilities.FindModByAssembly(typeof(HookTimingTests).Assembly);

    private static ModInfo? OwnModInfo() =>
        OwnMod() is { } mod
            ? LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod)
            : null;

    // Takes the hook time these tests recorded back out of the live session.
    private static void ForgetTestTime()
    {
        if (OwnModInfo() is { } info)
        {
            TestStartup.Forget(info.Profiler, HookCategory);
        }
    }
}
