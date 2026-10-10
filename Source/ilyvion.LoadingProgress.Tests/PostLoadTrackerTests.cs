using System.Text;
using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

// Some of these tests wait through the startup's tail, while other mods' post-load events run.
[TestFixture(TestType.MainMenu)]
[WarningsAllowed(TestStartup.OtherModsWarnings)]
internal sealed class PostLoadTrackerTests
{
    // A quicktest asks for the game's scene while the interface initializes, and the scene
    // loads before the next frame. The startup used to end only on that frame, so an event
    // started in between was timed as the startup's and took in the scene's load.
    [Test]
    public static void AQuicktestEndsTheStartupWhenItAsksForTheGamesScene() =>
        Expect.IsTrue(
            PostLoadTracker.HasLeftForAGame(ProgramState.Entry, quickStarted: true, null)
        );

    // A save loaded at startup is an event that loads the game's scene, and it runs while the
    // program is still at its entry state. It used to be timed as an event after loading, and
    // counted in the loading time.
    [Test]
    public static void AnEventThatLoadsTheGamesSceneEndsTheStartup() =>
        Expect.IsTrue(
            PostLoadTracker.HasLeftForAGame(
                ProgramState.Entry,
                quickStarted: false,
                GenScene.PlaySceneName
            )
        );

    [Test]
    public static void AGameThatHasStartedEndsTheStartup() =>
        Expect.IsTrue(
            PostLoadTracker.HasLeftForAGame(ProgramState.MapInitializing, quickStarted: false, null)
        );

    [Test]
    public static void TheEntryStateWithNoGameAskedForIsStillOnTheWayToTheMenu() =>
        Expect.IsFalse(
            PostLoadTracker.HasLeftForAGame(ProgramState.Entry, quickStarted: false, null)
        );

    // A mod that keeps a long event queued on the menu used to keep the loading window over it
    // for good. Past MaxTailMs of active time, the startup ends without a time to the menu.
    [Test]
    public static void AMenuThatNeverSettlesEndsTheWaitAtTheLimit()
    {
        Expect.IsFalse(
            PostLoadTracker.HasWaitedTooLong(1000f, 1000f + PostLoadTracker.MaxTailMs - 1f)
        );
        Expect.IsTrue(PostLoadTracker.HasWaitedTooLong(1000f, 1000f + PostLoadTracker.MaxTailMs));
    }

    [Test]
    public static void NothingHasWaitedBeforeTheWaitBegins() =>
        Expect.IsFalse(PostLoadTracker.HasWaitedTooLong(-1f, PostLoadTracker.MaxTailMs * 2f));

    [Test]
    public static void TheFirstIdleFrameNeverSettlesTheMenu() =>
        Expect.IsFalse(PostLoadTracker.IsMenuSettled(-1f, 100000f, 1));

    [Test]
    public static void TwoCloseIdleFramesSettleTheMenu() =>
        Expect.IsTrue(PostLoadTracker.IsMenuSettled(100000f, 100016f, 2));

    // A mod building its state on the menu's first frame stalls the main thread for seconds
    // with nothing queued; the frame after that stall must not count, or the stall is left
    // out of the time to the menu.
    [Test]
    public static void AnIdleFrameAfterAStallDoesNotSettleTheMenu() =>
        Expect.IsFalse(PostLoadTracker.IsMenuSettled(100000f, 107700f, 2));

    [Test]
    public static void TheFrameAfterTheStalledOneSettlesTheMenu() =>
        Expect.IsTrue(PostLoadTracker.IsMenuSettled(107700f, 107716f, 3));

    [Test]
    public static void FramesAQuarterOfASecondApartAreNotClose()
    {
        Expect.IsTrue(PostLoadTracker.IsMenuSettled(1000f, 1249.9f, 2));
        Expect.IsFalse(PostLoadTracker.IsMenuSettled(1000f, 1250f, 2));
    }

    // A menu that never draws two frames close together, below four frames a second, would
    // otherwise keep the startup open until the player left the menu.
    [Test]
    public static void ASlowMenuSettlesAfterFiveIdleFrames()
    {
        Expect.IsFalse(PostLoadTracker.IsMenuSettled(1000f, 1400f, 4));
        Expect.IsTrue(PostLoadTracker.IsMenuSettled(1000f, 1400f, 5));
    }

