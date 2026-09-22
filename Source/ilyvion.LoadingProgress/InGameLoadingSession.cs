using System.Diagnostics;
using System.Xml;
using RimWorld.Planet;

namespace ilyvion.LoadingProgress;

internal enum InGameSessionKind
{
    None,
    WorldGeneration,
    PlanetRegeneration,
    NewGameMapGeneration,
    SaveLoading,
    EncounterMapGeneration,
    EncounterMapGenerationStatic,
}

// Ordered per kind below (PhasesByKind); the outer progress bar is PhaseIndex/PhaseCount within
// that list, mirroring LoadingProgressWindow's CurrentStage/LoadingStage.Finished bar.
internal enum InGameSessionPhase
{
    WorldGeneration_SetupSteps,
    WorldGeneration_LayerSteps,
    WorldGeneration_Deferred,

    PlanetRegeneration_RegeneratingLayers,

    NewGameMapGeneration_LoadingScene,
    NewGameMapGeneration_SetUp,
    NewGameMapGeneration_GenSteps,
    NewGameMapGeneration_Finalize,
    NewGameMapGeneration_PostInit,
    NewGameMapGeneration_Deferred,

    SaveLoading_LoadingScene,
    SaveLoading_ReadingFile,
    SaveLoading_World,
    SaveLoading_Maps,
    SaveLoading_ResolvingCrossReferences,
    SaveLoading_PostLoadInits,
    SaveLoading_Spawning,
    SaveLoading_Finishing,
    SaveLoading_Deferred,

    EncounterMapGeneration_SetUp,
    EncounterMapGeneration_GenSteps,
    EncounterMapGeneration_Finalize,
    EncounterMapGeneration_PostInit,
    EncounterMapGeneration_SpawningColonists,
    EncounterMapGeneration_Deferred,

    EncounterMapGenerationStatic_Generating,
}

// A session can start and end an unbounded number of times over one game process (loading
// different saves, generating a new world, settling or generating encounter maps repeatedly).
// Unlike LoadingProgressWindow.StageData's StageRules,
// which is a one-way pass through a fixed list for the single pre-game load and destructively
// prunes itself as it advances, nothing here is ever removed or "used up": DetermineKind and
// AdvanceSession are pure functions re-evaluated fresh on every call, and the mutable session
// fields below are fully reset on every transition so the same session can restart identically
// any number of times.
internal static class InGameLoadingSession
{
    // The eventTextKey InGameDeferredActionReplacement's redirected event is queued under; it
    // always continues whatever session was already active (that's the Prefix's own gating
    // condition for queuing it), so AdvanceSession treats it as a continuation instead of running
    // it through DetermineKind, which has no way to recover the pre-redirect kind from this key
    // alone.
    internal const string DeferredRedirectEventTextKey =
        "LoadingProgress.InGameExecuteToExecuteWhenFinished";

    internal static InGameSessionKind DetermineKind(
        string? eventTextKey,
        string? levelToLoad,
        ProgramState programState,
        bool inPlayScene,
        int mapCount,
        bool gameToLoadPending,
        bool doAsynchronously
    ) =>
        eventTextKey switch
        {
            "GeneratingWorld" => InGameSessionKind.WorldGeneration,
            "GeneratingPlanet" => InGameSessionKind.PlanetRegeneration,
            "GeneratingMap" => programState == ProgramState.Entry
            || levelToLoad == "Play"
            || (inPlayScene && mapCount == 0)
                ? InGameSessionKind.NewGameMapGeneration
            : doAsynchronously ? InGameSessionKind.EncounterMapGeneration
            // Synchronous in-play map generation (dev gizmos, gravship landings, the
            // new-colony quest): the action runs in one Update() with no repaint until it
            // finishes, so live progress is impossible; only the single static frame before
            // the freeze can be shown.
            : InGameSessionKind.EncounterMapGenerationStatic,
            // Every in-play encounter map (visit site, settlement attack, peace talks, escape
            // ship, transporters, ambush, caravan meeting/demand) queues this key; it always
            // runs synchronously, so it's unconditionally static regardless of doAsynchronously.
            "GeneratingMapForNewEncounter" => InGameSessionKind.EncounterMapGenerationStatic,
            // Settle/SetupCamp's second event; always a continuation of an EncounterMapGeneration
            // session, never a session start on its own.
            "SpawningColonists" => InGameSessionKind.EncounterMapGeneration,
            // The scene-load event (GameDataSaveLoader.LoadGame) carries levelToLoad "Play"; the
            // event that actually reads the file (queued afterwards, from Root_Play.Start) has no
            // levelToLoad of its own, so it's recognized instead by GameInitData still holding the
            // save name that the first event stashed there.
            "LoadingLongEvent" => levelToLoad == "Play" || (inPlayScene && gameToLoadPending)
                ? InGameSessionKind.SaveLoading
                // levelToLoad == "Entry" is the return-to-menu event.
                : InGameSessionKind.None,
            _ => InGameSessionKind.None,
        };

