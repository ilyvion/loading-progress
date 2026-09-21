using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress;

internal sealed class Settings : ModSettings
{
    private bool _patchInitialization = true;
    public bool PatchInitialization
    {
        get => _patchInitialization;
        set => _patchInitialization = value;
    }

    private bool _patchReloadContent = true;
    public bool PatchReloadContent
    {
        get => _patchReloadContent;
        set => _patchReloadContent = value;
    }

    private bool _showInGameLoadingProgress = true;
    public bool ShowInGameLoadingProgress
    {
        get => _showInGameLoadingProgress;
        set => _showInGameLoadingProgress = value;
    }

    private bool _patchInGameDeferredRepaint = true;
    public bool PatchInGameDeferredRepaint
    {
        get => _patchInGameDeferredRepaint;
        set => _patchInGameDeferredRepaint = value;
    }

    private LoadingWindowPlacement _loadingWindowPlacement = LoadingWindowPlacement.Middle;
    public LoadingWindowPlacement LoadingWindowPlacement
    {
        get => _loadingWindowPlacement;
        set => _loadingWindowPlacement = value;
    }

    private Vector2 _customPlacementRelativePosition = new(0.5f, 0.5f);
    public Vector2 CustomPlacementRelativePosition
    {
        get => _customPlacementRelativePosition;
        set => _customPlacementRelativePosition = CustomPlacement.ClampRelative(value);
    }

    private float _lastLoadingTime = -1f;
    public float LastLoadingTime
    {
        get => _lastLoadingTime;
        set => _lastLoadingTime = value;
    }

    private int _lastLoadingModHash = -1;
    public int LastLoadingModHash
    {
        get => _lastLoadingModHash;
        set => _lastLoadingModHash = value;
    }

    private List<float> _loadingTimes = [];
    public List<float> LoadingTimes => _loadingTimes;

    public int LoadingTimeSampleCount => Math.Min(_loadingTimes.Count, _loadingTimesCapacity);

    private int _loadingTimesCapacity = 10;
    public int LoadingTimesCapacity
    {
        get => _loadingTimesCapacity;
        set => _loadingTimesCapacity = value;
    }

    private bool _clearEstimatesOnModListChange = true;
    public bool ClearEstimatesOnModListChange
    {
        get => _clearEstimatesOnModListChange;
        set => _clearEstimatesOnModListChange = value;
    }

    // The 10 most-recent entries get decreasing weights 10→1; everything older gets weight 1.
    // This means old entries are never squeezed out no matter how large the history grows.
    private const int WeightSpread = 10;

    public float? AverageLoadingTime
    {
        get
        {
            if (_loadingTimes.Count == 0)
            {
                return null;
            }
            var weightedSum = 0f;
            var totalWeight = 0f;
            var count = _loadingTimes.Count;
            var startIdx = Math.Max(count - _loadingTimesCapacity, 0);
            for (var i = startIdx; i < count; i++)
            {
                var distFromNewest = count - 1 - i;
                var weight = Math.Max(WeightSpread - distFromNewest, 1f);
                weightedSum += _loadingTimes[i] * weight;
                totalWeight += weight;
            }
            return weightedSum / totalWeight;
        }
    }

    private bool _showLastLoadingTime = true;
    public bool ShowLastLoadingTime
    {
        get => _showLastLoadingTime;
        set => _showLastLoadingTime = value;
    }

    private bool _showLoadingTimeAsCountDown;
    public bool ShowLoadingTimeAsCountDown
    {
        get => _showLoadingTimeAsCountDown;
        set => _showLoadingTimeAsCountDown = value;
    }

    private bool _showLastLoadingTimeProgressBar = true;
    public bool ShowLastLoadingTimeProgressBar
    {
        get => _showLastLoadingTimeProgressBar;
        set => _showLastLoadingTimeProgressBar = value;
    }

