using System.Diagnostics;
using static ilyvion.LoadingProgress.Constants;

namespace ilyvion.LoadingProgress;

/// <summary>
/// Which of the mod's windows stands in for vanilla's status box.
/// </summary>
internal enum OwnWindow
{
    None,
    Loading,
    InGame,
}

internal sealed partial class LoadingProgressWindow
{
    internal static Vector2 WindowSize
    {
        get
        {
            var windowSize = field;
            if (LoadingProgressMod.Settings.ShowLastLoadingTime)
            {
                windowSize.y += 30f;
                if (LoadingProgressMod.Settings.LoadingTimeSampleCount > 0)
                {
                    windowSize.y += Text.LineHeightOf(GameFont.Small) + VerticalWidgetMargin;
                }
                if (HasLastLoadAndHashChanged())
                {
                    windowSize.y += Text.LineHeightOf(GameFont.Small) + VerticalWidgetMargin;
                }
            }
            return windowSize;
        }
    } = new(776f, 110f);

    private static bool HasLastLoadAndHashChanged() =>
        _lastLoadingTime.HasValue
        && _currentModHash != LoadingProgressMod.Settings.LastLoadingModHash;

    /// <summary>
    /// The DeepProfiler label Root.Start's InitializingInterface event opens with, which runs
    /// after the loading event and every ExecuteWhenFinished action it queued.
    /// </summary>
    internal const string LoadingFinishedLabel = "Misc Init (InitializingInterface)";

    internal static Stopwatch? _loadingStopwatch;
    internal static TimeSpan? _lastLoadingTime;
    internal static int _currentModHash;

    /// <summary>
    /// How long this launch took to load, set once the startup has reached the main menu,
    /// where the loading window records it. A startup that went straight into a game, or whose
    /// menu never settled, records none.
    /// </summary>
    internal static TimeSpan? CurrentLoadingTime { get; private set; }

    // Whether the startup ended with no loading time to record, not reaching the main menu.
    private static bool _loadingTimeNotRecorded;

    /// <summary>
    /// Whether this startup is over: it has reached the main menu, gone straight into a game,
    /// or waited for a menu that never settled as long as it waits. The loading window leaves
    /// the screen then, and records a loading time only for the first.
    /// </summary>
    internal static bool StartupComplete { get; private set; }

    /// <summary>
    /// What the main menu's corner, the pause menu and the mod settings show for this launch's
    /// loading time, each a way into its startup impact: see
    /// <see cref="LoadingTimeTextFor"/>.
    /// </summary>
    internal static string? LoadingTimeText =>
        LoadingTimeTextFor(CurrentLoadingTime, _loadingTimeNotRecorded);

    /// <summary>
    /// The loading time's text: the time, or that none was recorded when
    /// <paramref name="notRecorded"/>, as for a startup that never reached the main menu;
    /// null while there is neither, as before the startup ends.
    /// </summary>
    internal static string? LoadingTimeTextFor(TimeSpan? loadingTime, bool notRecorded) =>
        loadingTime is { } time
            ? "LoadingProgress.LoadingTime".Translate(Utilities.FormatDuration(time)).ToString()
        : notRecorded ? "LoadingProgress.LoadingTimeNotRecorded".Translate().ToString()
        : null;

    /// <summary>
    /// Which of the mod's windows stands in for vanilla's status box right now.
    /// </summary>
    internal static OwnWindow CurrentOwnWindow =>
        OwnWindowFor(
            CurrentStage,
            StartupComplete,
            Current.ProgramState == ProgramState.Entry,
            InGameLoadingSession.IsActive
        );

    /// <summary>
    /// The loading window while loading runs and, at a startup, on through the long events
    /// that follow it until the main menu is usable; the in-game window for a load or a
    /// generation started after that; none once a startup has reached the menu.
    /// </summary>
    internal static OwnWindow OwnWindowFor(
        LoadingStage stage,
        bool startupComplete,
        bool inEntry,
        bool inGameSessionActive
    ) =>
        (stage, inGameSessionActive, startupComplete, inEntry) switch
        {
            (not LoadingStage.Finished, _, _, _) => OwnWindow.Loading,
            (_, true, _, _) => OwnWindow.InGame,
            (_, _, false, true) => OwnWindow.Loading,
            _ => OwnWindow.None,
        };

    /// <summary>
    /// Shows a long event that runs after loading on the activity line: its text, or a word
    /// for one that has none, which vanilla's status box shows as a bare "...".
    /// </summary>
    internal static void ShowPostLoadEvent(LongEventHandler.QueuedLongEvent queuedEvent) =>
        SetCurrentLoadingActivityRaw(ActivityFor(queuedEvent.eventText));

