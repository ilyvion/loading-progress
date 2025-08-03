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

    private LoadingWindowPlacement _loadingWindowPlacement = LoadingWindowPlacement.Top;
    public LoadingWindowPlacement LoadingWindowPlacement
    {
        get => _loadingWindowPlacement;
        set => _loadingWindowPlacement = value;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref _patchInitialization, "patchInitialization", true);
        Scribe_Values.Look(ref _loadingWindowPlacement, "loadingWindowPlacement", LoadingWindowPlacement.Middle);
    }

    public void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new();
        listingStandard.Begin(inRect);

        listingStandard.CheckboxLabeled(
            "LoadingProgress.PatchInitialization".Translate(),
            ref _patchInitialization,
            "LoadingProgress.PatchInitialization.Tip".Translate());

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