    internal static InGameSessionKind AdvanceSession(
        InGameSessionKind activeKind,
        bool hasCurrentEvent,
        bool eventChanged,
        bool queueEmpty,
        bool resetSignal,
        string? eventTextKey,
        string? levelToLoad,
        ProgramState programState,
        bool inPlayScene,
        int mapCount,
        bool gameToLoadPending,
        bool doAsynchronously
    )
    {
        if (resetSignal)
        {
            return InGameSessionKind.None;
        }

        if (!hasCurrentEvent)
        {
            // Still waiting between two events of the same chain (e.g. mid scene-load).
            return queueEmpty ? InGameSessionKind.None : activeKind;
        }

        return levelToLoad == "Entry" ? InGameSessionKind.None
            : !eventChanged ? activeKind
            : eventTextKey == DeferredRedirectEventTextKey ? activeKind
            : DetermineKind(
                eventTextKey,
                levelToLoad,
                programState,
                inPlayScene,
                mapCount,
                gameToLoadPending,
                doAsynchronously
            );
    }

    // The fixed, ordered phase list per kind; PhaseIndex/PhaseCount (the outer bar) are this
    // list's index/length for the session's current Kind.
    private static readonly Dictionary<InGameSessionKind, InGameSessionPhase[]> PhasesByKind = new()
    {
        [InGameSessionKind.WorldGeneration] =
        [
            InGameSessionPhase.WorldGeneration_SetupSteps,
            InGameSessionPhase.WorldGeneration_LayerSteps,
            InGameSessionPhase.WorldGeneration_Deferred,
        ],
        [InGameSessionKind.PlanetRegeneration] =
        [
            InGameSessionPhase.PlanetRegeneration_RegeneratingLayers,
        ],
        [InGameSessionKind.NewGameMapGeneration] =
        [
            InGameSessionPhase.NewGameMapGeneration_LoadingScene,
            InGameSessionPhase.NewGameMapGeneration_SetUp,
            InGameSessionPhase.NewGameMapGeneration_GenSteps,
            InGameSessionPhase.NewGameMapGeneration_Finalize,
            InGameSessionPhase.NewGameMapGeneration_PostInit,
            InGameSessionPhase.NewGameMapGeneration_Deferred,
        ],
        [InGameSessionKind.SaveLoading] =
        [
            InGameSessionPhase.SaveLoading_LoadingScene,
            InGameSessionPhase.SaveLoading_ReadingFile,
            InGameSessionPhase.SaveLoading_World,
            InGameSessionPhase.SaveLoading_Maps,
            InGameSessionPhase.SaveLoading_ResolvingCrossReferences,
            InGameSessionPhase.SaveLoading_PostLoadInits,
            InGameSessionPhase.SaveLoading_Spawning,
            InGameSessionPhase.SaveLoading_Finishing,
            InGameSessionPhase.SaveLoading_Deferred,
        ],
        [InGameSessionKind.EncounterMapGeneration] =
        [
            InGameSessionPhase.EncounterMapGeneration_SetUp,
            InGameSessionPhase.EncounterMapGeneration_GenSteps,
            InGameSessionPhase.EncounterMapGeneration_Finalize,
            InGameSessionPhase.EncounterMapGeneration_PostInit,
            InGameSessionPhase.EncounterMapGeneration_SpawningColonists,
            InGameSessionPhase.EncounterMapGeneration_Deferred,
        ],
        [InGameSessionKind.EncounterMapGenerationStatic] =
        [
            InGameSessionPhase.EncounterMapGenerationStatic_Generating,
        ],
    };

    internal static InGameSessionPhase[] PhasesFor(InGameSessionKind kind) =>
        PhasesByKind.GetValueOrDefault(kind, []);

    internal static InGameSessionPhase[] CurrentKindPhases => PhasesFor(Kind);

    // Only the kinds whose ExecuteWhenFinished closure InGameDeferredActionReplacement knows how
    // to chunk (a finished map's mapDrawer.RegenerateEverythingNow(), or world gen's
    // renderer.RegenerateAllLayersNow()) have a deferred phase to enter; PlanetRegeneration
    // already gets live per-layer progress through vanilla's own enumerator-based long event, and
    // EncounterMapGenerationStatic's one painted frame precedes any of the generation it
    // describes.
    private static readonly Dictionary<InGameSessionKind, InGameSessionPhase> DeferredPhaseByKind =
        new()
        {
            [InGameSessionKind.WorldGeneration] = InGameSessionPhase.WorldGeneration_Deferred,
            [InGameSessionKind.NewGameMapGeneration] =
                InGameSessionPhase.NewGameMapGeneration_Deferred,
            [InGameSessionKind.SaveLoading] = InGameSessionPhase.SaveLoading_Deferred,
            [InGameSessionKind.EncounterMapGeneration] =
                InGameSessionPhase.EncounterMapGeneration_Deferred,
        };

    internal static InGameSessionPhase? DetermineDeferredPhase(InGameSessionKind kind) =>
        DeferredPhaseByKind.TryGetValue(kind, out var phase) ? phase : null;

    // The levelToLoad every scene-loading long event carries; SceneLoadPhases' kinds are exactly
    // the ones whose session opens with such an event (GameDataSaveLoader.LoadGame and
    // PageUtility.InitGameStart both queue theirs with it, and Root_Play.Start then queues the
    // event that does the work the rest of the phases describe).
    internal const string PlayLevelName = "Play";

