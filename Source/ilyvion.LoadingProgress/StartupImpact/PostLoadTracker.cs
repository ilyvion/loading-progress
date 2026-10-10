namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// Follows the startup's tail: the long events that run once loading is over, up to the frame
/// the main menu is usable (two idle frames within 250 ms of each other, or the fifth idle
/// frame in a row). Keeps the loading window's activity line on the event that is running,
/// ends the startup for the window when the menu is reached and, with tracking on, times
/// each event under the mod whose code it runs.
/// </summary>
/// <remarks>
/// <para>
/// Loading is over, and the tracking clock stops, where the interface begins initializing,
/// the window's Finished stage. What runs after, the rest of the interface's initialization
/// and the windows and setup other mods queue for after loading, is time the player still
/// waits through, with the loading window on screen. Each such event's own work is timed,
/// and credited to the mod whose code it runs, under its own category, so it shows beside
/// everything else that mod cost: a synchronous event's run, an enumerator event's steps in
/// each frame, and an asynchronous event from its thread's start to the frame the engine
/// finishes it in, since its work runs beside the frames. The rest of each frame, drawing
/// the loading window among it, is no event's. The engine's own events go under the base
/// game, and one whose code no mod loaded is left untimed, as a deferred action from such
/// code is. The interface's own event is timed from the clock stop instead, since the clock
/// stops partway through it. The
/// deferred actions an event queues run when it finishes; each is timed under the mod it is
/// credited to, the way deferred actions during loading are, with the event paused meanwhile.
/// That needs the deferred-action patch, which the settings for the initialization patches
/// turn off; without it, the engine runs those actions inside the event.
/// </para>
/// <para>
/// Time the game spends paused is left out. The engine keeps running in the background only
/// while it loads; on the first idle frame it applies the player's 'Run in background'
/// preference, off by default, and from then on an unfocused game stops between frames. A
/// pause runs from the end of the last frame to where the next frame's long events begin. A
/// player who switched to another window during a long load would otherwise find the time
/// away counted as loading time.
/// </para>
/// </remarks>
internal static class PostLoadTracker
{
    /// <summary>
    /// What everything timed after loading is timed under, the long events and the deferred
    /// actions alike: the remaining time's entry for after loading takes what is timed under it
    /// back off. Something timed then under any other key would count in its owner's time and
    /// again in that entry.
    /// </summary>
    internal const string Category = "LoadingProgress.StartupImpact.PostLoadLongEvent";

    // Two idle frames closer together than this mean the menu is drawing freely; a wait
    // between frames longer than this, while the game was in the background, was a pause.
    private const float QuickFrameMs = 250f;

    // A menu that never draws two frames that close together, below four frames a second,
    // counts as reached at this many idle frames in a row.
    private const int SlowMenuIdleFrames = 5;

    // A menu that has not settled this long after loading, in active time, is taken never to:
    // a mod that keeps a long event queued on the menu would otherwise keep the loading window
    // over it for good. The wait after loading takes 20 to 30 s on a list of 230 mods.
    internal const float MaxTailMs = 5f * 60f * 1000f;
    private static float _tailStartMs = -1f;

    private static bool _done;
    private static LongEventHandler.QueuedLongEvent? _current;

    // What _current is timed under, once its timing has started.
    private static (ModContentPack? Owner, bool IsBaseGame, string Category)? _currentTiming;

    private static float _lastIdleFrameMs = -1f;
    private static int _idleFrames;

    private static bool _watchingFrames;
    private static bool _watchingFocus;
    private static float _frameEndMs = -1f;
    private static bool _unfocusedSinceFrameEnd;

    /// <summary>
    /// The time the game has sat paused in the background since loading finished, so far.
    /// </summary>
    internal static float PausedMs { get; private set; }

    private static float RealtimeMs => Time.realtimeSinceStartup * 1000f;

