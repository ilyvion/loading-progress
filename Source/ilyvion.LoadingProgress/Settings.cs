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

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref _patchInitialization, "patchInitialization", true);
    }

    public void DoSettingsWindowContents(Rect inRect)
    {
        Listing_Standard listingStandard = new();
        listingStandard.Begin(inRect);

        listingStandard.CheckboxLabeled(
            "LoadingProgress.PatchInitialization".Translate(),
            ref _patchInitialization,
            "LoadingProgress.PatchInitialization.Tip".Translate());

        listingStandard.End();
    }
}
