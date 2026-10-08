using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class DeferredActionRunTests
{
    private const string Label = "ilyvion.LoadingProgress.Tests.DeferredActionRunTests -> Test";
    private const string Category =
        $"{LongEventHandler_ExecuteToExecuteWhenFinished_Patches.DeferredActionCategory}|{Label}";
    private const string TrackingOff = "Startup impact tracking is off.";

    // An action that threw used to leave its category open on its owner's timer: the time it
    // had run was never recorded, and every category started there afterwards ran inside it
    // for the rest of the startup.
    [Test]
    [ErrorsAllowed("Could not execute post-long-event action")]
    public static void AnActionThatThrowsHasItsCategoryClosed()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TrackingOff);
            return;
        }

        var mod = OwnMod();
        var info = OwnModInfo();
        Expect.IsNotNull(info);
        try
        {
            LongEventHandler_ExecuteToExecuteWhenFinished_Patches.RunDeferredAction(
                () =>
                    throw new InvalidOperationException(
                        "A deferred action that fails, for the test."
                    ),
                Label,
                timed: true
            );

            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(Category));
        }
        finally
        {
            // If the category was left open, close it, or every later category on this timer
            // would run inside it for the rest of the session.
            if (!info!.Profiler.Metrics.ContainsKey(Category))
            {
                StartupImpactProfilerUtil.StopModProfiler(mod, Category);
            }
            Forget(info.Profiler, Category);
        }
    }

    [Test]
    public static void AnActionRunsTimedUnderItsOwner()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TrackingOff);
            return;
        }

        var info = OwnModInfo();
        Expect.IsNotNull(info);
        try
        {
            var ran = false;
            LongEventHandler_ExecuteToExecuteWhenFinished_Patches.RunDeferredAction(
                () => ran = true,
                Label,
                timed: true
            );

            Expect.IsTrue(ran);
            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(Category));
        }
        finally
        {
            Forget(info!.Profiler, Category);
        }
    }

    // Finding an action's owner walks its closure by reflection, for each of the tens of
    // thousands of actions a large mod list queues. With tracking off at startup nothing used
    // the result, and the walk ran anyway.
    [Test]
    public static void AnUntimedActionNeitherLooksItsOwnerUpNorRecordsTime()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TrackingOff);
            return;
        }

        var info = OwnModInfo();
        Expect.IsNotNull(info);
        try
        {
            var ran = false;
            var lookups = 0;
            LongEventHandler_ExecuteToExecuteWhenFinished_Patches.RunDeferredAction(
                () => ran = true,
                Label,
                timed: false,
                _ =>
                {
                    lookups++;
                    return (null, false);
                }
            );

            Expect.IsTrue(ran);
            Expect.AreEqual(0, lookups);
            Expect.IsFalse(info!.Profiler.Metrics.ContainsKey(Category));
        }
        finally
        {
            Forget(info!.Profiler, Category);
        }
    }

    // A failure in the timing used to skip the action itself, and the error blamed the action.
    [Test]
    [WarningsAllowed("Could not time the deferred action")]
    public static void AnActionWhoseTimingFailsStillRuns()
    {
        var ran = false;
        LongEventHandler_ExecuteToExecuteWhenFinished_Patches.RunDeferredAction(
            () => ran = true,
            Label,
            timed: true,
            _ => throw new InvalidOperationException("An owner lookup that fails, for the test.")
        );

        Expect.IsTrue(ran);
    }

    private static ModContentPack? OwnMod() =>
        Utilities.FindModByAssembly(typeof(DeferredActionRunTests).Assembly);

    private static ModInfo? OwnModInfo() =>
        OwnMod() is { } mod
            ? LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod)
            : null;

    // Takes the test's category back out of the live session's list.
    private static void Forget(Profiler profiler, string category) =>
        _ = profiler.Metrics.TryRemove(category, out _);
}