    internal static void Update()
    {
        if (!IsFollowingTheTail)
        {
            return;
        }

        var startupImpact = LoadingProgressMod.instance?.StartupImpact;
        var finished = LoadingProgressWindow.CurrentStage == LoadingStage.Finished;
        WatchFrames();

        // Straight into a game: there is no idle menu to wait for, and the game's loading is not
        // the startup's.
        if (
            HasLeftForAGame(
                Current.ProgramState,
                QuickStarter.quickStarted,
                LongEventHandler.currentEvent?.levelToLoad
            )
        )
        {
            Finish(startupImpact, menuReached: false);
            return;
        }

        var active = RealtimeMs - PausedMs;
        if (_tailStartMs < 0f)
        {
            _tailStartMs = active;
        }
        if (HasWaitedTooLong(_tailStartMs, active))
        {
            LoadingProgressMod.Warning(
                $"The main menu had not settled {MaxTailMs / 60000f} minutes after loading, so the startup ends without a time to it."
            );
            Finish(startupImpact, menuReached: false);
            return;
        }

        var current = LongEventHandler.currentEvent;
        if (finished && current != null)
        {
            LoadingProgressWindow.ShowPostLoadEvent(current);
        }

        if (
            finished
            && current == null
            && !LongEventHandler.AnyEventNowOrWaiting
            && Find.UIRoot != null
        )
        {
            // The queue going empty is not the player being able to click. A mod that builds
            // its state on the menu's first frame stalls the main thread between that frame
            // and the next, so the menu counts as reached only once two idle frames have come
            // close together, which includes any such stall in the time to the menu. Pauses
            // are left out of the comparison, so one is not mistaken for a stall.
            _idleFrames++;
            var settled = IsMenuSettled(_lastIdleFrameMs, active, _idleFrames);
            _lastIdleFrameMs = active;
            if (settled)
            {
                Finish(startupImpact, menuReached: true);
            }
        }
        else
        {
            _lastIdleFrameMs = -1f;
            _idleFrames = 0;
        }
    }

    /// <summary>
    /// Takes the pause before this frame, if any, out of the wait after loading, where this
    /// frame's long-event work begins. An asynchronous event open through the pause has it
    /// taken back off its time, and goes on being timed from here.
    /// </summary>
    internal static void BeginFrame()
    {
        if (!IsFollowingTheTail)
        {
            return;
        }

        var paused = PauseIn(
            _frameEndMs,
            RealtimeMs,
            _unfocusedSinceFrameEnd,
            Application.runInBackground
        );
        PausedMs += paused;
        if (paused > 0f && _current is { } open)
        {
            StopCurrentQuietly(paused, "Could not stop timing an event after loading");
            StartCurrentQuietly(
                open,
                "Could not go on timing an event after loading, so the rest of it is untimed"
            );
        }
    }

    /// <summary>
    /// Starts timing the current event's work in this frame, when the wait after loading is
    /// being timed, the event does not load the game's scene and
    /// <see cref="StartsTimingThisCall"/> says this call into it starts its work.
    /// </summary>
    internal static void BeginEventWork()
    {
        if (
            _current != null
            || !IsTimingTheTail
            || LongEventHandler.currentEvent is not { } current
            || HasLeftForAGame(Current.ProgramState, QuickStarter.quickStarted, current.levelToLoad)
            || !StartsTimingThisCall(
                isEnumerator: current.eventActionEnumerator != null,
                isAsynchronous: current.doAsynchronously,
                threadStarted: LongEventHandler.eventThread != null,
                waitingToBeDisplayed: current.ShouldWaitUntilDisplayed
            )
        )
        {
            return;
        }

        StartCurrentQuietly(current, "Could not time an event after loading, so it runs untimed");
    }

    /// <summary>
    /// Stops timing the current event once the engine's call into it returns, unless
    /// <see cref="StaysOpenAfterThisCall"/> says its work goes on beside the frames.
    /// </summary>
    internal static void EndEventWork()
    {
        if (
            _current is { } open
            && !StaysOpenAfterThisCall(
                open.doAsynchronously,
                stillCurrent: ReferenceEquals(LongEventHandler.currentEvent, open)
            )
        )
        {
            StopCurrentQuietly(0f, "Could not stop timing an event after loading");
        }
    }

