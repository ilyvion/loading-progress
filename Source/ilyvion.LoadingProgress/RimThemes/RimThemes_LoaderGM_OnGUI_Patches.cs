namespace ilyvion.LoadingProgress.RimThemes;

// LoaderGM is a MonoBehaviour whose OnGUI runs after the game's own, so anything drawn there
// lands on top of RimThemes' loading overlay.
[HarmonyPatch]
internal static class RimThemes_LoaderGM_OnGUI_Patches
{
    internal static bool Prepare() => RimThemesUtils.HasRimThemes;

    internal static MethodInfo TargetMethod() =>
        AccessTools.Method("aRandomKiwi.RimThemes.LoaderGM:OnGUI");

    internal static void Postfix()
    {
        if (RimThemesUtils.LoaderOverlayActive)
        {
            Verse_LongEventHandler_DrawOwnWindow_Patch.DrawWindows();
        }
    }
}