    // Vanilla's asynchronous scene load is a stage of its own, running before the first of the
    // phases the loading thread reports on: it is the only part of the session whose progress
    // comes from Unity rather than from a DeepProfiler label, and it is over before the file is
    // read or the map is generated.
    private static readonly HashSet<InGameSessionPhase> SceneLoadPhases =
    [
        InGameSessionPhase.SaveLoading_LoadingScene,
        InGameSessionPhase.NewGameMapGeneration_LoadingScene,
    ];

    internal static bool IsSceneLoadPhase(InGameSessionPhase phase) =>
        SceneLoadPhases.Contains(phase);

    // A session that begins on the scene-loading event starts in that phase; one picked up after
    // the scene is already loaded (Root_Play.Start's event recognized on its own) starts at the
    // phase after it, so the bar accounts for a scene load that has demonstrably already happened.
    internal static InGameSessionPhase DetermineStartPhase(
        InGameSessionKind kind,
        string? levelToLoad
    )
    {
        var phases = PhasesByKind[kind];
        return !IsSceneLoadPhase(phases[0]) || levelToLoad == PlayLevelName ? phases[0] : phases[1];
    }

    // The scene load reports a float; it is published against a fixed max so the snapshot keeps
    // holding a whole (phase, current, max) triple like every other phase's does.
    internal const int SceneLoadProgressResolution = 1000;

    internal static InGameSessionPhase NextPhase(
        InGameSessionPhase[] phases,
        InGameSessionPhase phase
    )
    {
        var index = Array.IndexOf(phases, phase);
        return index >= 0 && index + 1 < phases.Length ? phases[index + 1] : phase;
    }

    internal static int PhaseIndex => Math.Max(Array.IndexOf(CurrentKindPhases, Phase), 0);
    internal static int PhaseCount => Math.Max(CurrentKindPhases.Length, 1);