    private bool _showLastLoadingTimeInCorner = true;
    public bool ShowLastLoadingTimeInCorner
    {
        get => _showLastLoadingTimeInCorner;
        set => _showLastLoadingTimeInCorner = value;
    }

    private bool _showMemoryUsage = true;
    public bool ShowMemoryUsage
    {
        get => _showMemoryUsage;
        set => _showMemoryUsage = value;
    }

    private bool _showFasterGameLoadingEarlyModContentLoading = true;
    public bool ShowFasterGameLoadingEarlyModContentLoading
    {
        get => _showFasterGameLoadingEarlyModContentLoading;
        set => _showFasterGameLoadingEarlyModContentLoading = value;
    }

    private bool _trackStartupLoadingImpact;
    public bool TrackStartupLoadingImpact
    {
        get => _trackStartupLoadingImpact;
        set => _trackStartupLoadingImpact = value;
    }

    private bool _autoSaveStartupImpactReport;
    public bool AutoSaveStartupImpactReport
    {
        get => _autoSaveStartupImpactReport;
        set => _autoSaveStartupImpactReport = value;
    }

    private bool _showBaseGameOffThreadImpact;
    public bool ShowBaseGameOffThreadImpact
    {
        get => _showBaseGameOffThreadImpact;
        set => _showBaseGameOffThreadImpact = value;
    }

    private int _sessionsToKeep = SessionRetention.DefaultSessionsToKeep;
    public int SessionsToKeep
    {
        get => _sessionsToKeep;
        set => _sessionsToKeep = value;
    }

    private bool _keepPinnedSessions = true;
    public bool KeepPinnedSessions
    {
        get => _keepPinnedSessions;
        set => _keepPinnedSessions = value;
    }

    private bool _keepUnfinishedBoots = true;
    public bool KeepUnfinishedBoots
    {
        get => _keepUnfinishedBoots;
        set => _keepUnfinishedBoots = value;
    }

    private Color _progressBarColor = Widgets_Progressbar.BarColor;
    public Color ProgressBarColor
    {
        get => _progressBarColor;
        set => _progressBarColor = value;
    }

    private Color _smallBarColor = Widgets_Progressbar.SmallBarColor;
    public Color SmallBarColor
    {
        get => _smallBarColor;
        set => _smallBarColor = value;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref _patchInitialization, "patchInitialization", true);
        Scribe_Values.Look(ref _patchReloadContent, "patchReloadContent", true);
        Scribe_Values.Look(ref _showInGameLoadingProgress, "showInGameLoadingProgress", true);
        Scribe_Values.Look(ref _patchInGameDeferredRepaint, "patchInGameDeferredRepaint", true);
        Scribe_Values.Look(
            ref _loadingWindowPlacement,
            "loadingWindowPlacement",
            LoadingWindowPlacement.Middle
        );
        Scribe_Values.Look(
            ref _customPlacementRelativePosition,
            "customPlacementRelativePosition",
            new Vector2(0.5f, 0.5f)
        );
        Scribe_Values.Look(ref _lastLoadingTime, "lastLoadingTime", -1f);
        Scribe_Values.Look(ref _lastLoadingModHash, "lastLoadingModHash", -1);
        Scribe_Collections.Look(ref _loadingTimes, "loadingTimes", LookMode.Value);
        _loadingTimes ??= [];
        Scribe_Values.Look(ref _loadingTimesCapacity, "loadingTimesCapacity", 10);
        Scribe_Values.Look(
            ref _clearEstimatesOnModListChange,
            "clearEstimatesOnModListChange",
            true
        );

