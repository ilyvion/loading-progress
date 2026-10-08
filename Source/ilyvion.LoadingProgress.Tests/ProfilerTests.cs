using System.Diagnostics;
using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;
using ilyvion.LoadingProgress.StartupImpact.Patches;

namespace ilyvion.LoadingProgress.Tests;

// Some of these tests wait through the startup's tail, while other mods' post-load events run.
[TestFixture(TestType.MainMenu)]
[WarningsAllowed(TestStartup.OtherModsWarnings)]
internal sealed class ProfilerTests
{
    private const string TestHarmonyId = "ilyvion.LoadingProgress.Tests.ProfilerTests";
    private const string ConstructorCategory = "LoadingProgress.StartupImpact.ModConstructor";
    private const string NextCategory = "LoadingProgress.Tests.ProfilerTests.Next";

    // Hands back the given times, in order, one per stop.
    private sealed class ScriptedProfiler(params float[] laps) : SingleThreadedProfiler("test")
    {
        private int _lap;

        public override void Start() { }

        public override float Stop() => laps[_lap++];
    }

    // A category started inside another one used to take the other's time so far with it:
    // the outer category was credited only with what it ran after the inner one stopped.
    [Test]
    public static void StartingInsideACategoryHandsBackItsTimeSoFar()
    {
        var profiler = new ScriptedProfiler(300f, 50f, 100f);

        var none = profiler.Start("outer", out var nothingOpen);
        var outerSoFar = profiler.Start("inner", out var interrupted);

        Expect.AreApproximatelyEqual(0f, none);
        Expect.IsNull(nothingOpen);
        Expect.AreApproximatelyEqual(300f, outerSoFar);
        Expect.AreEqual("outer", interrupted);
        Expect.AreApproximatelyEqual(50f, profiler.Stop("inner"));
        Expect.AreApproximatelyEqual(100f, profiler.Stop("outer"));
    }

    // A start hands back the open category's time before it opens the new one, so a start
    // that throws while recording that time has opened nothing: the open category is still on
    // top for the stop that was always coming for it.
    [Test]
    public static void TheOpenCategoryStaysOnTopUntilTheNewOneIsPushed()
    {
        var profiler = new ScriptedProfiler(300f, 100f);
        profiler.Start("outer");

        var outerSoFar = profiler.Interrupt(out var interrupted);

        Expect.AreApproximatelyEqual(300f, outerSoFar);
        Expect.AreEqual("outer", interrupted);
        Expect.AreApproximatelyEqual(100f, profiler.Stop("outer", out var stopped));
        Expect.AreEqual("outer", stopped);
    }