    // A player who switched to another window while the game loaded: the game stopped between
    // two frames until they came back, and that wait is theirs, not the startup's.
    [Test]
    public static void AWaitInTheBackgroundIsAPause() =>
        Expect.AreApproximatelyEqual(
            600000f,
            PostLoadTracker.PauseIn(1000f, 601000f, unfocusedSinceFrameEnd: true, false)
        );

    [Test]
    public static void AWaitWithTheGameInFrontIsNoPause() =>
        Expect.AreApproximatelyEqual(
            0f,
            PostLoadTracker.PauseIn(1000f, 601000f, unfocusedSinceFrameEnd: false, false)
        );

    // With 'Run in background' on the game never stops, so a long wait is something else.
    [Test]
    public static void AGameThatRunsInTheBackgroundIsNeverPaused() =>
        Expect.AreApproximatelyEqual(
            0f,
            PostLoadTracker.PauseIn(1000f, 601000f, unfocusedSinceFrameEnd: true, true)
        );

    [Test]
    public static void WatchingFocusThatSubscribesSaysSo() =>
        Expect.IsTrue(PostLoadTracker.TryWatchFocus(() => { }));

    // A focus event the engine doesn't have fails when the subscribing method is called; the
    // wait after loading goes on without it.
    [Test]
    [WarningsAllowed("Could not watch for the game going into the background")]
    public static void WatchingFocusThatFailsIsLoggedAndGoesOn() =>
        Expect.IsFalse(
            PostLoadTracker.TryWatchFocus(() =>
                throw new MissingMethodException("UnityEngine.Application", "add_focusChanged")
            )
        );

    [Test]
    public static void AnOrdinaryFrameIsNoPause() =>
        Expect.AreApproximatelyEqual(
            0f,
            PostLoadTracker.PauseIn(1000f, 1016f, unfocusedSinceFrameEnd: true, false)
        );

    [Test]
    public static void NothingIsPausedBeforeAFrameHasEnded() =>
        Expect.AreApproximatelyEqual(
            0f,
            PostLoadTracker.PauseIn(-1f, 601000f, unfocusedSinceFrameEnd: true, false)
        );

    // A synchronous long event runs inside LongEventsUpdate, so the pause is measured in the
    // tracker's prefix on it, where the frame's long events begin, and an event that runs for
    // seconds in the frame after a pause is not taken off with it.
    [Test]
    public static void ThePauseIsMeasuredBeforeTheFramesLongEvents()
    {
        var patches = Harmony.GetPatchInfo(
            AccessTools.Method(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsUpdate))
        );

