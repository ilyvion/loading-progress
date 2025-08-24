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

    private LoadingWindowPlacement _loadingWindowPlacement = LoadingWindowPlacement.Middle;
    public LoadingWindowPlacement LoadingWindowPlacement
    {
        get => _loadingWindowPlacement;
        set => _loadingWindowPlacement = value;
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

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref _patchInitialization, "patchInitialization", true);
        Scribe_Values.Look(ref _patchReloadContent, "patchReloadContent", true);
        Scribe_Values.Look(ref _loadingWindowPlacement, "loadingWindowPlacement", LoadingWindowPlacement.Middle);
        Scribe_Values.Look(ref _lastLoadingTime, "lastLoadingTime", -1f);
        Scribe_Values.Look(ref _lastLoadingModHash, "lastLoadingModHash", -1);
        Scribe_Values.Look(ref _showLastLoadingTime, "showLastLoadingTime", true);
        Scribe_Values.Look(ref _showLoadingTimeAsCountDown, "showLoadingTimeAsCountDown", false);
        Scribe_Values.Look(ref _showLastLoadingTimeProgressBar, "showLastLoadingTimeProgressBar", true);
        Scribe_Values.Look(ref _showLastLoadingTimeInCorner, "showLastLoadingTimeInCorner", true);
        Scribe_Values.Look(ref _showFasterGameLoadingEarlyModContentLoading, "showFasterGameLoadingEarlyModContentLoading", true);
        Scribe_Values.Look(ref _trackStartupLoadingImpact, "trackStartupLoadingImpact", false);
    }

    public void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new();
        listingStandard.Begin(inRect);

        listingStandard.CheckboxLabeled(
            "LoadingProgress.PatchInitialization".Translate(),
            ref _patchInitialization,
            "LoadingProgress.PatchInitialization.Tip".Translate());

        listingStandard.CheckboxLabeled(
            "LoadingProgress.PatchReloadContent".Translate(),
            ref _patchReloadContent,
            "LoadingProgress.PatchReloadContent.Tip".Translate());

        listingStandard.CheckboxLabeled(
            "LoadingProgress.LastLoadingTime".Translate(),
            ref _showLastLoadingTime,
            "LoadingProgress.LastLoadingTime.Tip".Translate());

        listingStandard.CheckboxLabeled(
            "LoadingProgress.LoadingTimeAsCountDown".Translate(),
            ref _showLoadingTimeAsCountDown,
            "LoadingProgress.LoadingTimeAsCountDown.Tip".Translate());

        listingStandard.CheckboxLabeled(
            "LoadingProgress.LastLoadingTimeProgressBar".Translate(),
            ref _showLastLoadingTimeProgressBar,
            "LoadingProgress.LastLoadingTimeProgressBar.Tip".Translate());

        listingStandard.CheckboxLabeled(
            "LoadingProgress.LastLoadingTimeInCorner".Translate(),
            ref _showLastLoadingTimeInCorner,
            "LoadingProgress.LastLoadingTimeInCorner.Tip".Translate());

        listingStandard.CheckboxLabeled(
            "LoadingProgress.ShowFasterGameLoadingEarlyModContentLoading".Translate(),
            ref _showFasterGameLoadingEarlyModContentLoading,
            "LoadingProgress.ShowFasterGameLoadingEarlyModContentLoading.Tip".Translate());

        listingStandard.CheckboxLabeled(
            "LoadingProgress.TrackStartupLoadingImpact".Translate(),
            ref _trackStartupLoadingImpact,
            "LoadingProgress.TrackStartupLoadingImpact.Tip".Translate());

        if (listingStandard.ButtonTextLabeledPct(
            "LoadingProgress.LoadingWindowPlacement".Translate(),
            $"LoadingProgress.{_loadingWindowPlacement}".Translate(),
            0.6f,
            TextAnchor.MiddleLeft))
        {
            List<FloatMenuOption> list =
            [
                new FloatMenuOption(
                    "LoadingProgress.Top".Translate(),
                    () => _loadingWindowPlacement = LoadingWindowPlacement.Top),
                new FloatMenuOption(
                    "LoadingProgress.Middle".Translate(),
                    () => _loadingWindowPlacement = LoadingWindowPlacement.Middle),
                new FloatMenuOption(
                    "LoadingProgress.Bottom".Translate(),
                    () => _loadingWindowPlacement = LoadingWindowPlacement.Bottom),
                // new FloatMenuOption(
                //     "LoadingProgress.Custom".Translate(),
                //     () => _loadingWindowPlacement = LoadingWindowPlacement.Custom)
            ];
            Find.WindowStack.Add(new FloatMenu(list));
        }

        listingStandard.End();
    }
}

internal enum LoadingWindowPlacement
{
    Top,
    Middle,
    Bottom,
    Custom
}