    /// <summary>
    /// The activity line for a long event after loading: its text, or a word for one that has
    /// none.
    /// </summary>
    internal static string ActivityFor(string? eventText) =>
        eventText is { Length: > 0 } text
            ? text
            : Translations.GetTranslation("LoadingProgress.FinishingUp");

    /// <summary>
    /// Stops the clock as the startup ends, and with <paramref name="recordLoadingTime"/>
    /// records the loading time, less <paramref name="pausedMs"/> the game sat paused in the
    /// background. Only a startup that reached the main menu has one: one that went straight
    /// into a game, or whose menu never settled, leaves without recording a time, which would
    /// stop short of the menu or take in however long it waited.
    /// </summary>
    /// <remarks>
    /// The long events other mods run after the interface begins initializing, and any stall
    /// on the menu's first frame, are part of the player's wait, so the time shown and
    /// estimated runs to here rather than to the end of loading. The startup impact window
    /// counts to the same idle frame, so the two figures agree, and the corner shows the same
    /// time.
    /// </remarks>
    internal static void CompleteStartup(float pausedMs, bool recordLoadingTime)
    {
        if (StartupComplete)
        {
            return;
        }
        StartupComplete = true;

        if (_loadingStopwatch is { } loadingStopwatch)
        {
            loadingStopwatch.Stop();
            if (recordLoadingTime)
            {
                RecordLoadingTime(
                    Math.Max(0f, (float)loadingStopwatch.Elapsed.TotalSeconds - (pausedMs / 1000f))
                );
            }
            else
            {
                _loadingTimeNotRecorded = true;
            }
        }
        Translations.Clear();
    }

    private static void RecordLoadingTime(float elapsedSeconds)
    {
        CurrentLoadingTime = TimeSpan.FromSeconds(elapsedSeconds);
        var settings = LoadingProgressMod.Settings;
        AddLoadingTimeSample(settings, elapsedSeconds, _currentModHash);
        settings.Write();
    }

    /// <summary>
    /// Adds a loading time to the history the estimate is taken from.
    /// </summary>
    internal static void AddLoadingTimeSample(Settings settings, float elapsedSeconds, int modHash)
    {
        // Samples from earlier versions ran only to the interface starting to initialize, so
        // they read short against what is measured now; the history starts afresh once.
        if (!settings.LoadingTimesMeasuredToMenu)
        {
            settings.LoadingTimes.Clear();
            settings.LoadingTimesMeasuredToMenu = true;
        }

        // Mod list changed: the existing list served as the estimate this load, but we clear
        // it so history reflects the new mod configuration.
        if (settings.ClearEstimatesOnModListChange && modHash != settings.LastLoadingModHash)
        {
            settings.LoadingTimes.Clear();
        }

        settings.LoadingTimes.Add(elapsedSeconds);
        while (settings.LoadingTimes.Count > settings.LoadingTimesCapacity)
        {
            settings.LoadingTimes.RemoveAt(0);
        }

        settings.LastLoadingModHash = modHash;
    }