    // Pure label -> phase transition table for the kinds whose phases are driven by DeepProfiler
    // labels (world generation's SetupSteps -> LayerSteps transition instead comes from the
    // GeneratePlanetLayer prefix, since it needs the layer itself, not just its label; planet
    // regeneration emits no DeepProfiler labels at all and has only the one phase; the static
    // encounter kind ignores labels entirely, per OnProfilerLabel, since its one painted frame
    // is fixed before any of the generation it describes actually runs).
    internal static InGameSessionPhase DeterminePhaseFromLabel(
        InGameSessionKind kind,
        InGameSessionPhase currentPhase,
        string label
    ) =>
        kind switch
        {
            InGameSessionKind.NewGameMapGeneration => label switch
            {
                "Generate contents into map" => InGameSessionPhase.NewGameMapGeneration_GenSteps,
                "Finalize map init" => InGameSessionPhase.NewGameMapGeneration_Finalize,
                "MapComponent.MapGenerated()" or "Map generator post init" =>
                    InGameSessionPhase.NewGameMapGeneration_PostInit,
                _ => currentPhase,
            },
            // Settle/SetupCamp/dev gizmos generate their map through the same
            // MapGenerator.GenerateMap call as new-game map generation, so the label sequence
            // (and its phase boundaries) is identical; only the target phase enum differs.
            InGameSessionKind.EncounterMapGeneration => label switch
            {
                "Generate contents into map" => InGameSessionPhase.EncounterMapGeneration_GenSteps,
                "Finalize map init" => InGameSessionPhase.EncounterMapGeneration_Finalize,
                "MapComponent.MapGenerated()" or "Map generator post init" =>
                    InGameSessionPhase.EncounterMapGeneration_PostInit,
                _ => currentPhase,
            },
            // ResolveAllCrossReferences() and DoAllPostLoadInits() are each called exactly once,
            // globally, in this fixed order, from ScribeLoader.FinalizeLoading (never per map, so
            // unlike the spawning/PostMapInit loops below, splitting them into their own phases
            // can't reintroduce a same-phase reset for multi-map saves).
            InGameSessionKind.SaveLoading => label switch
            {
                "ResolveAllCrossReferences()" =>
                    InGameSessionPhase.SaveLoading_ResolvingCrossReferences,
                "DoAllPostLoadInits()" => InGameSessionPhase.SaveLoading_PostLoadInits,
                "Game.FinalizeInit" => InGameSessionPhase.SaveLoading_Finishing,
                _ => currentPhase,
            },
            InGameSessionKind.WorldGeneration
            or InGameSessionKind.PlanetRegeneration
            or InGameSessionKind.EncounterMapGenerationStatic
            or InGameSessionKind.None => currentPhase,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    // Verse.LongEventHandler.SetCurrentEventText is called exactly 4 times from Game.LoadGame,
    // in this fixed order; comparing call order is more robust than comparing the (already
    // translated, so locale-dependent) text itself.
    internal static InGameSessionPhase DetermineSaveLoadingPhaseFromEventTextCallOrder(
        int callIndex
    ) =>
        callIndex switch
        {
            1 => InGameSessionPhase.SaveLoading_World,
            2 => InGameSessionPhase.SaveLoading_Maps,
            3 => InGameSessionPhase.SaveLoading_ResolvingCrossReferences,
            >= 4 => InGameSessionPhase.SaveLoading_Spawning,
            _ => InGameSessionPhase.SaveLoading_ReadingFile,
        };

    // CrossRefHandler.ResolveAllCrossReferences() and PostLoadIniter.DoAllPostLoadInits() each
    // emit one DeepProfiler label around their whole loop (per-item progress instead comes from a
    // transpiler tick inside each loop, via OnInitializingSubProgressItemProcessed); each one also
    // marks the entry into its own dedicated phase (see DeterminePhaseFromLabel), so this reset is
    // just that phase's own starting point, not a reset within an already-running phase.
    // Thing.PostMapInit() is deliberately not included here: unlike these two, it (and the spawn
    // loop before it) runs once per map rather than once globally, so its own progress is
    // accumulated across maps instead (see OnThingPostMapInit) rather than reset per map.
    internal static bool IsSaveLoadingSubProgressResetLabel(string label) =>
        label is "ResolveAllCrossReferences()" or "DoAllPostLoadInits()";

    // The two loops' totals are known upfront from live collection counts.
    internal static int DetermineInitializingSubPhaseTotal(
        string label,
        int crossReferencingExposablesCount,
        int saveablesToPostLoadCount
    ) =>
        label switch
        {
            "ResolveAllCrossReferences()" => crossReferencingExposablesCount,
            "DoAllPostLoadInits()" => saveablesToPostLoadCount,
            _ => 0,
        };

    // Where Thing.PostMapInit()'s per-item progress applies for each kind that reaches it:
    // NewGameMapGeneration/EncounterMapGeneration get their own dedicated Finalize phase, while
    // SaveLoading runs Map.FinalizeInit() (and so this same loop) inside its Spawning phase, since
    // it has no separate phase for it.
    internal static InGameSessionPhase? DetermineThingPostMapInitPhase(InGameSessionKind kind) =>
        kind switch
        {
            InGameSessionKind.NewGameMapGeneration =>
                InGameSessionPhase.NewGameMapGeneration_Finalize,
            InGameSessionKind.EncounterMapGeneration =>
                InGameSessionPhase.EncounterMapGeneration_Finalize,
            InGameSessionKind.SaveLoading => InGameSessionPhase.SaveLoading_Spawning,
            InGameSessionKind.WorldGeneration
            or InGameSessionKind.PlanetRegeneration
            or InGameSessionKind.EncounterMapGenerationStatic
            or InGameSessionKind.None => null,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private const string GenStepLabelPrefix = "GenStep - ";
    private const string WorldGenStepLabelPrefix = "WorldGenStep - ";
    private const string WorldGenLayerLabelPrefix = "WorldGen - ";

    // WorldGenerator.GeneratePlanetLayer's own DeepProfiler label is just the layer's .NET type
    // name (PlanetLayer has no ToString override), so it's stripped from display entirely; the
    // GeneratePlanetLayer prefix sets a readable name from the layer's def instead.
    internal static bool IsSuppressedWorldGenLayerLabel(string label) =>
        label.StartsWith(WorldGenLayerLabelPrefix, StringComparison.Ordinal);

    internal static string StripKnownLabelPrefix(string label) =>
        label.StartsWith(GenStepLabelPrefix, StringComparison.Ordinal)
            ? label[GenStepLabelPrefix.Length..]
        : label.StartsWith(WorldGenStepLabelPrefix, StringComparison.Ordinal)
            ? label[WorldGenStepLabelPrefix.Length..]
        : label;

    internal static int AdvanceProgressCurrent(int current, int max) => Math.Min(current + 1, max);

    // A map's own item counts only become known once that map's loop actually starts, so growing
    // max mid-phase (see OnMapFinalizeLoadingStarted/OnCompressedThingsCounted/OnThingPostMapInit)
    // is unavoidable; rescaling current to the same fraction at the new max keeps the bar exactly
    // where it already was instead of letting the added capacity make it drop.
    internal static int RescaleCurrentForGrownMax(int current, int oldMax, int newMax) =>
        oldMax > 0 ? (int)Math.Round((double)current * newMax / oldMax) : current;

    // GeneratingMap and SpawningColonists (Settle/SetupCamp's two-event chain) are distinct
    // QueuedLongEvent objects, so unlike the map-generation phase boundaries above, this
    // transition can't be driven by a DeepProfiler label; it has to be detected from the event
    // key change itself.
    internal static bool ShouldEnterSpawningColonistsPhase(
        InGameSessionKind kind,
        bool eventChanged,
        string? eventTextKey
    ) =>
        kind == InGameSessionKind.EncounterMapGeneration
        && eventChanged
        && eventTextKey == "SpawningColonists";

    internal static int CountDirtyVisibleLayers(IEnumerable<(bool Dirty, bool Visible)> layers) =>
        layers.Count(l => l.Dirty && l.Visible);

    // Pure XML-node parsing so it can be tested against an in-memory fixture instead of a real
    // save file; called only while Scribe.loader.curXmlParent is the <game> element (right after
    // SetCurrentEventText("LoadingMap") is called, per ScribeLoader's own node-nesting rules).
    internal static int CountThingsAcrossMaps(XmlNode? gameNode)
    {
        var mapsNode = FindChildElement(gameNode, "maps");
        if (mapsNode == null)
        {
            return 0;
        }

        var total = 0;
        foreach (XmlNode mapNode in mapsNode.ChildNodes)
        {
            var thingsNode = FindChildElement(mapNode, "things");
            if (thingsNode != null)
            {
                total += thingsNode.ChildNodes.Count;
            }
        }
        return total;
    }

    private static XmlNode? FindChildElement(XmlNode? parent, string name)
    {
        if (parent == null)
        {
            return null;
        }
        foreach (XmlNode child in parent.ChildNodes)
        {
            if (child.Name == name)
            {
                return child;
            }
        }
        return null;
    }

    internal static InGameSessionKind Kind { get; private set; } = InGameSessionKind.None;
    internal static bool IsActive => Kind != InGameSessionKind.None;
    internal static InGameSessionPhase Phase => _snapshot.Phase;
    internal static string Label { get; private set; } = string.Empty;
    internal static string DisplayLabel => StripKnownLabelPrefix(Label);

    // Phase and (current, max) are only self-consistent when read together off one instance: the
    // loading thread (separate from the main thread that renders InGameLoadingWindow) publishes
    // every change via a single atomic reference swap in SetProgress, so a frame drawn mid-update
    // always observes either the whole old or the whole new (phase, current, max) triple, never a
    // new phase paired with the other's stale current/max (or vice versa).
    private sealed class ProgressSnapshot(InGameSessionPhase phase, int current, int max)
    {
        internal InGameSessionPhase Phase { get; } = phase;
        internal int Current { get; } = current;
        internal int Max { get; } = max;
    }

    private static volatile ProgressSnapshot _snapshot = new(
        InGameSessionPhase.WorldGeneration_SetupSteps,
        0,
        0
    );

    internal static (float current, float max)? Progress
    {
        get
        {
            var snapshot = _snapshot;
            return snapshot.Max > 0 ? (snapshot.Current, snapshot.Max) : null;
        }
    }

    private static void SetProgress(int current, int max) =>
        SetProgress(_snapshot.Phase, current, max);

    private static void SetProgress(InGameSessionPhase phase, int current, int max) =>
        _snapshot = new ProgressSnapshot(phase, current, max);

    private static int _saveLoadingEventTextCallIndex;

    private static Stopwatch? _stopwatch;
    internal static TimeSpan Elapsed => _stopwatch?.Elapsed ?? TimeSpan.Zero;

    private static LongEventHandler.QueuedLongEvent? _lastEventRef;
    private static bool _resetSignaled;

    internal static void SignalReset() => _resetSignaled = true;

    internal static void Update()
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.Finished)
        {
            return;
        }

        if (!LoadingProgressMod.Settings.ShowInGameLoadingProgress)
        {
            if (IsActive)
            {
                End();
            }
            return;
        }

        var currentEvent = LongEventHandler.currentEvent;
        var hasCurrentEvent = currentEvent != null;
        var eventChanged = !ReferenceEquals(currentEvent, _lastEventRef);
        _lastEventRef = currentEvent;

        var resetSignal = _resetSignaled;
        _resetSignaled = false;

        var eventTextKey = currentEvent?.eventTextKey;
        var levelToLoad = currentEvent?.levelToLoad;

        if (eventChanged && hasCurrentEvent && eventTextKey != null)
        {
            LogUnrecognizedKeyIfNeeded(eventTextKey);
        }

        var newKind = AdvanceSession(
            Kind,
            hasCurrentEvent,
            eventChanged,
            !LongEventHandler.AnyEventNowOrWaiting,
            resetSignal,
            eventTextKey,
            levelToLoad,
            Current.ProgramState,
            GenScene.InPlayScene,
            Current.Game?.Maps.Count ?? 0,
            !Find.GameInitData?.gameToLoad.NullOrEmpty() ?? false,
            currentEvent?.doAsynchronously ?? false
        );

        if (newKind == Kind)
        {
            if (IsSceneLoadPhase(Phase))
            {
                UpdateSceneLoadPhase(levelToLoad, hasCurrentEvent);
                return;
            }
            if (ShouldEnterSpawningColonistsPhase(newKind, eventChanged, eventTextKey))
            {
                Label = string.Empty;
                SetProgress(InGameSessionPhase.EncounterMapGeneration_SpawningColonists, 0, 0);
            }
            return;
        }

        if (newKind == InGameSessionKind.None)
        {
            End();
        }
        else
        {
            Kind = newKind;
            Label = string.Empty;
            // RegenerateLayersIfDirtyInLongEvent's prefix runs a frame before the GeneratingPlanet
            // event it just queued becomes the current event, so the layer count it captured has
            // to be picked up here rather than starting this session's max at 0 like every other
            // kind's.
            SetProgress(
                DetermineStartPhase(newKind, levelToLoad),
                0,
                newKind == InGameSessionKind.PlanetRegeneration
                    ? _pendingPlanetRegenerationLayerCount
                    : 0
            );
            _saveLoadingEventTextCallIndex = 0;
            _postMapInitCountedMap = null;
            _stopwatch = Stopwatch.StartNew();
        }
    }