        if (
            Scribe.mode == LoadSaveMode.LoadingVars
            && _lastLoadingTime > 0
            && _loadingTimes.Count == 0
        )
        {
            // Migrate from old versions where only the last loading time was saved,
            // to the new system where a history of loading times is saved.
            // Put the last loading time into the history so it isn't lost,
            // and so it can be used in the average loading time calculation.
            _loadingTimes.Add(_lastLoadingTime);
            _lastLoadingTime = -1f;
        }
        Scribe_Values.Look(ref _showLastLoadingTime, "showLastLoadingTime", true);
        Scribe_Values.Look(ref _showLoadingTimeAsCountDown, "showLoadingTimeAsCountDown", false);
        Scribe_Values.Look(
            ref _showLastLoadingTimeProgressBar,
            "showLastLoadingTimeProgressBar",
            true
        );
        Scribe_Values.Look(ref _showLastLoadingTimeInCorner, "showLastLoadingTimeInCorner", true);
        Scribe_Values.Look(ref _showMemoryUsage, "showMemoryUsage", true);
        Scribe_Values.Look(
            ref _showFasterGameLoadingEarlyModContentLoading,
            "showFasterGameLoadingEarlyModContentLoading",
            true
        );
        Scribe_Values.Look(ref _trackStartupLoadingImpact, "trackStartupLoadingImpact", false);
        Scribe_Values.Look(ref _autoSaveStartupImpactReport, "autoSaveStartupImpactReport", false);
        Scribe_Values.Look(ref _showBaseGameOffThreadImpact, "showBaseGameOffThreadImpact", false);
        Scribe_Values.Look(
            ref _sessionsToKeep,
            "sessionsToKeep",
            SessionRetention.DefaultSessionsToKeep
        );
        Scribe_Values.Look(ref _keepPinnedSessions, "keepPinnedSessions", true);
        Scribe_Values.Look(ref _keepUnfinishedBoots, "keepUnfinishedBoots", true);
        Scribe_Values.Look(ref _progressBarColor, "progressBarColor", Widgets_Progressbar.BarColor);
        Scribe_Values.Look(ref _smallBarColor, "smallBarColor", Widgets_Progressbar.SmallBarColor);
    }

    private static TimeSpan? _loadingTime;

    private Vector2 _settingsScrollPosition;

    /// <summary>
    /// Height of the settings content, carried from the previous frame so the
    /// scroll view knows how far it reaches.
    /// </summary>
    private float _settingsContentHeight = 600f;

    /// <summary>
    /// Retention settings, sitting with the rest of the startup impact group and
    /// shown whenever tracking is on. Sessions reach the history from automatic
    /// saving and from the impact window's own Save button, so they can exist
    /// with auto-save off and the settings that bound them have to be reachable.
    /// </summary>
    /// <remarks>
    /// Its own method rather than more lines in DoSettingsWindowContents, which
    /// is already close to its class coupling limit.
    /// </remarks>
    private void DoSessionHistorySettings(Listing_Standard listingStandard)
    {
        _sessionsToKeep = (int)
            listingStandard.SliderLabeled(
                "LoadingProgress.SessionsToKeep".Translate(_sessionsToKeep),
                _sessionsToKeep,
                SessionRetention.MinimumSessionsToKeep,
                SessionRetention.MaximumSessionsToKeep,
                tooltip: "LoadingProgress.SessionsToKeep.Tip".Translate()
            );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.KeepPinnedSessions".Translate(),
            ref _keepPinnedSessions,
            "LoadingProgress.KeepPinnedSessions.Tip".Translate()
        );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.KeepUnfinishedBoots".Translate(),
            ref _keepUnfinishedBoots,
            "LoadingProgress.KeepUnfinishedBoots.Tip".Translate()
        );

        if (listingStandard.ButtonText("LoadingProgress.ManageSessions".Translate()))
        {
            Find.WindowStack.Add(new DialogStartupImpactHistory(modal: true));
        }
    }

    public void DoSettingsWindowContents(Rect inRect)
    {
        // Dialog_ModSettings gives this method a fixed 584px on a 700px window, and the
        // settings had grown to within a row of filling it: adding any further row pushed the
        // loading time button at the bottom off the edge, where it was clipped rather than
        // scrolled to.
        Rect viewRect = new(0f, 0f, inRect.width - ScrollBarWidth, _settingsContentHeight);
        Widgets.BeginScrollView(inRect, ref _settingsScrollPosition, viewRect);

        // maxOneColumn is load-bearing. Without it GetRect breaks to a second column as soon
        // as the content passes the view rect's height, and CurHeight, which is the column's
        // own y, resets with it. Feeding that back as the next frame's height collapses the
        // view rect until only the first row fits.
        Listing_Standard listingStandard = new() { maxOneColumn = true };
        listingStandard.Begin(viewRect);

        listingStandard.CheckboxLabeled(
            "LoadingProgress.PatchInitialization".Translate(),
            ref _patchInitialization,
            "LoadingProgress.PatchInitialization.Tip".Translate()
        );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.PatchReloadContent".Translate(),
            ref _patchReloadContent,
            "LoadingProgress.PatchReloadContent.Tip".Translate()
        );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.PatchInGameDeferredRepaint".Translate(),
            ref _patchInGameDeferredRepaint,
            "LoadingProgress.PatchInGameDeferredRepaint.Tip".Translate()
        );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.LastLoadingTime".Translate(),
            ref _showLastLoadingTime,
            "LoadingProgress.LastLoadingTime.Tip".Translate()
        );

        _loadingTimesCapacity = (int)
            listingStandard.SliderLabeled(
                "LoadingProgress.LoadingTimesCapacity".Translate(_loadingTimesCapacity),
                _loadingTimesCapacity,
                1f,
                50f,
                tooltip: "LoadingProgress.LoadingTimesCapacity.Tip".Translate()
            );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.ClearEstimatesOnModListChange".Translate(),
            ref _clearEstimatesOnModListChange,
            "LoadingProgress.ClearEstimatesOnModListChange.Tip".Translate()
        );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.LoadingTimeAsCountDown".Translate(),
            ref _showLoadingTimeAsCountDown,
            "LoadingProgress.LoadingTimeAsCountDown.Tip".Translate()
        );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.LastLoadingTimeProgressBar".Translate(),
            ref _showLastLoadingTimeProgressBar,
            "LoadingProgress.LastLoadingTimeProgressBar.Tip".Translate()
        );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.LastLoadingTimeInCorner".Translate(),
            ref _showLastLoadingTimeInCorner,
            "LoadingProgress.LastLoadingTimeInCorner.Tip".Translate()
        );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.ShowMemoryUsage".Translate(),
            ref _showMemoryUsage,
            "LoadingProgress.ShowMemoryUsage.Tip".Translate()
        );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.ShowFasterGameLoadingEarlyModContentLoading".Translate(),
            ref _showFasterGameLoadingEarlyModContentLoading,
            "LoadingProgress.ShowFasterGameLoadingEarlyModContentLoading.Tip".Translate()
        );

        listingStandard.CheckboxLabeled(
            "LoadingProgress.TrackStartupLoadingImpact".Translate(),
            ref _trackStartupLoadingImpact,
            "LoadingProgress.TrackStartupLoadingImpact.Tip".Translate()
        );

        if (_trackStartupLoadingImpact)
        {
            listingStandard.CheckboxLabeled(
                "LoadingProgress.AutoSaveStartupImpactReport".Translate(),
                ref _autoSaveStartupImpactReport,
                "LoadingProgress.AutoSaveStartupImpactReport.Tip".Translate()
            );

            // Tied to tracking rather than to auto-save: the Save button records a session
            // too, so a history can exist with auto-save off, and the settings that bound it
            // have to be reachable in that case.
            DoSessionHistorySettings(listingStandard);

            listingStandard.CheckboxLabeled(
                "LoadingProgress.ShowBaseGameOffThreadImpact".Translate(),
                ref _showBaseGameOffThreadImpact,
                "LoadingProgress.ShowBaseGameOffThreadImpact.Tip".Translate()
            );
        }

        listingStandard.CheckboxLabeled(
            "LoadingProgress.ShowInGameLoadingProgress".Translate(),
            ref _showInGameLoadingProgress,
            "LoadingProgress.ShowInGameLoadingProgress.Tip".Translate()
        );

        listingStandard.ColorPicker(
            "LoadingProgress.ProgressBarColor",
            "LoadingProgress.ProgressBarColor.Tip",
            _progressBarColor,
            Widgets_Progressbar.BarColor,
            newColor => _progressBarColor = newColor
        );

        listingStandard.ColorPicker(
            "LoadingProgress.SmallBarColor",
            "LoadingProgress.SmallBarColor.Tip",
            _smallBarColor,
            Widgets_Progressbar.SmallBarColor,
            newColor => _smallBarColor = newColor
        );

        if (
            listingStandard.ButtonTextLabeledPct(
                "LoadingProgress.LoadingWindowPlacement".Translate(),
                $"LoadingProgress.{_loadingWindowPlacement}".Translate(),
                0.6f,
                TextAnchor.MiddleLeft
            )
        )
        {
            List<FloatMenuOption> list =
            [
                new FloatMenuOption(
                    "LoadingProgress.Top".Translate(),
                    () => _loadingWindowPlacement = LoadingWindowPlacement.Top
                ),
                new FloatMenuOption(
                    "LoadingProgress.Middle".Translate(),
                    () => _loadingWindowPlacement = LoadingWindowPlacement.Middle
                ),
                new FloatMenuOption(
                    "LoadingProgress.Bottom".Translate(),
                    () => _loadingWindowPlacement = LoadingWindowPlacement.Bottom
                ),
                new FloatMenuOption(
                    "LoadingProgress.Custom".Translate(),
                    () => _loadingWindowPlacement = LoadingWindowPlacement.Custom
                ),
            ];
            Find.WindowStack.Add(new FloatMenu(list));
        }

        if (_loadingWindowPlacement == LoadingWindowPlacement.Custom)
        {
            if (listingStandard.ButtonText("LoadingProgress.EditCustomPlacement".Translate()))
            {
                Find.WindowStack.Add(new Dialog_CustomPlacementPreview());
            }
        }

        listingStandard.Gap();

        var avgLoadingTime = LoadingProgressMod.Settings.AverageLoadingTime;
        if (avgLoadingTime.HasValue)
        {
            _loadingTime ??= TimeSpan.FromSeconds(avgLoadingTime.Value);
            string text = "LoadingProgress.LoadingTime".Translate(
                Utilities.FormatDuration(_loadingTime.Value)
            );
            if (
                listingStandard.ButtonTextLabeled(
                    "LoadingProgress.LoadingTimeLabel".Translate(),
                    text,
                    tooltip: "LoadingProgress.LoadingTime.Tip".Translate()
                )
            )
            {
                Find.WindowStack.Add(new DialogStartupImpact());
            }
        }

        _settingsContentHeight = NextViewHeight(
            listingStandard.CurHeight,
            inRect.height,
            BottomPadding
        );
        listingStandard.End();

        Widgets.EndScrollView();
    }

    private const float ScrollBarWidth = 20f;
    private const float BottomPadding = 12f;

    /// <summary>
    /// Height to give the settings view rect next frame, from the height the
    /// listing just measured.
    /// </summary>
    /// <remarks>
    /// Never returns less than the viewport. A view rect shorter than what is
    /// on screen is what lets Listing break to a second column, and the height
    /// it reports afterwards is that column's own, so feeding it back
    /// unclamped collapses the screen to a single row.
    /// </remarks>
    internal static float NextViewHeight(
        float measuredContentHeight,
        float viewportHeight,
        float padding
    ) => Math.Max(viewportHeight, measuredContentHeight + padding);
}

internal enum LoadingWindowPlacement
{
    Top,
    Middle,
    Bottom,
    Custom,
}
