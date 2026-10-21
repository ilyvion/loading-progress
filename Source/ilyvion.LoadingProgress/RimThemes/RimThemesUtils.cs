namespace ilyvion.LoadingProgress.RimThemes;

internal static class RimThemesUtils
{
    private static bool? _hasRimThemes;
    public static bool HasRimThemes
    {
        get
        {
            _hasRimThemes ??= ModsConfig.ActiveModsInLoadOrder.Any(IsRimThemes);
            return _hasRimThemes.Value;
        }
    }

    internal static bool IsRimThemes(ModMetaData mod) =>
        mod.PackageIdNonUnique.Equals("arandomkiwi.rimthemes", StringComparison.OrdinalIgnoreCase);

    private static FieldInfo? DisableCustomLoaderField =>
        field ??= AccessTools.Field("aRandomKiwi.RimThemes.Settings:disableCustomLoader");

    /// <summary>
    /// Whether RimThemes is currently drawing its full-screen loading overlay, which covers
    /// everything the game draws for a long event. Mirrors the condition in
    /// <c>LoaderGM.OnGUI</c>.
    /// </summary>
    public static bool LoaderOverlayActive =>
        HasRimThemes
        && LongEventHandler.ShouldWaitForEvent
        && DisableCustomLoaderField?.GetValue(null) is false;
}