    // levelLoadOp exists only while the scene-loading event is the current one: vanilla creates it
    // once that event's worker thread has finished and clears it together with the event itself, so
    // the op being absent while the event still carries the level means the scene load has not
    // begun yet, and the phase is over as soon as a different event is current.
    private static void UpdateSceneLoadPhase(string? levelToLoad, bool hasCurrentEvent)
    {
        if (levelToLoad == PlayLevelName)
        {
            var levelLoadOp = LongEventHandler.levelLoadOp;
            var progress =
                levelLoadOp == null ? 0f
                : levelLoadOp.isDone ? 1f
                : levelLoadOp.progress;
            SetProgress(
                Phase,
                (int)(progress * SceneLoadProgressResolution),
                SceneLoadProgressResolution
            );
        }
        else if (hasCurrentEvent)
        {
            Label = string.Empty;
            SetProgress(NextPhase(CurrentKindPhases, Phase), 0, 0);
        }
    }

    private static void End()
    {
        Kind = InGameSessionKind.None;
        Label = string.Empty;
        SetProgress(0, 0);
        _postMapInitCountedMap = null;
        _stopwatch = null;
    }

    // DeepProfiler.Start/End fire on whatever thread called them, which for other mods running
    // map/world generation off-thread (map preview mods, most notably) need not be the worker
    // thread actually driving our own session; without this check their labels would bleed into
    // our display.
    private static bool IsFromSessionThread() =>
        LongEventHandler.eventThread is { } thread && Thread.CurrentThread == thread;

