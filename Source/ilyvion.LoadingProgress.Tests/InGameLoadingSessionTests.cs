using System.Xml;
using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class InGameLoadingSessionTests
{
    [Test]
    public static void DetermineKindMapsGeneratingWorldToWorldGeneration()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingWorld",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.WorldGeneration, kind);
    }

    [Test]
    public static void DetermineKindMapsGeneratingPlanetToPlanetRegeneration()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingPlanet",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.PlanetRegeneration, kind);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapToNewGameMapGenerationWhenProgramStateIsEntry()
    {
        // The pre-scene-load "GeneratingMap" event (PageUtility.InitGameStart) has no
        // levelToLoad of its own worth relying on here; ProgramState is still Entry at that
        // point.
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMap",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.NewGameMapGeneration, kind);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapToNewGameMapGenerationWhenLevelToLoadIsPlay()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMap",
            "Play",
            ProgramState.MapInitializing,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.NewGameMapGeneration, kind);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapToNewGameMapGenerationWhenInPlaySceneWithNoMaps()
    {
        // The post-scene-load "GeneratingMap" event (Root_Play.Start -> Game.InitNewGame) has
        // no levelToLoad and ProgramState is already MapInitializing by then; it's recognized
        // as the same kind via the "in the Play scene with no maps yet" condition instead.
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMap",
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.NewGameMapGeneration, kind);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapToEncounterMapGenerationWhenInPlayAsyncWithExistingMaps()
    {
        // Settle/SetupCamp/dev gizmos: an in-play "GeneratingMap" event with maps already
        // present, running asynchronously (live progress is possible), must be recognized as
        // its own kind rather than misclassified as new-game map generation.
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMap",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.EncounterMapGeneration, kind);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapToEncounterMapGenerationStaticWhenInPlaySyncWithExistingMaps()
    {
        // Synchronous in-play map generation (gravship landings, the new-colony quest, the dev
        // "generate map here" gizmo) runs in one Update() with no repaint until it finishes, so
        // live progress is impossible and it must not be picked up as EncounterMapGeneration;
        // only the static single-frame kind fits.
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMap",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.EncounterMapGenerationStatic, kind);
    }

    [Test]
    public static void DetermineKindMapsGeneratingMapForNewEncounterToEncounterMapGenerationStatic()
    {
        // Every in-play encounter map (visit site, settlement attack, peace talks, escape ship,
        // transporters, ambush, caravan meeting/demand) queues this key and always runs
        // synchronously; it must be recognized as the static kind regardless of doAsynchronously.
        var kind = InGameLoadingSession.DetermineKind(
            "GeneratingMapForNewEncounter",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.EncounterMapGenerationStatic, kind);
    }

    [Test]
    public static void DetermineKindMapsSpawningColonistsToEncounterMapGeneration()
    {
        // Settle/SetupCamp's second event (CaravanEnterMapUtility.Enter); always recognized as
        // EncounterMapGeneration regardless of context, the same way "GeneratingWorld" always
        // maps to WorldGeneration.
        var kind = InGameLoadingSession.DetermineKind(
            "SpawningColonists",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 2,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.EncounterMapGeneration, kind);
    }

    [Test]
    public static void DetermineKindMapsLoadingLongEventToSaveLoadingWhenLevelToLoadIsPlay()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "LoadingLongEvent",
            "Play",
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.SaveLoading, kind);
    }

    [Test]
    public static void DetermineKindMapsLoadingLongEventToSaveLoadingWhenGameToLoadIsPending()
    {
        // GameDataSaveLoader.LoadGame's scene-load event carries levelToLoad "Play", but the
        // event queued afterwards from Root_Play.Start (which actually reads the save file) has
        // no levelToLoad of its own. It must still be recognized as the same session via
        // GameInitData still holding the save name the first event stashed there.
        var kind = InGameLoadingSession.DetermineKind(
            "LoadingLongEvent",
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: true,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.SaveLoading, kind);
    }

    [Test]
    public static void DetermineKindMapsLoadingLongEventToNoneWhenLevelToLoadIsEntry()
    {
        // GenScene.GoToMainMenu queues "LoadingLongEvent" with levelToLoad "Entry"; this must
        // not be picked up as a save-loading session.
        var kind = InGameLoadingSession.DetermineKind(
            "LoadingLongEvent",
            "Entry",
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.None, kind);
    }

    [Test]
    public static void DetermineKindMapsLoadingLongEventToNoneWhenNotInPlaySceneAndNoGameToLoad()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "LoadingLongEvent",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.None, kind);
    }

    [Test]
    public static void DetermineKindMapsUnrecognizedKeyToNone()
    {
        var kind = InGameLoadingSession.DetermineKind(
            "Autosaver",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.None, kind);
    }

    [Test]
    public static void AdvanceSessionStartsNewSessionWhenAWhitelistedEventBecomesCurrent()
    {
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.None,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "GeneratingWorld",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.WorldGeneration, kind);
    }

    [Test]
    public static void AdvanceSessionKeepsActiveKindWhenTheSameEventIsStillRunning()
    {
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.WorldGeneration,
            hasCurrentEvent: true,
            eventChanged: false,
            queueEmpty: false,
            resetSignal: false,
            "GenStep - Terrain",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.WorldGeneration, kind);
    }

    [Test]
    public static void AdvanceSessionKeepsActiveKindWhenTheDeferredRedirectEventBecomesCurrent()
    {
        // InGameDeferredActionReplacement's redirected event is a brand new QueuedLongEvent
        // object (eventChanged: true) carrying a key DetermineKind doesn't recognize; it must
        // still continue whatever kind was already active instead of ending the session.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.SaveLoading,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            InGameLoadingSession.DeferredRedirectEventTextKey,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.SaveLoading, kind);
    }

    [Test]
    public static void AdvanceSessionKeepsSessionAliveWhileWaitingBetweenChainedEvents()
    {
        // currentEvent is briefly null (e.g. mid Unity scene-load) but the queue still has the
        // next event of the same chain waiting; the session must not drop.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.NewGameMapGeneration,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            null,
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.NewGameMapGeneration, kind);
    }

    [Test]
    public static void AdvanceSessionEndsWhenQueueIsEmptyAndNoCurrentEvent()
    {
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.SaveLoading,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: true,
            resetSignal: false,
            null,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.None, kind);
    }

    [Test]
    public static void AdvanceSessionEndsImmediatelyOnResetSignalRegardlessOfOtherState()
    {
        // Simulates an error mid-load (ClearQueuedEvents / Scribe.ForceStop); must end the
        // session even though an event is still technically current.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.WorldGeneration,
            hasCurrentEvent: true,
            eventChanged: false,
            queueEmpty: false,
            resetSignal: true,
            "GenerateWorld",
            null,
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.None, kind);
    }

    [Test]
    public static void AdvanceSessionEndsOnReturnToMenuEvent()
    {
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.SaveLoading,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "LoadingLongEvent",
            "Entry",
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.None, kind);
    }

    [Test]
    public static void AdvanceSessionNewGameMapGenerationSpansTheTwoEventChainAcrossASceneLoad()
    {
        // Full fixture sequence for new-game map generation: PrepForMapGen's "GeneratingMap"
        // event, then a gap while the "Play" scene loads, then Game.InitNewGame's distinct
        // "GeneratingMap" event object, then the session ending once the queue drains.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.None,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "GeneratingMap",
            "Play",
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.NewGameMapGeneration, kind);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            null,
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.NewGameMapGeneration, kind);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "GeneratingMap",
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.NewGameMapGeneration, kind);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: true,
            resetSignal: false,
            null,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.None, kind);
    }

    [Test]
    public static void AdvanceSessionEncounterMapGenerationSpansTheGeneratingMapAndSpawningColonistsEventPair()
    {
        // Settle: SettleInEmptyTileUtility.Settle queues an async "GeneratingMap" event (reusing
        // MapGenerator.GenerateMap, same as new-game map generation, but with existing maps
        // already present) followed, after that event's worker thread finishes, by a second,
        // distinct "SpawningColonists" QueuedLongEvent object (CaravanEnterMapUtility.Enter);
        // it must be recognized as a continuation of the same session, not an unrelated event.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.None,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "GeneratingMap",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.EncounterMapGeneration, kind);

        // currentEvent is briefly null between the two events (the first event's worker thread
        // has finished but the second hasn't been dequeued yet); the session must not drop.
        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            null,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.EncounterMapGeneration, kind);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "SpawningColonists",
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 2,
            gameToLoadPending: false,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.EncounterMapGeneration, kind);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: true,
            resetSignal: false,
            null,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 2,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.None, kind);
    }

    [Test]
    public static void AdvanceSessionSaveLoadingSpansTheTwoEventChainAcrossASceneLoad()
    {
        // Regression coverage for the bug where loading a save only showed the mod's window for
        // the very first ("Play"-scene-load) event, then silently fell back to vanilla's display
        // for the rest of the load. GameDataSaveLoader.LoadGame's scene-load event ("Play") is
        // followed, after the scene finishes loading, by a second, distinct QueuedLongEvent
        // object (queued from Root_Play.Start) that has the same eventTextKey but no
        // levelToLoad of its own; it must still be recognized as a continuation of the same
        // save-loading session via gameToLoadPending.
        var kind = InGameLoadingSession.AdvanceSession(
            InGameSessionKind.None,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "LoadingLongEvent",
            "Play",
            ProgramState.Entry,
            inPlayScene: false,
            mapCount: 0,
            gameToLoadPending: true,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.SaveLoading, kind);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: false,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            null,
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: true,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.SaveLoading, kind);

        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            "LoadingLongEvent",
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: true,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.SaveLoading, kind);

        // The event text changes several times within this same event (world -> map -> init ->
        // spawn) via SetCurrentEventText, but the event object itself never changes, so the
        // session must keep its kind with no reclassification.
        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: true,
            eventChanged: false,
            queueEmpty: false,
            resetSignal: false,
            "LoadingLongEvent",
            null,
            ProgramState.MapInitializing,
            inPlayScene: true,
            mapCount: 0,
            gameToLoadPending: true,
            doAsynchronously: true
        );
        Expect.AreEqual(InGameSessionKind.SaveLoading, kind);

        // The final, untextkeyed screen-fade event ends the session.
        kind = InGameLoadingSession.AdvanceSession(
            kind,
            hasCurrentEvent: true,
            eventChanged: true,
            queueEmpty: false,
            resetSignal: false,
            null,
            null,
            ProgramState.Playing,
            inPlayScene: true,
            mapCount: 1,
            gameToLoadPending: false,
            doAsynchronously: false
        );
        Expect.AreEqual(InGameSessionKind.None, kind);
    }

    [Test]
    public static void DeterminePhaseFromLabelAdvancesNewGameMapGenerationPhasesInOrder()
    {
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.NewGameMapGeneration,
            InGameSessionPhase.NewGameMapGeneration_SetUp,
            "Generate contents into map"
        );
        Expect.AreEqual(InGameSessionPhase.NewGameMapGeneration_GenSteps, phase);

        phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.NewGameMapGeneration,
            phase,
            "Finalize map init"
        );
        Expect.AreEqual(InGameSessionPhase.NewGameMapGeneration_Finalize, phase);

        phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.NewGameMapGeneration,
            phase,
            "MapComponent.MapGenerated()"
        );
        Expect.AreEqual(InGameSessionPhase.NewGameMapGeneration_PostInit, phase);
    }

    [Test]
    public static void DeterminePhaseFromLabelAlsoAdvancesToPostInitOnMapGeneratorPostInitLabel()
    {
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.NewGameMapGeneration,
            InGameSessionPhase.NewGameMapGeneration_Finalize,
            "Map generator post init"
        );
        Expect.AreEqual(InGameSessionPhase.NewGameMapGeneration_PostInit, phase);
    }

    [Test]
    public static void DeterminePhaseFromLabelKeepsNewGameMapGenerationPhaseForUnrelatedLabels()
    {
        // The per-step "GenStep - <def>" labels advance the inner progress bar (via
        // OnProfilerLabel), not the outer phase; the phase only changes on the boundary labels.
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.NewGameMapGeneration,
            InGameSessionPhase.NewGameMapGeneration_GenSteps,
            "GenStep - ElevationFertility"
        );
        Expect.AreEqual(InGameSessionPhase.NewGameMapGeneration_GenSteps, phase);
    }

    [Test]
    public static void DeterminePhaseFromLabelAdvancesEncounterMapGenerationPhasesInOrder()
    {
        // Settle/SetupCamp reuse MapGenerator.GenerateMap, so the label sequence is identical to
        // new-game map generation; only the target phase enum differs.
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.EncounterMapGeneration,
            InGameSessionPhase.EncounterMapGeneration_SetUp,
            "Generate contents into map"
        );
        Expect.AreEqual(InGameSessionPhase.EncounterMapGeneration_GenSteps, phase);

        phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.EncounterMapGeneration,
            phase,
            "Finalize map init"
        );
        Expect.AreEqual(InGameSessionPhase.EncounterMapGeneration_Finalize, phase);

        phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.EncounterMapGeneration,
            phase,
            "MapComponent.MapGenerated()"
        );
        Expect.AreEqual(InGameSessionPhase.EncounterMapGeneration_PostInit, phase);
    }

    [Test]
    public static void ShouldEnterSpawningColonistsPhaseIsTrueWhenTheSpawningColonistsEventBecomesCurrent() =>
        Expect.IsTrue(
            InGameLoadingSession.ShouldEnterSpawningColonistsPhase(
                InGameSessionKind.EncounterMapGeneration,
                eventChanged: true,
                "SpawningColonists"
            )
        );

    [Test]
    public static void ShouldEnterSpawningColonistsPhaseIsFalseForOtherKinds() =>
        // NewGameMapGeneration never chains into a "SpawningColonists" event; a modded call
        // site emitting one while a new-game session is active must not be picked up.
        Expect.IsFalse(
            InGameLoadingSession.ShouldEnterSpawningColonistsPhase(
                InGameSessionKind.NewGameMapGeneration,
                eventChanged: true,
                "SpawningColonists"
            )
        );

    [Test]
    public static void ShouldEnterSpawningColonistsPhaseIsFalseWhenTheEventDidNotChange() =>
        // Guards against re-entering the phase (and resetting its progress) on every frame the
        // SpawningColonists event stays current, not just the one frame it becomes current.
        Expect.IsFalse(
            InGameLoadingSession.ShouldEnterSpawningColonistsPhase(
                InGameSessionKind.EncounterMapGeneration,
                eventChanged: false,
                "SpawningColonists"
            )
        );

    [Test]
    public static void ShouldEnterSpawningColonistsPhaseIsFalseForUnrelatedKeys() =>
        Expect.IsFalse(
            InGameLoadingSession.ShouldEnterSpawningColonistsPhase(
                InGameSessionKind.EncounterMapGeneration,
                eventChanged: true,
                "GeneratingMap"
            )
        );

    [Test]
    public static void DeterminePhaseFromLabelAdvancesSaveLoadingToFinishingOnGameFinalizeInitLabel()
    {
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.SaveLoading,
            InGameSessionPhase.SaveLoading_Spawning,
            "Game.FinalizeInit"
        );
        Expect.AreEqual(InGameSessionPhase.SaveLoading_Finishing, phase);
    }

    [Test]
    public static void DeterminePhaseFromLabelKeepsSaveLoadingPhaseForUnrelatedLabels()
    {
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.SaveLoading,
            InGameSessionPhase.SaveLoading_Spawning,
            "Spawn everything into the map"
        );
        Expect.AreEqual(InGameSessionPhase.SaveLoading_Spawning, phase);
    }

    [Test]
    public static void DeterminePhaseFromLabelIsANoOpForWorldGeneration()
    {
        // World generation's only phase transition (SetupSteps -> LayerSteps) comes from the
        // GeneratePlanetLayer prefix, which has the layer itself available; no label alone can
        // drive it, since the "WorldGen - <type name>" label isn't even human-readable.
        var phase = InGameLoadingSession.DeterminePhaseFromLabel(
            InGameSessionKind.WorldGeneration,
            InGameSessionPhase.WorldGeneration_SetupSteps,
            "WorldGenStep - Tiles"
        );
        Expect.AreEqual(InGameSessionPhase.WorldGeneration_SetupSteps, phase);
    }

    [Test]
    public static void DeterminePhaseFromLabelIsANoOpForPlanetRegeneration() =>
        // Planet regeneration has only the one phase and emits no DeepProfiler labels at all
        // (per-layer progress instead comes from the WorldDrawLayerBase.Regenerate prefix).
        Expect.AreEqual(
            InGameSessionPhase.PlanetRegeneration_RegeneratingLayers,
            InGameLoadingSession.DeterminePhaseFromLabel(
                InGameSessionKind.PlanetRegeneration,
                InGameSessionPhase.PlanetRegeneration_RegeneratingLayers,
                "GenStep - Terrain"
            )
        );

    [Test]
    public static void DeterminePhaseFromLabelIsANoOpForEncounterMapGenerationStatic() =>
        // The static kind's one painted frame is fixed before the synchronous action (and any
        // labels it would emit) even runs, so it has only the one phase and ignores labels.
        Expect.AreEqual(
            InGameSessionPhase.EncounterMapGenerationStatic_Generating,
            InGameLoadingSession.DeterminePhaseFromLabel(
                InGameSessionKind.EncounterMapGenerationStatic,
                InGameSessionPhase.EncounterMapGenerationStatic_Generating,
                "GenStep - ElevationFertility"
            )
        );

    [Test]
    public static void DetermineSaveLoadingPhaseFromEventTextCallOrderMapsCallsInFixedOrder()
    {
        Expect.AreEqual(
            InGameSessionPhase.SaveLoading_World,
            InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(1)
        );
        Expect.AreEqual(
            InGameSessionPhase.SaveLoading_Maps,
            InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(2)
        );
        Expect.AreEqual(
            InGameSessionPhase.SaveLoading_Initializing,
            InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(3)
        );
        Expect.AreEqual(
            InGameSessionPhase.SaveLoading_Spawning,
            InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(4)
        );
    }

    [Test]
    public static void DetermineSaveLoadingPhaseFromEventTextCallOrderClampsCallsPastTheFourth() =>
        // Defensive: SetCurrentEventText is only ever called 4 times by vanilla, but a modded
        // caller (or a future game version) calling it a 5th time shouldn't invent a new phase.
        Expect.AreEqual(
            InGameSessionPhase.SaveLoading_Spawning,
            InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(5)
        );

    [Test]
    public static void DetermineSaveLoadingPhaseFromEventTextCallOrderDefaultsToReadingFile() =>
        Expect.AreEqual(
            InGameSessionPhase.SaveLoading_ReadingFile,
            InGameLoadingSession.DetermineSaveLoadingPhaseFromEventTextCallOrder(0)
        );

    [Test]
    public static void IsSaveLoadingSubProgressResetLabelIsTrueForBothInitializingLoopLabels()
    {
        Expect.IsTrue(
            InGameLoadingSession.IsSaveLoadingSubProgressResetLabel("ResolveAllCrossReferences()")
        );
        Expect.IsTrue(
            InGameLoadingSession.IsSaveLoadingSubProgressResetLabel("DoAllPostLoadInits()")
        );
    }

    [Test]
    public static void IsSaveLoadingSubProgressResetLabelIsTrueForThingPostMapInit() =>
        Expect.IsTrue(
            InGameLoadingSession.IsSaveLoadingSubProgressResetLabel("Thing.PostMapInit()")
        );

    [Test]
    public static void IsSaveLoadingSubProgressResetLabelIsFalseForUnrelatedLabels() =>
        Expect.IsFalse(
            InGameLoadingSession.IsSaveLoadingSubProgressResetLabel("Spawn everything into the map")
        );

    [Test]
    public static void DetermineInitializingSubPhaseTotalUsesCrossReferencingExposablesCountForResolveAllCrossReferences() =>
        Expect.AreEqual(
            42,
            InGameLoadingSession.DetermineInitializingSubPhaseTotal(
                "ResolveAllCrossReferences()",
                crossReferencingExposablesCount: 42,
                saveablesToPostLoadCount: 7
            )
        );

    [Test]
    public static void DetermineInitializingSubPhaseTotalUsesSaveablesToPostLoadCountForDoAllPostLoadInits() =>
        Expect.AreEqual(
            7,
            InGameLoadingSession.DetermineInitializingSubPhaseTotal(
                "DoAllPostLoadInits()",
                crossReferencingExposablesCount: 42,
                saveablesToPostLoadCount: 7
            )
        );

    [Test]
    public static void DetermineInitializingSubPhaseTotalIsZeroForThingPostMapInit() =>
        // The Thing.PostMapInit() loop's total isn't known upfront from any live collection count;
        // it's set later, from the first postfix call that has a Map instance to read
        // map.listerThings.AllThings.Count from.
        Expect.AreEqual(
            0,
            InGameLoadingSession.DetermineInitializingSubPhaseTotal(
                "Thing.PostMapInit()",
                crossReferencingExposablesCount: 42,
                saveablesToPostLoadCount: 7
            )
        );

    [Test]
    public static void DetermineThingPostMapInitPhaseMapsMapGenerationKindsToTheirOwnFinalizePhase()
    {
        Expect.AreEqual(
            InGameSessionPhase.NewGameMapGeneration_Finalize,
            InGameLoadingSession.DetermineThingPostMapInitPhase(
                InGameSessionKind.NewGameMapGeneration
            )
        );
        Expect.AreEqual(
            InGameSessionPhase.EncounterMapGeneration_Finalize,
            InGameLoadingSession.DetermineThingPostMapInitPhase(
                InGameSessionKind.EncounterMapGeneration
            )
        );
    }

    [Test]
    public static void DetermineThingPostMapInitPhaseMapsSaveLoadingToItsSpawningPhase() =>
        // SaveLoading has no dedicated Finalize phase: Map.FinalizeInit() (and so its
        // Thing.PostMapInit() loop) runs inside Spawning instead.
        Expect.AreEqual(
            InGameSessionPhase.SaveLoading_Spawning,
            InGameLoadingSession.DetermineThingPostMapInitPhase(InGameSessionKind.SaveLoading)
        );

    [Test]
    public static void DetermineThingPostMapInitPhaseIsNullForKindsThatNeverReachIt()
    {
        Expect.IsTrue(
            InGameLoadingSession.DetermineThingPostMapInitPhase(InGameSessionKind.WorldGeneration)
                == null
        );
        Expect.IsTrue(
            InGameLoadingSession.DetermineThingPostMapInitPhase(
                InGameSessionKind.PlanetRegeneration
            ) == null
        );
        Expect.IsTrue(
            InGameLoadingSession.DetermineThingPostMapInitPhase(
                InGameSessionKind.EncounterMapGenerationStatic
            ) == null
        );
        Expect.IsTrue(
            InGameLoadingSession.DetermineThingPostMapInitPhase(InGameSessionKind.None) == null
        );
    }

    [Test]
    public static void DetermineDeferredPhaseMapsEachChunkableKindToItsOwnDeferredPhase()
    {
        Expect.AreEqual(
            InGameSessionPhase.WorldGeneration_Deferred,
            InGameLoadingSession.DetermineDeferredPhase(InGameSessionKind.WorldGeneration)
        );
        Expect.AreEqual(
            InGameSessionPhase.NewGameMapGeneration_Deferred,
            InGameLoadingSession.DetermineDeferredPhase(InGameSessionKind.NewGameMapGeneration)
        );
        Expect.AreEqual(
            InGameSessionPhase.SaveLoading_Deferred,
            InGameLoadingSession.DetermineDeferredPhase(InGameSessionKind.SaveLoading)
        );
        Expect.AreEqual(
            InGameSessionPhase.EncounterMapGeneration_Deferred,
            InGameLoadingSession.DetermineDeferredPhase(InGameSessionKind.EncounterMapGeneration)
        );
    }

    [Test]
    public static void DetermineDeferredPhaseIsNullForKindsInGameDeferredActionReplacementNeverChunks()
    {
        // PlanetRegeneration already gets live per-layer progress from vanilla's own
        // enumerator-based long event, and EncounterMapGenerationStatic's one painted frame
        // precedes any of the generation it describes; neither goes through
        // InGameDeferredActionReplacement.
        Expect.IsTrue(
            InGameLoadingSession.DetermineDeferredPhase(InGameSessionKind.PlanetRegeneration)
                == null
        );
        Expect.IsTrue(
            InGameLoadingSession.DetermineDeferredPhase(
                InGameSessionKind.EncounterMapGenerationStatic
            ) == null
        );
        Expect.IsTrue(InGameLoadingSession.DetermineDeferredPhase(InGameSessionKind.None) == null);
    }

    [Test]
    public static void AdvanceProgressCurrentIncrementsBelowMax() =>
        Expect.AreEqual(4, InGameLoadingSession.AdvanceProgressCurrent(3, 5));

    // Regression coverage for §8 risk item 8: counts derived from approximate totals (e.g.
    // things spawned after load) must never let the inner bar exceed its own max.
    [Test]
    public static void AdvanceProgressCurrentClampsAtMax() =>
        Expect.AreEqual(5, InGameLoadingSession.AdvanceProgressCurrent(5, 5));

    [Test]
    public static void CountDirtyVisibleLayersCountsOnlyLayersThatAreBothDirtyAndVisible()
    {
        var count = InGameLoadingSession.CountDirtyVisibleLayers([
            (Dirty: true, Visible: true),
            (Dirty: true, Visible: false),
            (Dirty: false, Visible: true),
            (Dirty: false, Visible: false),
            (Dirty: true, Visible: true),
        ]);
        Expect.AreEqual(2, count);
    }

    [Test]
    public static void CountDirtyVisibleLayersReturnsZeroForAnEmptyLayerList() =>
        Expect.AreEqual(0, InGameLoadingSession.CountDirtyVisibleLayers([]));

    [Test]
    public static void StripKnownLabelPrefixStripsGenStepPrefix() =>
        Expect.AreEqual(
            "ElevationFertility",
            InGameLoadingSession.StripKnownLabelPrefix("GenStep - ElevationFertility")
        );

    [Test]
    public static void StripKnownLabelPrefixStripsWorldGenStepPrefix() =>
        Expect.AreEqual(
            "Tiles",
            InGameLoadingSession.StripKnownLabelPrefix("WorldGenStep - Tiles")
        );

    [Test]
    public static void StripKnownLabelPrefixLeavesOtherLabelsUnchanged() =>
        Expect.AreEqual(
            "Finalize map init",
            InGameLoadingSession.StripKnownLabelPrefix("Finalize map init")
        );

    [Test]
    public static void IsSuppressedWorldGenLayerLabelDetectsTheLayerBoundaryLabel() =>
        Expect.IsTrue(
            InGameLoadingSession.IsSuppressedWorldGenLayerLabel(
                "WorldGen - RimWorld.Planet.SurfaceLayer"
            )
        );

    // "WorldGenStep - Tiles" must not be mistaken for the "WorldGen - <type>" layer-boundary
    // label just because it shares a prefix; it's a legitimate, readable label on its own.
    [Test]
    public static void IsSuppressedWorldGenLayerLabelDoesNotMatchWorldGenStepLabels() =>
        Expect.IsFalse(InGameLoadingSession.IsSuppressedWorldGenLayerLabel("WorldGenStep - Tiles"));

    [Test]
    public static void CountThingsAcrossMapsSumsThingNodesFromEveryMap()
    {
        var doc = ParseXmlFixture(
            """
            <game>
                <maps>
                    <li>
                        <things>
                            <thing Class="Plant" />
                            <thing Class="Mineable" />
                        </things>
                    </li>
                    <li>
                        <things>
                            <thing Class="Building" />
                        </things>
                    </li>
                </maps>
            </game>
            """
        );

        Expect.AreEqual(3, InGameLoadingSession.CountThingsAcrossMaps(doc.DocumentElement));
    }

    [Test]
    public static void CountThingsAcrossMapsReturnsZeroWhenThereIsNoMapsNode()
    {
        var doc = ParseXmlFixture("<game></game>");

        Expect.AreEqual(0, InGameLoadingSession.CountThingsAcrossMaps(doc.DocumentElement));
    }

    [Test]
    public static void CountThingsAcrossMapsIgnoresMapsWithNoThingsNode()
    {
        var doc = ParseXmlFixture(
            """
            <game>
                <maps>
                    <li></li>
                    <li>
                        <things>
                            <thing Class="Building" />
                        </things>
                    </li>
                </maps>
            </game>
            """
        );

        Expect.AreEqual(1, InGameLoadingSession.CountThingsAcrossMaps(doc.DocumentElement));
    }

    // XmlDocument.LoadXml(string) resolves external entities by default (XXE risk); these
    // fixtures are hardcoded test data, not untrusted input, but XmlReader with DtdProcessing
    // disabled avoids relying on that distinction.
    private static XmlDocument ParseXmlFixture(string xml)
    {
        using var reader = XmlReader.Create(
            new StringReader(xml),
            new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }
        );
        var doc = new XmlDocument();
        doc.Load(reader);
        return doc;
    }
}