        Expect.IsNotNull(patches);
        Expect.IsTrue(
            patches.Prefixes.Any(patch =>
                patch.PatchMethod.DeclaringType == typeof(LongEventHandler_LongEventsUpdate_Patches)
            )
        );
    }

    // An event's category used to open in the frame it was picked from the queue, the frame
    // before its work ran, so that frame's drawing of the loading window and the start of the
    // next were credited to the event's mod. Each kind of event is timed where the engine runs
    // its work.
    [Test]
    public static void EachKindOfEventIsTimedWhereItsWorkRuns()
    {
        string[] methods =
        [
            nameof(LongEventHandler.UpdateCurrentSynchronousEvent),
            nameof(LongEventHandler.UpdateCurrentAsynchronousEvent),
            nameof(LongEventHandler.UpdateCurrentEnumeratorEvent),
        ];
        foreach (var method in methods)
        {
            var patches = Harmony.GetPatchInfo(
                AccessTools.Method(typeof(LongEventHandler), method)
            );

            Expect.IsNotNull(patches);
            Expect.IsTrue(
                patches.Prefixes.Any(patch =>
                    patch.PatchMethod.DeclaringType
                    == typeof(LongEventHandler_UpdateCurrentEvent_Patches)
                )
            );
            Expect.IsTrue(
                patches.Postfixes.Any(patch =>
                    patch.PatchMethod.DeclaringType
                    == typeof(LongEventHandler_UpdateCurrentEvent_Patches)
                )
            );
        }
    }

    [Test]
    public static void EveryCallIntoAnEnumeratorEventRunsSomeOfItsSteps() =>
        Expect.IsTrue(
            PostLoadTracker.StartsTimingThisCall(
                isEnumerator: true,
                isAsynchronous: false,
                threadStarted: false,
                waitingToBeDisplayed: true
            )
        );

    // A synchronous event with text waits a frame for its text to be drawn; that frame is
    // the loading window's, not the event's.
    [Test]
    public static void ASynchronousEventWaitingToBeDisplayedIsNotTimed()
    {
        Expect.IsFalse(
            PostLoadTracker.StartsTimingThisCall(
                isEnumerator: false,
                isAsynchronous: false,
                threadStarted: false,
                waitingToBeDisplayed: true
            )
        );
        Expect.IsTrue(
            PostLoadTracker.StartsTimingThisCall(
                isEnumerator: false,
                isAsynchronous: false,
                threadStarted: false,
                waitingToBeDisplayed: false
            )
        );
    }

    [Test]
    public static void AnAsynchronousEventIsTimedFromItsThreadsStart()
    {
        Expect.IsTrue(
            PostLoadTracker.StartsTimingThisCall(
                isEnumerator: false,
                isAsynchronous: true,
                threadStarted: false,
                waitingToBeDisplayed: false
            )
        );
        Expect.IsFalse(
            PostLoadTracker.StartsTimingThisCall(
                isEnumerator: false,
                isAsynchronous: true,
                threadStarted: true,
                waitingToBeDisplayed: false
            )
        );
    }

    [Test]
    public static void AnAsynchronousEventStaysTimedUntilTheEngineFinishesIt()
    {
        Expect.IsTrue(PostLoadTracker.StaysOpenAfterThisCall(isAsynchronous: true, true));
        Expect.IsFalse(PostLoadTracker.StaysOpenAfterThisCall(isAsynchronous: true, false));
    }

    // An enumerator event stays current across frames, but only its steps in each frame are
    // its own.
    [Test]
    public static void AnyOtherEventStopsBeingTimedWhenTheCallIntoItReturns()
    {
        Expect.IsFalse(PostLoadTracker.StaysOpenAfterThisCall(isAsynchronous: false, true));
        Expect.IsFalse(PostLoadTracker.StaysOpenAfterThisCall(isAsynchronous: false, false));
    }

    // With the initialization patches off in the settings, nothing used to make the main
    // thread the active one after loading, so a long event timed there went to the off-thread
    // figures, which the totals never see and a pause cannot be taken off.
    [Test]
    public static IEnumerator AnEventTimedAfterLoadingCountsOnTheMainThread()
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

        var category = $"{PostLoadTracker.Category}|{nameof(PostLoadTrackerTests)}.Event";
        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        var profiler = startupImpact.BaseGameProfiler;

        // As though the loading thread were still the active one.
        Task.Run(startupImpact.UpdateActiveThreadId).Wait();
        try
        {
            Expect.IsFalse(startupImpact.IsActiveThread());

            PostLoadTracker.StartTiming(null, isBaseGame: true, category);
            StartupImpactProfilerUtil.StopBaseGameProfiler(category);

            Expect.IsTrue(startupImpact.IsActiveThread());
            Expect.IsTrue(profiler.Metrics.ContainsKey(category));
        }
        finally
        {
            startupImpact.UpdateActiveThreadId();
            TestStartup.Forget(profiler, category);
        }
    }

    // An asynchronous event open through a pause is stopped with the pause taken back off, on
    // the timer it was started on. A pause longer than the event leaves it nothing.
    [Test]
    public static IEnumerator APauseIsTakenOffTheTimerTheEventRanOn()
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

        var category = $"{PostLoadTracker.Category}|{nameof(PostLoadTrackerTests)}.Paused";
        var profiler = LoadingProgressMod.instance.StartupImpact.BaseGameProfiler;
        try
        {
            PostLoadTracker.StartTiming(null, isBaseGame: true, category);
            StartupImpactProfilerUtil.Stop(null, isBaseGame: true, category, discountMs: 60000f);

            Expect.IsTrue(profiler.Metrics.TryGetValue(category, out var ms));
            Expect.AreApproximatelyEqual(0f, ms);
        }
        finally
        {
            TestStartup.Forget(profiler, category);
        }
    }

    // A start that throws while recording the category open below it has opened nothing. The
    // event used to keep its category anyway, so stopping it stopped the open category in its
    // place, and the test's own stop of that category then found it gone. Either stop logs an
    // error, which fails the test.
    [Test]
    public static IEnumerator AnEventWhoseTimingFailsToStartHasNothingStopped()
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

        const string Open = "LoadingProgress.Tests.PostLoadTrackerTests.Open";
        var profiler = LoadingProgressMod.instance.StartupImpact.BaseGameProfiler;
        // Never run: only the code it names decides whose timer the event is on.
        var queuedEvent = new LongEventHandler.QueuedLongEvent
        {
            eventAction = Log.ResetMessageCount,
            eventTextKey = $"{nameof(PostLoadTrackerTests)}.Failed",
        };
        var category = $"{PostLoadTracker.Category}|{PostLoadTracker.Describe(queuedEvent)}";
        StartupImpactProfilerUtil.StartBaseGameProfiler(Open);
        var openStopped = false;
        try
        {
            RecordingFailure.During(() =>
                _ = Expect.Throws<InvalidOperationException>(() =>
                    PostLoadTracker.StartCurrent(queuedEvent)
                )
            );
            PostLoadTracker.StopCurrent(0f);
            StartupImpactProfilerUtil.StopBaseGameProfiler(Open);
            openStopped = true;

            Expect.IsTrue(profiler.Metrics.ContainsKey(Open));
            Expect.IsFalse(profiler.Metrics.ContainsKey(category));
        }
        finally
        {
            // Nothing else is current once the startup has ended.
            PostLoadTracker.StopCurrent(0f);
            if (!openStopped)
            {
                StartupImpactProfilerUtil.StopBaseGameProfiler(Open);
            }
            TestStartup.Forget(profiler, Open);
            TestStartup.Forget(profiler, category);
        }
    }

    [Test]
    public static void AnEventIsNamedByItsTextWhenTheKeyTranslates() =>
        Expect.AreEqual(
            "LoadingProgress.Title".Translate().ToString(),
            PostLoadTracker.Describe("LoadingProgress.Title", null, null)
        );

    [Test]
    public static void AKeyThatDoesNotTranslateIsShownAsItIs() =>
        Expect.AreEqual(
            "NoSuchKey.ForThisTest",
            PostLoadTracker.Describe("NoSuchKey.ForThisTest", null, null)
        );

    // An event with no text used to be named after the compiler's closure class and method,
    // which tell a player nothing.
    [Test]
    public static void ALambdaIsNamedAfterTheMethodItWasWrittenIn()
    {
        Action action = static () => { };

        Expect.AreEqual(
            $"{typeof(PostLoadTrackerTests).FullName}.{nameof(ALambdaIsNamedAfterTheMethodItWasWrittenIn)}",
            PostLoadTracker.Describe(null, action, null)
        );
    }

    [Test]
    public static void AnIteratorIsNamedAfterItsMethod() =>
        Expect.AreEqual(
            $"{typeof(PostLoadTrackerTests).FullName}.{nameof(Steps)}",
            PostLoadTracker.Describe(null, null, Steps())
        );

    [Test]
    public static void AnEventIsOwnedByTheModWhoseCodeItRuns()
    {
        Action action = static () => { };

        var owner = PostLoadTracker.OwnerOf(action, null, out var isBaseGame);

        Expect.IsNotNull(owner);
        Expect.ReferencesAreEqual(
            Utilities.FindModByAssembly(typeof(PostLoadTrackerTests).Assembly),
            owner
        );
        Expect.IsFalse(isBaseGame);
    }

    [Test]
    public static void TheEnginesOwnEventBelongsToTheBaseGame()
    {
        Action action = LongEventHandler.ClearQueuedEvents;

        var owner = PostLoadTracker.OwnerOf(action, null, out var isBaseGame);

        Expect.IsNull(owner);
        Expect.IsTrue(isBaseGame);
    }

    // An event from code no mod loaded used to go under the base game, while a deferred
    // action from the same code went untimed. Both now follow the deferred actions' rule.
    [Test]
    public static void AnEventFromCodeNoModLoadedBelongsToNeither()
    {
        var holder = new StringBuilder();
        Func<string> action = holder.ToString;
        IEnumerator enumerator = new List<int>().GetEnumerator();

        var actionOwner = PostLoadTracker.OwnerOf(action, null, out var actionIsBaseGame);
        var enumeratorOwner = PostLoadTracker.OwnerOf(
            null,
            enumerator,
            out var enumeratorIsBaseGame
        );

        Expect.IsNull(actionOwner);
        Expect.IsFalse(actionIsBaseGame);
        Expect.IsNull(enumeratorOwner);
        Expect.IsFalse(enumeratorIsBaseGame);
    }

    private static IEnumerator Steps()
    {
        yield break;
    }
}