    internal static void OnProfilerLabel(string label)
    {
        if (!IsActive || !IsFromSessionThread())
        {
            return;
        }

#pragma warning disable IDE0010 // None is unreachable here; IsActive already guards it above
        switch (Kind)
        {
            case InGameSessionKind.WorldGeneration:
                if (IsSuppressedWorldGenLayerLabel(label))
                {
                    return;
                }
                if (label.StartsWith(WorldGenStepLabelPrefix, StringComparison.Ordinal))
                {
                    var snapshot = _snapshot;
                    SetProgress(
                        AdvanceProgressCurrent(snapshot.Current, snapshot.Max),
                        snapshot.Max
                    );
                }
                break;

            case InGameSessionKind.NewGameMapGeneration:
                var mapGenPhase = DeterminePhaseFromLabel(Kind, Phase, label);
                if (mapGenPhase != Phase)
                {
                    SetProgress(mapGenPhase, 0, 0);
                }
                if (
                    Phase == InGameSessionPhase.NewGameMapGeneration_GenSteps
                    && label.StartsWith(GenStepLabelPrefix, StringComparison.Ordinal)
                )
                {
                    var snapshot = _snapshot;
                    var max = snapshot.Max == 0 ? MapGenerator.tmpGenSteps.Count : snapshot.Max;
                    SetProgress(AdvanceProgressCurrent(snapshot.Current, max), max);
                }
                break;

            case InGameSessionKind.EncounterMapGeneration:
                var encounterMapGenPhase = DeterminePhaseFromLabel(Kind, Phase, label);
                if (encounterMapGenPhase != Phase)
                {
                    SetProgress(encounterMapGenPhase, 0, 0);
                }
                if (
                    Phase == InGameSessionPhase.EncounterMapGeneration_GenSteps
                    && label.StartsWith(GenStepLabelPrefix, StringComparison.Ordinal)
                )
                {
                    var snapshot = _snapshot;
                    var max = snapshot.Max == 0 ? MapGenerator.tmpGenSteps.Count : snapshot.Max;
                    SetProgress(AdvanceProgressCurrent(snapshot.Current, max), max);
                }
                break;

            case InGameSessionKind.SaveLoading:
                var saveLoadingPhase = DeterminePhaseFromLabel(Kind, Phase, label);
                if (IsSaveLoadingSubProgressResetLabel(label))
                {
                    SetProgress(
                        saveLoadingPhase,
                        0,
                        DetermineInitializingSubPhaseTotal(
                            label,
                            Scribe.loader.crossRefs.crossReferencingExposables.Count,
                            Scribe.loader.initer.saveablesToPostLoad.Count
                        )
                    );
                }
                else if (saveLoadingPhase != Phase)
                {
                    var snapshot = _snapshot;
                    SetProgress(saveLoadingPhase, snapshot.Current, snapshot.Max);
                }
                break;

            // The static kind's one painted frame is fixed at session start from the event's
            // own fields (see InGameLoadingWindow), before any of the labels below are ever
            // emitted; picking one up here would only matter on a repaint that never happens.
            case InGameSessionKind.EncounterMapGenerationStatic:
                return;
        }
#pragma warning restore IDE0010

        Label = label;
    }

    internal static void OnProfilerLabelRestored(string? label)
    {
        if (
            !IsActive
            || label == null
            || !IsFromSessionThread()
            || Kind == InGameSessionKind.EncounterMapGenerationStatic
        )
        {
            return;
        }
        if (Kind == InGameSessionKind.WorldGeneration && IsSuppressedWorldGenLayerLabel(label))
        {
            // Keep showing the friendly per-layer name from OnWorldGenLayerStarted instead of
            // falling back to the type-name label.
            return;
        }
        Label = label;
    }