    internal static void DrawContents(Rect rect)
    {
        if (_loadingStopwatch is null)
        {
            _loadingStopwatch = Stopwatch.StartNew();
            var avgTime = LoadingProgressMod.Settings.AverageLoadingTime;
            _lastLoadingTime = avgTime.HasValue ? TimeSpan.FromSeconds(avgTime.Value) : null;
            _currentModHash = StableListHasher.ComputeListHash(
                LoadedModManager.RunningModsListForReading.Select(mod => mod.PackageId)
            );
            LoadingSessionStats.Reset();
        }

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.UpperLeft;

        var loadingProgressRect = rect;
        loadingProgressRect.x += HorizontalMargin;
        loadingProgressRect.y += 10f;
        loadingProgressRect.width -= 2 * HorizontalMargin;
        loadingProgressRect.height = Text.LineHeight;

        Widgets.Label(loadingProgressRect, Translations.GetTranslation("LoadingProgress.Title"));

        if (LoadingProgressMod.Settings.ShowMemoryUsage)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            Widgets.Label(
                loadingProgressRect,
                Translations.GetTranslation(
                    "LoadingProgress.MemoryUsage",
                    Utilities.FormatBytes(MemoryUsage.ManagedHeapBytes),
                    Utilities.FormatBytes(MemoryUsage.ProcessBytes)
                )
            );
            GUI.color = Color.white;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        var loadingActivityRect = loadingProgressRect;
        loadingProgressRect.y += loadingProgressRect.height + VerticalWidgetMargin;
        Text.Font = GameFont.Small;
        loadingActivityRect.height = Text.LineHeight;

        var rule = CurrentStageRule;
        string? label = null;
        if (rule.CustomLabel is StageDisplayLabel customLabel)
        {
            label = customLabel(_currentLoadingActivity);
        }
        label ??= GetStageTranslation(rule.Stage, _currentLoadingActivity);

        if (!string.IsNullOrEmpty(label))
        {
            var ellipsisRect = loadingProgressRect;
            ellipsisRect.width -= 10f;
            Widgets.Label(
                loadingProgressRect,
                Utilities.ClampTextWithEllipsisMarkupAware(ellipsisRect, label)
            );
        }

        var progressRect = loadingProgressRect;
        progressRect.y += loadingActivityRect.height + VerticalWidgetMargin;
        progressRect.height = ProgressBarHeight;
        var barColor = LoadingProgressMod.Settings.ProgressBarColor;
        var smallBarColor = LoadingProgressMod.Settings.SmallBarColor;
        if (StageProgress is (float currentValue, float maxValue))
        {
            Widgets_Progressbar.DrawHorizontalProgressBar(
                progressRect,
                (int)CurrentStage,
                (int)LoadingStage.Finished,
                currentValue,
                maxValue,
                barColor,
                smallBarColor
            );
        }
        else
        {
            Widgets_Progressbar.DrawHorizontalProgressBar(
                progressRect,
                (int)CurrentStage,
                (int)LoadingStage.Finished,
                customBarColor: barColor
            );
        }

        if (LoadingProgressMod.Settings.ShowLastLoadingTime)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;

            var loadingTimeRect = progressRect;
            loadingTimeRect.y += progressRect.height + VerticalWidgetMargin;
            loadingTimeRect.height = Text.LineHeight;

            var elapsed = _loadingStopwatch.Elapsed;
            if (_lastLoadingTime.HasValue)
            {
                var totalSeconds = (float)_lastLoadingTime.Value.TotalSeconds;
                Widgets_Progressbar.DrawHorizontalProgressBar(
                    loadingTimeRect,
                    Math.Clamp((float)elapsed.TotalSeconds, 0f, totalSeconds),
                    totalSeconds,
                    (float)elapsed.TotalSeconds > totalSeconds
                        ? (float)elapsed.TotalSeconds - totalSeconds
                        : null,
                    (float)elapsed.TotalSeconds > totalSeconds ? 10f : null,
                    TimeBarColor,
                    TimerSmallBarColor
                );
            }

            var lastLoadingTimeText = _lastLoadingTime.HasValue
                ? $"~{Utilities.FormatDuration(_lastLoadingTime.Value)}"
                : "--:--";
            string loadingTimeText;
            if (LoadingProgressMod.Settings.ShowLoadingTimeAsCountDown)
            {
                var remainingTime = _lastLoadingTime.HasValue
                    ? _lastLoadingTime.Value - elapsed
                    : TimeSpan.Zero;
                loadingTimeText =
                    remainingTime > TimeSpan.Zero
                        ? Translations.GetTranslation(
                            "LoadingProgress.TimeRemaining",
                            Utilities.FormatDuration(remainingTime),
                            lastLoadingTimeText
                        )
                        : Translations.GetTranslation(
                            "LoadingProgress.TimeOverEstimate",
                            Utilities.FormatDuration(remainingTime),
                            lastLoadingTimeText
                        );
            }
            else
            {
                loadingTimeText = $"{Utilities.FormatDuration(elapsed)} / {lastLoadingTimeText}";
            }
            Widgets.Label(loadingTimeRect, loadingTimeText);

            var sampleCount = LoadingProgressMod.Settings.LoadingTimeSampleCount;
            if (sampleCount > 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                var sampleInfoRect = loadingTimeRect;
                sampleInfoRect.y += loadingTimeRect.height + VerticalWidgetMargin;
                sampleInfoRect.height = Text.LineHeight;
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                Widgets.Label(
                    sampleInfoRect,
                    sampleCount == 1
                        ? Translations.GetTranslation("LoadingProgress.EstimateBasedOnSingle")
                        : Translations.GetTranslation(
                            "LoadingProgress.EstimateBasedOn",
                            sampleCount
                        )
                );
                GUI.color = Color.white;
                loadingTimeRect = sampleInfoRect;
            }

            if (HasLastLoadAndHashChanged())
            {
                Text.Font = GameFont.Small;

                var modHashRect = loadingTimeRect;
                modHashRect.y += loadingTimeRect.height + VerticalWidgetMargin;
                modHashRect.height = Text.LineHeight;
                Widgets.Label(
                    modHashRect,
                    Translations.GetTranslation("LoadingProgress.ModHashChanged")
                );
            }
        }

        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static Color TimeBarColor => LoadingProgressMod.Settings.ProgressBarColor.Darken(0.2f);
    private static readonly Color TimerSmallBarColor = Color
        .white.Darken(0.2f)
        .ToTransparent(0.75f);
}
