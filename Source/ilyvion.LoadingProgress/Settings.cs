using UnityEngine;

namespace ilyvion.LoadingProgress;

public class Settings : ModSettings
{
    private bool _patchInitialization = true;
    public bool PatchInitialization
    {
        get => _patchInitialization;
        set => _patchInitialization = value;
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

    private bool _showLastLoadingTimeProgressBar = true;
    public bool ShowLastLoadingTimeProgressBar
    {
        get => _showLastLoadingTimeProgressBar;
        set => _showLastLoadingTimeProgressBar = value;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref _patchInitialization, "patchInitialization", true);
        Scribe_Values.Look(ref _loadingWindowPlacement, "loadingWindowPlacement", LoadingWindowPlacement.Middle);
        Scribe_Values.Look(ref _lastLoadingTime, "lastLoadingTime", -1f);
        Scribe_Values.Look(ref _lastLoadingModHash, "lastLoadingModHash", -1);
        Scribe_Values.Look(ref _showLastLoadingTime, "showLastLoadingTime", true);
        Scribe_Values.Look(ref _showLastLoadingTimeProgressBar, "showLastLoadingTimeProgressBar", true);
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
            "LoadingProgress.LastLoadingTime".Translate(),
            ref _showLastLoadingTime,
            "LoadingProgress.LastLoadingTime.Tip".Translate());

        listingStandard.CheckboxLabeled(
            "LoadingProgress.LastLoadingTimeProgressBar".Translate(),
            ref _showLastLoadingTimeProgressBar,
            "LoadingProgress.LastLoadingTimeProgressBar.Tip".Translate());

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

public enum LoadingWindowPlacement
{
    Top,
    Middle,
    Bottom,
    Custom
}