    internal static void OnWorldGenLayerStarted(PlanetLayerDef def)
    {
        if (!IsActive || Kind != InGameSessionKind.WorldGeneration || !IsFromSessionThread())
        {
            return;
        }
        Label = def.LabelCap.ToString();
        SetProgress(InGameSessionPhase.WorldGeneration_LayerSteps, 0, def.GenStepsInOrder.Count);
    }

    private static volatile int _pendingPlanetRegenerationLayerCount;

    // Not gated on IsActive/Kind: this fires from RegenerateLayersIfDirtyInLongEvent, which is
    // what queues the GeneratingPlanet event in the first place, so the session isn't active yet.
    internal static void OnPlanetRegenerationQueued(int dirtyVisibleLayerCount)
    {
        if (!UnityData.IsInMainThread)
        {
            return;
        }
        _pendingPlanetRegenerationLayerCount = dirtyVisibleLayerCount;
    }

    internal static void OnWorldDrawLayerRegenerationStarted(WorldDrawLayerBase layer)
    {
        if (!IsActive || !UnityData.IsInMainThread)
        {
            return;
        }
        // Fires for two unrelated callers: vanilla's own enumerator-driven "GeneratingPlanet"
        // long event (PlanetRegeneration), and InGameDeferredActionReplacement's chunked
        // replacement for world gen's deferred renderer regen (WorldGeneration, once it has
        // entered its own Deferred phase). Both advance the same per-layer progress the same way.
        if (
            Kind != InGameSessionKind.PlanetRegeneration
            && !(
                Kind == InGameSessionKind.WorldGeneration
                && Phase == InGameSessionPhase.WorldGeneration_Deferred
            )
        )
        {
            return;
        }
        Label = layer.GetType().Name;
        var snapshot = _snapshot;
        SetProgress(AdvanceProgressCurrent(snapshot.Current, snapshot.Max), snapshot.Max);
    }

    // Entered from InGameDeferredActionReplacement right before it starts chunking a finished
    // map's renderer regen (mapDrawer.RegenerateEverythingNow, per section) or world gen's
    // renderer regen (renderer.RegenerateAllLayersNow, per layer - though the per-layer advance
    // itself comes from OnWorldDrawLayerRegenerationStarted above, once this phase is active).
    internal static void OnDeferredRegenerationStarted(int totalUnits)
    {
        if (!IsActive || !UnityData.IsInMainThread)
        {
            return;
        }
        if (DetermineDeferredPhase(Kind) is not { } phase)
        {
            return;
        }
        Label = string.Empty;
        SetProgress(phase, 0, totalUnits);
    }

    // Only the map-section replacement calls this directly; the world-layer replacement's
    // progress instead comes from OnWorldDrawLayerRegenerationStarted, since
    // WorldDrawLayerBase.Regenerate() is already patched there.
    internal static void OnMapDrawerSectionRegenerated(int sectionX, int sectionZ)
    {
        if (
            !IsActive
            || !UnityData.IsInMainThread
            || DetermineDeferredPhase(Kind) is not { } phase
            || Phase != phase
        )
        {
            return;
        }
        Label = $"Section {sectionX},{sectionZ}";
        var snapshot = _snapshot;
        SetProgress(AdvanceProgressCurrent(snapshot.Current, snapshot.Max), snapshot.Max);
    }

    internal static void OnSetCurrentEventText()
    {
        if (!IsActive || Kind != InGameSessionKind.SaveLoading || !IsFromSessionThread())
        {
            return;
        }

        _saveLoadingEventTextCallIndex++;
        var newPhase = DetermineSaveLoadingPhaseFromEventTextCallOrder(
            _saveLoadingEventTextCallIndex
        );
        if (newPhase == Phase)
        {
            return;
        }

        SetProgress(
            newPhase,
            0,
            newPhase == InGameSessionPhase.SaveLoading_Maps
                ? CountThingsAcrossMaps(Scribe.loader.curXmlParent)
                : 0
        );
    }

    internal static void OnThingExposeData()
    {
        if (
            !IsActive
            || Kind != InGameSessionKind.SaveLoading
            || Phase != InGameSessionPhase.SaveLoading_Maps
            || Scribe.mode != LoadSaveMode.LoadingVars
            || Scribe.loader.curXmlParent?.ParentNode?.Name != "things"
            || !IsFromSessionThread()
        )
        {
            return;
        }

        var snapshot = _snapshot;
        SetProgress(AdvanceProgressCurrent(snapshot.Current, snapshot.Max), snapshot.Max);
    }

    // Called from a transpiler tick inserted after each ExposeData() call inside
    // CrossRefHandler.ResolveAllCrossReferences()'s and PostLoadIniter.DoAllPostLoadInits()'s
    // loops; which of the two loops (and so which of the two phases) is currently running doesn't
    // matter here, since OnProfilerLabel has already set the max to that loop's own total when its
    // label started.
    internal static void OnInitializingSubProgressItemProcessed()
    {
        if (
            !IsActive
            || Kind != InGameSessionKind.SaveLoading
            || Phase
                is not (
                    InGameSessionPhase.SaveLoading_ResolvingCrossReferences
                    or InGameSessionPhase.SaveLoading_PostLoadInits
                )
            || !IsFromSessionThread()
        )
        {
            return;
        }

        var snapshot = _snapshot;
        SetProgress(AdvanceProgressCurrent(snapshot.Current, snapshot.Max), snapshot.Max);
    }

