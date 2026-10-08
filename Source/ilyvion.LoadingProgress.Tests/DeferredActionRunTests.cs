using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

// Some of these tests wait through the startup's tail, while other mods' post-load events run.
[TestFixture(TestType.MainMenu)]
[WarningsAllowed(TestStartup.OtherModsWarnings)]
internal sealed class DeferredActionRunTests
{
    private const string Label = "ilyvion.LoadingProgress.Tests.DeferredActionRunTests -> Test";
    private const string Category =
        $"{LongEventHandler_ExecuteToExecuteWhenFinished_Patches.DeferredActionCategory}|{Label}";

    // An action that threw used to leave its category open on its owner's timer: the time it
    // had run was never recorded, and every category started there afterwards ran inside it
    // for the rest of the startup.
    [Test]
    [ErrorsAllowed("Could not execute post-long-event action")]
    public static IEnumerator AnActionThatThrowsHasItsCategoryClosed()
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
            TestStartup.Forget(info.Profiler, Category);
        }
    }

    [Test]
    public static IEnumerator AnActionRunsTimedUnderItsOwner()
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
            TestStartup.Forget(info!.Profiler, Category);
        }
    }

    // After loading, deferred actions used to run inside whichever event queued them, so a
    // mod's action queued from a hook on the interface's initialization was credited to the
    // base game. They are now timed under their own mod, as time after loading.
    [Test]
    public static IEnumerator AnActionAfterLoadingIsTimedAsTimeAfterLoading()
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

        var info = OwnModInfo();
        Expect.IsNotNull(info);
        var category = $"{PostLoadTracker.Category}|{Label}";
        try
        {
            LongEventHandler_ExecuteToExecuteWhenFinished_Patches.RunDeferredAction(
                static () => { },
                Label,
                timed: true,
                PostLoadTracker.Category
            );

            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(category));
        }
        finally
        {
            TestStartup.Forget(info!.Profiler, category);
        }
    }

    // The engine runs an action queued during the pass in the same pass, then clears the
    // queue; the timed pass after loading does the same.
    [Test]
    public static IEnumerator TheActionsAfterLoadingRunAsTheEngineRunsThem()
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

        var info = OwnModInfo();
        Expect.IsNotNull(info);
        List<Action> queue = [];
        List<string> ran = [];
        void Second()
        {
            ran.Add("second");
        }
        void First()
        {
            ran.Add("first");
            queue.Add(Second);
        }
        Action first = First;
        var category =
            $"{PostLoadTracker.Category}|{PostLoadTracker.DescribeCode(first.Method.DeclaringType, first.Method.Name)}";
        try
        {
            queue.Add(first);
            PostLoadTracker.RunDeferredActions(queue);

            Expect.AreEqual(2, ran.Count);
            Expect.AreEqual("first", ran[0]);
            Expect.AreEqual("second", ran[1]);
            Expect.IsEmpty(queue);
            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(category));
        }
        finally
        {
            TestStartup.Forget(info!.Profiler, category);
        }
    }

    // The event the actions after loading run after is paused while they run, so it keeps
    // only its own time, and it goes on being timed afterwards. The event here runs the
    // engine's code, so it is timed on the base game's timer and the action on this mod's:
    // without the pause, both would count the action's time. Checked by what is recorded
    // when, not by comparing durations, which a collection pause in a large game can stretch.
    [Test]
    public static IEnumerator TheEventTheActionsRunAfterIsPausedMeanwhile()
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

        var info = OwnModInfo();
        Expect.IsNotNull(info);
        var baseGame = LoadingProgressMod.instance.StartupImpact.BaseGameProfiler;
        // Never run: only the code it names decides whose timer the event is on.
        var queuedEvent = new LongEventHandler.QueuedLongEvent
        {
            eventAction = Log.ResetMessageCount,
            eventTextKey = $"{nameof(DeferredActionRunTests)}.Event",
        };
        var eventCategory = $"{PostLoadTracker.Category}|{PostLoadTracker.Describe(queuedEvent)}";
        // What the event had recorded when the action ran: nothing unless it was stopped first.
        var eventMsDuringTheAction = -1f;
        void RecordTheEvent()
        {
            if (baseGame.Metrics.TryGetValue(eventCategory, out var ms))
            {
                eventMsDuringTheAction = ms;
            }
        }
        Action action = RecordTheEvent;
        var actionCategory =
            $"{PostLoadTracker.Category}|{PostLoadTracker.DescribeCode(action.Method.DeclaringType, action.Method.Name)}";
        try
        {
            PostLoadTracker.StartCurrent(queuedEvent);
            PostLoadTracker.RunDeferredActions([action]);
            // The event's own time after its actions, which it records only if resumed.
            Thread.Sleep(30);
            PostLoadTracker.StopCurrent(0f);

            Expect.IsTrue(info!.Profiler.Metrics.ContainsKey(actionCategory));
            Expect.GreaterThanOrEqualTo(eventMsDuringTheAction, 0f);
            Expect.IsTrue(baseGame.Metrics.TryGetValue(eventCategory, out var eventMs));
            Expect.GreaterThanOrEqualTo(eventMs - eventMsDuringTheAction, 25f);
        }
        finally
        {
            // Nothing else is current once the startup has ended, so this stops only the
            // test's event, if a failure left it open.
            PostLoadTracker.StopCurrent(0f);
            TestStartup.Forget(info!.Profiler, actionCategory);
            TestStartup.Forget(baseGame, eventCategory);
        }
    }

    // The engine runs the queue on the thread that asks for it, and only the main thread's
    // timings count, so another thread's call is left to the engine. The pass also needs the
    // queue to hold something, loading to be over, and the wait after loading to be timed.
    [Test]
    public static void OnlyTheMainThreadHandsTheQueueToThePassAfterLoading()
    {
        Expect.IsTrue(
            LongEventHandler_ExecuteToExecuteWhenFinished_Patches.HandsTheQueueToTheTail(
                1,
                LoadingStage.Finished,
                timingTheTail: true,
                onMainThread: true
            )
        );
        Expect.IsFalse(
            LongEventHandler_ExecuteToExecuteWhenFinished_Patches.HandsTheQueueToTheTail(
                1,
                LoadingStage.Finished,
                timingTheTail: true,
                onMainThread: false
            )
        );
        Expect.IsFalse(
            LongEventHandler_ExecuteToExecuteWhenFinished_Patches.HandsTheQueueToTheTail(
                0,
                LoadingStage.Finished,
                timingTheTail: true,
                onMainThread: true
            )
        );
        Expect.IsFalse(
            LongEventHandler_ExecuteToExecuteWhenFinished_Patches.HandsTheQueueToTheTail(
                1,
                LoadingStage.GarbageCollection,
                timingTheTail: true,
                onMainThread: true
            )
        );
        Expect.IsFalse(
            LongEventHandler_ExecuteToExecuteWhenFinished_Patches.HandsTheQueueToTheTail(
                1,
                LoadingStage.Finished,
                timingTheTail: false,
                onMainThread: true
            )
        );
    }

    // Finding an action's owner walks its closure by reflection, for each of the tens of
    // thousands of actions a large mod list queues. With tracking off at startup nothing used
    // the result, and the walk ran anyway.
    [Test]
    public static void AnUntimedActionNeitherLooksItsOwnerUpNorRecordsTime()
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            Test.Skip(TestStartup.TrackingOff);
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
            TestStartup.Forget(info!.Profiler, Category);
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
}