    [Test]
    public static IEnumerator AnOuterCategoryKeepsItsTimeFromBeforeAnInnerOne()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TestStartup.TrackingOff);
            yield break;
        }

        // Every stop on the active thread also goes to the live session's stage ledger.
        var framesWaited = 0;
        while (TestStartup.StillStartingUp(ref framesWaited))
        {
            yield return null;
        }

        using var profiler = new Profiler("test");
        profiler.Start("outer");
        Spin(60);
        profiler.Start("inner");
        Spin(20);
        _ = profiler.Stop("inner");
        Spin(30);
        _ = profiler.Stop("outer");

        Expect.IsTrue(profiler.Metrics.TryGetValue("outer", out var outer));
        Expect.IsTrue(profiler.Metrics.TryGetValue("inner", out var inner));
        Expect.GreaterThanOrEqualTo(outer, 89f);
        Expect.GreaterThanOrEqualTo(inner, 19f);
    }

    // The profiler records the open category's time before it opens the new one. In the other
    // order, a start whose recording throws would leave the new category open with no stop
    // coming for it, and the open category's stop would close and record it instead, with a
    // mismatch error. TheOpenCategoryStaysOnTopUntilTheNewOneIsPushed checks the halves the
    // start is made of; this checks the start itself.
    [Test]
    public static IEnumerator AStartWhoseRecordingThrowsOpensNothing()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TestStartup.TrackingOff);
            yield break;
        }

        // A recording reaches the live session's stage ledger, where it is made to throw.
        var framesWaited = 0;
        while (TestStartup.StillStartingUp(ref framesWaited))
        {
            yield return null;
        }

        using var profiler = new Profiler("test");
        profiler.Start("outer");
        RecordingFailure.During(() =>
            _ = Expect.Throws<InvalidOperationException>(() => profiler.Start("inner"))
        );
        _ = profiler.Stop("outer");

        Expect.IsTrue(profiler.Metrics.ContainsKey("outer"));
        Expect.IsFalse(profiler.Metrics.ContainsKey("inner"));
    }

    // The timing patches closed their categories in postfixes, which do not run when the
    // method throws, and the engine goes on loading after a mod constructor that throws. The
    // category stayed open, and each later start on that mod's timer credited it with the
    // whole gap since the timer's previous step.
    [Test]
    public static IEnumerator AMethodThatThrowsHasItsCategoryClosed()
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

        var (mod, profiler, before) = ConstructorTimingSoFar();
        var harmony = TimeLikeAConstructor(nameof(ThrowingConstructor));
        try
        {
            try
            {
                ThrowingConstructor(mod);
            }
            catch (InvalidOperationException) { }

            // A stretch with nothing timed, then another category on the same timer: a
            // category left open would be credited with the stretch when that one starts.
            Spin(40);
            StartupImpactProfilerUtil.StartModProfiler(mod, NextCategory);
            StartupImpactProfilerUtil.StopModProfiler(mod, NextCategory);

            _ = profiler.Metrics.TryGetValue(ConstructorCategory, out var after);
            Expect.LessThanOrEqualTo(after - before, 20f);
        }
        finally
        {
            Untime(harmony, mod, profiler, before);
        }
    }

    // On a normal return the postfix stops the category at its place among other mods'
    // postfixes. A finalizer runs after all of them, so a stop there would take in another
    // mod's postfix on the same method, such as Missile Girl writing its cache after
    // ParseAndProcessXML.
    [Test]
    public static IEnumerator AnotherModsLaterPostfixStaysOutsideTheCategory()
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

        var (mod, profiler, before) = ConstructorTimingSoFar();
        var harmony = TimeLikeAConstructor(nameof(ReturningConstructor), nameof(SlowPostfix));
        try
        {
            ReturningConstructor(mod);

            _ = profiler.Metrics.TryGetValue(ConstructorCategory, out var after);
            Expect.LessThanOrEqualTo(after - before, 20f);
        }
        finally
        {
            Untime(harmony, mod, profiler, before);
        }
    }

    // When a postfix after ours throws, ours has already stopped the category, and the
    // finalizer must not stop it again: stopping one that is not running logs an error, which
    // fails the test.
    [Test]
    public static IEnumerator ALaterPostfixThatThrowsLeavesOneStop()
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

        var (mod, profiler, before) = ConstructorTimingSoFar();
        var harmony = TimeLikeAConstructor(nameof(ReturningConstructor), nameof(ThrowingPostfix));
        try
        {
            var threw = false;
            try
            {
                ReturningConstructor(mod);
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Expect.IsTrue(threw);
        }
        finally
        {
            Untime(harmony, mod, profiler, before);
        }
    }

    // This mod and its constructor time so far, before a test times one of the stand-ins.
    private static (ModContentPack Mod, Profiler Profiler, float Before) ConstructorTimingSoFar()
    {
        var mod = Utilities.FindModByAssembly(typeof(ProfilerTests).Assembly);
        Expect.IsNotNull(mod);
        var profiler = LoadingProgressMod
            .instance.StartupImpact.Modlist.GetModInfoFor(mod)!
            .Profiler;
        _ = profiler.Metrics.TryGetValue(ConstructorCategory, out var before);
        return (mod!, profiler, before);
    }

    // Puts the mod constructor timing on one of the stand-ins below, with another mod's
    // postfix after it when one is named.
    private static Harmony TimeLikeAConstructor(string standIn, string? laterPostfix = null)
    {
        var harmony = new Harmony(TestHarmonyId);
        var method = AccessTools.Method(typeof(ProfilerTests), standIn);
        _ = harmony.Patch(
            method,
            prefix: new HarmonyMethod(typeof(Mod_Constructor_Patches), "Prefix"),
            postfix: new HarmonyMethod(typeof(Mod_Constructor_Patches), "Postfix"),
            finalizer: new HarmonyMethod(typeof(Mod_Constructor_Patches), "Finalizer")
        );
        if (laterPostfix != null)
        {
            _ = harmony.Patch(
                method,
                postfix: new HarmonyMethod(typeof(ProfilerTests), laterPostfix)
                {
                    priority = HarmonyLib.Priority.Low,
                }
            );
        }
        return harmony;
    }

    // Takes the timing off again and the test's time back out of the live session. A category
    // a regression left open is closed first, so it cannot go on collecting time.
    private static void Untime(Harmony harmony, ModContentPack mod, Profiler profiler, float before)
    {
        harmony.UnpatchAll(harmony.Id);
        if (Mod_Constructor_Patches._currentModAssembly != null)
        {
            Mod_Constructor_Patches._currentModAssembly = null;
            StartupImpactProfilerUtil.StopModProfiler(mod, ConstructorCategory);
        }
        _ = profiler.Metrics.TryGetValue(ConstructorCategory, out var now);
        profiler.Discount(ConstructorCategory, now - before);
        if (profiler.Metrics.TryGetValue(NextCategory, out var next))
        {
            profiler.Discount(NextCategory, next);
            _ = profiler.Metrics.TryRemove(NextCategory, out _);
        }
    }

    // Stand in for mod constructors, with the argument the patch reads.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowingConstructor(ModContentPack mod) =>
        throw new InvalidOperationException($"A mod constructor that fails, for the test: {mod}");

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ReturningConstructor(ModContentPack mod) => _ = mod;

    // Another mod's postfixes, run after the timing's own.
    public static void SlowPostfix() => Spin(40);

    public static void ThrowingPostfix() =>
        throw new InvalidOperationException("A later postfix that fails, for the test.");

    // Busy for the given milliseconds, as real work would be.
    private static void Spin(int ms)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < ms) { }
    }
}