    // Map.FinalizeLoading's non-compressed things (already deserialized) and the compressed things
    // MapFileCompressor.ThingsToSpawnAfterLoad creates from them are both known before that map's
    // spawn loop starts; a save with multiple maps runs this once per map, so each map's counts add
    // to a running total rather than replacing it.
    internal static void OnMapFinalizeLoadingStarted(int nonCompressedThingCount)
    {
        if (
            !IsActive
            || Kind != InGameSessionKind.SaveLoading
            || Phase != InGameSessionPhase.SaveLoading_Spawning
            || !IsFromSessionThread()
        )
        {
            return;
        }

        var snapshot = _snapshot;
        var newMax = snapshot.Max + nonCompressedThingCount;
        SetProgress(RescaleCurrentForGrownMax(snapshot.Current, snapshot.Max, newMax), newMax);
    }

    internal static void OnCompressedThingsCounted(int compressedThingCount)
    {
        if (
            !IsActive
            || Kind != InGameSessionKind.SaveLoading
            || Phase != InGameSessionPhase.SaveLoading_Spawning
            || !IsFromSessionThread()
        )
        {
            return;
        }

        var snapshot = _snapshot;
        var newMax = snapshot.Max + compressedThingCount;
        SetProgress(RescaleCurrentForGrownMax(snapshot.Current, snapshot.Max, newMax), newMax);
    }

    // GenSpawn.Spawn's respawningAfterLoad overload and SpawnBuildingAsPossible are also called,
    // with that flag false, from the ordinary in-play spawning paths (building placement, pawn
    // spawning, ...); the patches calling this already check the flag first, since that's cheaper
    // than the session/phase checks below and keeps this a no-op on those hot paths.
    internal static void OnThingSpawnedAfterLoad()
    {
        if (
            !IsActive
            || Kind != InGameSessionKind.SaveLoading
            || Phase != InGameSessionPhase.SaveLoading_Spawning
            || !IsFromSessionThread()
        )
        {
            return;
        }

        var snapshot = _snapshot;
        SetProgress(AdvanceProgressCurrent(snapshot.Current, snapshot.Max), snapshot.Max);
    }

    // Map.FinalizeInit() (whose loop this tracks) runs once per map, immediately after that same
    // map's own spawn loop (see Map.FinalizeLoading), so for a multi-map save the two loops
    // interleave map-by-map rather than running as two separate global passes; unlike
    // ResolveAllCrossReferences()/DoAllPostLoadInits() above, that rules out a dedicated phase per
    // loop; giving PostMapInit's own phase would require leaving and re-entering it once per map,
    // which is exactly the kind of same-phase-worth backward jump this is meant to avoid. Instead,
    // each map's count is added to the running Spawning-phase total the first time that map is
    // seen here, the same way the spawn loop's own totals already accumulate in
    // OnMapFinalizeLoadingStarted/OnCompressedThingsCounted, so the bar keeps climbing continuously
    // across every map's spawn-then-PostMapInit pair instead of resetting between them.
    private static Map? _postMapInitCountedMap;

    // Thing.PostMapInit() is virtual with an empty base implementation, and overrides aren't
    // required to call base, so the count this drives (and the map.listerThings.AllThings.Count
    // total it's compared against, taken from the map instance the first call for that map
    // carries) is approximate rather than exact; RescaleCurrentForGrownMax keeps the bar from
    // dropping when that approximate total is added on top of the spawn loop's already-full count.
    internal static void OnThingPostMapInit(Map? map)
    {
        if (!IsActive || !IsFromSessionThread())
        {
            return;
        }

        if (DetermineThingPostMapInitPhase(Kind) is not { } phase || Phase != phase)
        {
            return;
        }

        var snapshot = _snapshot;
        var max = snapshot.Max;
        var current = snapshot.Current;
        if (map != null && !ReferenceEquals(map, _postMapInitCountedMap))
        {
            _postMapInitCountedMap = map;
            var newMax = max + map.listerThings.AllThings.Count;
            current = RescaleCurrentForGrownMax(current, max, newMax);
            max = newMax;
        }
        SetProgress(AdvanceProgressCurrent(current, max), max);
    }

    private static readonly object _unmatchedEventKeysLock = new();
    private static readonly HashSet<string> _unmatchedEventKeys = [];

    private static void LogUnrecognizedKeyIfNeeded(string eventTextKey)
    {
        if (
            eventTextKey
            is "GeneratingWorld"
                or "GeneratingPlanet"
                or "GeneratingMap"
                or "GeneratingMapForNewEncounter"
                or "LoadingLongEvent"
                or "SpawningColonists"
                or DeferredRedirectEventTextKey
        )
        {
            return;
        }

        if (!Prefs.DevMode)
        {
            return;
        }

        lock (_unmatchedEventKeysLock)
        {
            if (!_unmatchedEventKeys.Add(eventTextKey))
            {
                return;
            }
        }

        LoadingProgressMod.DevMessage(
            $"In-game long event with key '{eventTextKey}' is not recognized by the in-game "
                + "loading session's key→kind mapping."
        );
    }
}