    /// <summary>
    /// Whether the engine's call into the current event starts its work: every call into an
    /// enumerator event runs some of its steps, an asynchronous event's work begins with the
    /// call that starts its thread, and a synchronous event runs once its text has been
    /// displayed.
    /// </summary>
    internal static bool StartsTimingThisCall(
        bool isEnumerator,
        bool isAsynchronous,
        bool threadStarted,
        bool waitingToBeDisplayed
    ) => isEnumerator || (isAsynchronous ? !threadStarted : !waitingToBeDisplayed);

    /// <summary>
    /// Whether an event's timing stays open after the engine's call into it returns: an
    /// asynchronous event's thread works on until the engine finishes the event.
    /// </summary>
    internal static bool StaysOpenAfterThisCall(bool isAsynchronous, bool stillCurrent) =>
        isAsynchronous && stillCurrent;

    /// <summary>
    /// Starts timing the long event running when the clock stops: the interface's own
    /// initialization. The clock stops at a profiler label inside that event, after its work
    /// began untimed. It runs inside that event, before the engine assigns the interface, so a
    /// failure to start is logged rather than let through to it.
    /// </summary>
    internal static void StartAtClockStop()
    {
        if (!_done && _current == null && LongEventHandler.currentEvent is { } current)
        {
            StartCurrentQuietly(
                current,
                "Could not time the interface's initialization, so it runs untimed"
            );
        }
    }

    /// <summary>
    /// Whether the wait after loading is being timed: loading has finished with tracking on,
    /// the startup has not ended, and the program is still at its entry state. The game's scene
    /// leaves that state as it starts, and runs its interface's initialization inline, before
    /// the tracker next looks and ends the startup.
    /// </summary>
    internal static bool IsTimingTheTail =>
        !_done
        && Current.ProgramState == ProgramState.Entry
        && LoadingProgressMod.instance?.StartupImpact
            is { WasTrackingEnabledAtStartup: true, LoadingTimeMeasured: true };

    // Whether the tracker follows the wait after loading: timing needs tracking, while the
    // window's tail runs with or without it.
    private static bool IsFollowingTheTail =>
        IsTimingTheTail || (!_done && LoadingProgressWindow.CurrentStage == LoadingStage.Finished);

    /// <summary>
    /// Runs the deferred actions queued after loading as the engine does, each timed under the
    /// mod it is credited to, under <see cref="Category"/>. The long event they run after is
    /// paused meanwhile, so it keeps only its own time.
    /// </summary>
    /// <remarks>
    /// Like the engine, it runs an action that another queues during the pass in the same pass,
    /// and clears the queue at the end.
    /// </remarks>
    internal static void RunDeferredActions()
    {
        if (LongEventHandler.executingToExecuteWhenFinished)
        {
            Log.Warning("Already executing.");
            return;
        }

        LongEventHandler.executingToExecuteWhenFinished = true;
        try
        {
            RunDeferredActions(LongEventHandler.toExecuteWhenFinished);
        }
        finally
        {
            LongEventHandler.executingToExecuteWhenFinished = false;
        }
    }

    /// <summary>
    /// Runs <paramref name="actions"/>, including any queued onto it while they run, each timed
    /// under the mod it is credited to, under <see cref="Category"/>, with the current long
    /// event paused meanwhile, then clears it.
    /// </summary>
    /// <remarks>
    /// A failure in the timing, whether pausing the event, naming an action or going on with
    /// the event afterwards, is logged and leaves that part untimed. Every action still runs,
    /// as it would without the timing.
    /// </remarks>
    internal static void RunDeferredActions(List<Action> actions)
    {
        if (actions.Count == 0)
        {
            return;
        }

        TimeOnThisThread();
        var resume = _current;
        StopCurrentQuietly(0f, "Could not pause timing the current event for its deferred actions");

        DeepProfiler.Start("ExecuteToExecuteWhenFinished()");
        try
        {
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var label = LongEventHandler_ExecuteToExecuteWhenFinished_Patches.ProfilerLabel(
                    action
                );
                LongEventHandler_ExecuteToExecuteWhenFinished_Patches.RunLabelledDeferredAction(
                    action,
                    label,
                    CategoryLabel(action, label),
                    timed: true,
                    Category
                );
            }
        }
        finally
        {
            DeepProfiler.End();
            actions.Clear();
            if (resume != null)
            {
                StartCurrentQuietly(
                    resume,
                    "Could not go on timing the current event after its deferred actions, so the rest of it is untimed"
                );
            }
        }
    }

    // Timing never keeps the work it times from running: the tracker's own steps are logged
    // when they fail, with what that leaves untimed, and the frame or the pass goes on.
    private static void StopCurrentQuietly(float discountMs, string failure) =>
        Quietly(() => StopCurrent(discountMs), failure);

    private static void StartCurrentQuietly(
        LongEventHandler.QueuedLongEvent queuedEvent,
        string failure
    ) => Quietly(() => StartCurrent(queuedEvent), failure);

    private static void Quietly(Action step, string failure)
    {
        try
        {
            step();
        }
        catch (Exception e)
        {
            LoadingProgressMod.Warning($"{failure}: {e.Message}");
        }
    }

    // What an action after loading is timed as: its code, named the way it was written, or
    // the engine's label for it when naming it fails.
    private static string CategoryLabel(Action action, string profilerLabel)
    {
        try
        {
            return DescribeCode(action.Method.DeclaringType, action.Method.Name);
        }
        catch (Exception e)
        {
            LoadingProgressMod.Warning(
                $"Could not name the deferred action {profilerLabel}: {e.Message}"
            );
            return profilerLabel;
        }
    }

    /// <summary>
    /// Ends the tail: the window leaves, and records its loading time when the menu was
    /// reached, as the startup takes its time to the menu; then the startup saves its session
    /// once. An asynchronous event still being timed is stopped.
    /// </summary>
    private static void Finish(StartupImpact? startupImpact, bool menuReached)
    {
        StopCurrentQuietly(0f, "Could not stop timing the last event after loading");
        _done = true;
        if (_watchingFocus)
        {
            UnwatchFocus();
        }

        // One reading of the clock serves the time to the menu and the window's loading time,
        // so the two are the same figure. The time to the menu is taken before the window's
        // bookkeeping, which writes the settings, and the session is saved even if that
        // bookkeeping throws.
        var loadingMs = LoadingProgressWindow.ElapsedMs;
        if (menuReached)
        {
            startupImpact?.MarkMenuReached(loadingMs);
        }
        try
        {
            LoadingProgressWindow.CompleteStartup(loadingMs, recordLoadingTime: menuReached);
        }
        finally
        {
            startupImpact?.FinishStartup();
        }
    }

    /// <summary>
    /// Whether the menu has gone <see cref="MaxTailMs"/> of active time since the wait after
    /// loading began, at <paramref name="tailStartMs"/>, without settling.
    /// </summary>
    internal static bool HasWaitedTooLong(float tailStartMs, float nowMs) =>
        tailStartMs >= 0f && nowMs - tailStartMs >= MaxTailMs;

    /// <summary>
    /// Whether the startup has gone into a game instead of to the main menu: the program has
    /// left its entry state, a quicktest has asked for the game's scene, or the current event
    /// loads that scene, as loading a save does.
    /// </summary>
    internal static bool HasLeftForAGame(
        ProgramState programState,
        bool quickStarted,
        string? levelToLoad
    ) =>
        programState != ProgramState.Entry || quickStarted || levelToLoad == GenScene.PlaySceneName;

    /// <summary>
    /// Whether an idle frame at <paramref name="nowMs"/> counts the menu as usable: the first
    /// idle frame never does (there is nothing before it), and one that comes after a stall
    /// does not either, since the stall is what the player was waiting through. A menu that
    /// never draws quickly counts as reached after <see cref="SlowMenuIdleFrames"/> idle frames
    /// in a row.
    /// </summary>
    internal static bool IsMenuSettled(float lastIdleFrameMs, float nowMs, int idleFrames) =>
        lastIdleFrameMs >= 0f
        && (nowMs - lastIdleFrameMs < QuickFrameMs || idleFrames >= SlowMenuIdleFrames);

    /// <summary>
    /// How much of the wait from the end of the last frame to <paramref name="frameStartMs"/>,
    /// where this frame's long-event work begins, the game sat paused: all of it when the wait
    /// was longer than a frame, the game was in the background at some point since that frame
    /// ended, and it does not run there; else none.
    /// </summary>
    internal static float PauseIn(
        float frameEndMs,
        float frameStartMs,
        bool unfocusedSinceFrameEnd,
        bool runInBackground
    )
    {
        if (frameEndMs < 0f || !unfocusedSinceFrameEnd || runInBackground)
        {
            return 0f;
        }

        var waitMs = frameStartMs - frameEndMs;
        return waitMs > QuickFrameMs ? waitMs : 0f;
    }

    private static void WatchFrames()
    {
        if (_watchingFrames || Find.Root == null)
        {
            return;
        }

        _watchingFrames = true;
        _watchingFocus = TryWatchFocus(WatchFocus);
        _ = Find.Root.StartCoroutine(FrameEnds());
    }

    /// <summary>
    /// Runs <paramref name="watchFocus"/>, which subscribes to the game going into the
    /// background, and says whether it did; when it fails, that is logged, and time spent in the
    /// background after loading counts toward the time to the menu.
    /// </summary>
    internal static bool TryWatchFocus(Action watchFocus)
    {
        try
        {
            watchFocus();
            return true;
        }
        catch (Exception e)
        {
            LoadingProgressMod.Warning(
                $"Could not watch for the game going into the background, so time spent there after loading counts toward the time to the menu: {e.Message}"
            );
            return false;
        }
    }

    // The engine's focus event is only referenced from these two, so a missing one fails when
    // they are called rather than when their callers are compiled.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WatchFocus() => Application.focusChanged += OnFocusChanged;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void UnwatchFocus() => Application.focusChanged -= OnFocusChanged;

    private static void OnFocusChanged(bool focused)
    {
        if (!focused)
        {
            _unfocusedSinceFrameEnd = true;
        }
    }

    // When each frame ends, after everything it drew: a pause falls between one frame's end
    // and the next frame's start.
    private static IEnumerator FrameEnds()
    {
        var endOfFrame = new WaitForEndOfFrame();
        while (!_done)
        {
            yield return endOfFrame;
            _frameEndMs = RealtimeMs;
            _unfocusedSinceFrameEnd = !Application.isFocused;
        }
    }

    // Everything after loading runs on the main thread, and only the active thread's timings
    // count toward the totals and the stage ledger, or can have a pause taken off. The
    // deferred-task replacement makes the main thread the active one, but that patch is
    // skipped when the settings turn the initialization patches off, so the tracker does it
    // too before it times anything.
    private static void TimeOnThisThread() =>
        LoadingProgressMod.instance.StartupImpact.UpdateActiveThreadId();

    /// <summary>
    /// Starts timing <paramref name="queuedEvent"/> as the current event, under the mod whose
    /// code it runs.
    /// </summary>
    /// <remarks>
    /// The event becomes the current one first, and its category is kept only once its timing
    /// has started. An event whose start throws therefore stays current, untimed, until its
    /// work in the frame ends, and nothing is stopped for it.
    /// </remarks>
    internal static void StartCurrent(LongEventHandler.QueuedLongEvent queuedEvent)
    {
        _current = queuedEvent;
        var category = $"{Category}|{Describe(queuedEvent)}";
        var owner = OwnerOf(queuedEvent, out var isBaseGame);
        StartTiming(owner, isBaseGame, category);
        _currentTiming = (owner, isBaseGame, category);
    }

    /// <summary>
    /// Starts <paramref name="category"/> under its owner, as
    /// <see cref="StartupImpactProfilerUtil.Start"/> does, making this thread, the main one,
    /// the active thread first.
    /// </summary>
    internal static void StartTiming(ModContentPack? owner, bool isBaseGame, string category)
    {
        TimeOnThisThread();
        StartupImpactProfilerUtil.Start(owner, isBaseGame, category);
    }

    /// <summary>
    /// Stops timing the current event, taking <paramref name="discountMs"/>, time the game sat
    /// paused, back off. The event is no longer the current one even when stopping throws, and
    /// one whose timing never started is let go with nothing stopped.
    /// </summary>
    internal static void StopCurrent(float discountMs)
    {
        if (_current == null)
        {
            return;
        }

        var timing = _currentTiming;
        (_current, _currentTiming) = (null, null);
        if (timing is { } started)
        {
            StartupImpactProfilerUtil.Stop(
                started.Owner,
                started.IsBaseGame,
                started.Category,
                discountMs
            );
        }
    }

    internal static ModContentPack? OwnerOf(
        LongEventHandler.QueuedLongEvent queuedEvent,
        out bool isBaseGame
    ) => OwnerOf(queuedEvent.eventAction, queuedEvent.eventActionEnumerator, out isBaseGame);

    /// <summary>
    /// The mod whose code an event runs, by the rule a deferred action's code follows: see
    /// <see cref="StartupImpactProfilerUtil.OwnerOfCode"/>.
    /// </summary>
    internal static ModContentPack? OwnerOf(
        Delegate? action,
        object? enumerator,
        out bool isBaseGame
    ) =>
        StartupImpactProfilerUtil.OwnerOfCode(
            action?.Method.DeclaringType?.Assembly ?? enumerator?.GetType().Assembly,
            out isBaseGame
        );

    internal static string Describe(LongEventHandler.QueuedLongEvent queuedEvent) =>
        Describe(
            queuedEvent.eventTextKey,
            queuedEvent.eventAction,
            queuedEvent.eventActionEnumerator
        );

    /// <summary>
    /// What to call an event: its text as the player saw it when the key translates, else the
    /// key, else the method it ran, named the way it was written.
    /// </summary>
    internal static string Describe(string? key, Delegate? action, object? enumerator)
    {
        if (key is { Length: > 0 } text)
        {
            return text.CanTranslate() ? text.Translate().ToString() : text;
        }

        if (action != null)
        {
            return DescribeCode(action.Method.DeclaringType, action.Method.Name);
        }

        var enumeratorType = enumerator?.GetType();
        return enumeratorType != null
            ? DescribeCode(enumeratorType.DeclaringType, enumeratorType.Name)
            : "?";
    }

    /// <summary>
    /// A method named the way it was written. A lambda compiles to a method named like
    /// <c>&lt;Init&gt;b__3_0</c> on a class nested in the type it was written in, and an
    /// iterator to a class named like <c>&lt;Load&gt;d__5</c>, neither of which tells a
    /// player anything; this gives the written type's full name and the method in brackets.
    /// </summary>
    internal static string DescribeCode(Type? type, string memberName)
    {
        while (type?.DeclaringType != null && CompilerGenerated.Is(type))
        {
            type = type.DeclaringType;
        }

        var name = WrittenName(memberName);
        return type == null ? name
            : name.StartsWith('.') ? $"{type.FullName}{name}"
            : $"{type.FullName}.{name}";
    }

    private static string WrittenName(string name)
    {
        if (name.StartsWith('<'))
        {
            var close = name.IndexOf('>', StringComparison.Ordinal);
            if (close > 1)
            {
                return name[1..close];
            }
        }
        return name;
    }
}
