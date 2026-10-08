namespace ilyvion.LoadingProgress.FasterGameLoading;

internal static class FasterGameLoadingUtils
{
    private static bool? _hasFasterGameLoading;
    public static bool HasFasterGameLoading
    {
        get
        {
            _hasFasterGameLoading ??= ModsConfig.ActiveModsInLoadOrder.Any(IsFasterGameLoading);
            return _hasFasterGameLoading.Value;
        }
    }

    /// <summary>
    /// Whether <paramref name="mod"/> is Faster Game Loading. The id is compared without the
    /// <c>_steam</c> postfix the game adds to a Workshop copy's id while a local copy with the
    /// same id is installed.
    /// </summary>
    internal static bool IsFasterGameLoading(ModMetaData mod) =>
        mod.PackageIdNonUnique.Equals(
            "taranchuk.fastergameloading",
            StringComparison.OrdinalIgnoreCase
        );

    public static HashSet<ModContentPack>? LoadedMods
    {
        get
        {
            field ??=
                AccessTools
                    .Field("FasterGameLoading.ModContentPack_ReloadContentInt_Patch:loadedMods")
                    ?.GetValue(null) as HashSet<ModContentPack>;
            return field;
        }
    }

    public static bool FasterGameLoadingEarlyModContentLoadingIsFinished =>
        FasterGameLoading_DelayedActions_LateUpdate_Patches._pauseFasterGameLoading_DelayedActions_LateUpdate
        || LoadingProgressWindow.CurrentStage >= LoadingStage.ExecuteToExecuteWhenFinished2;

    public static T? GetFasterGameLoadingSetting<T>(string settingName) =>
        AccessTools
            .Field("FasterGameLoading.FasterGameLoadingSettings:" + settingName)
            ?.GetValue(null)
            is T value
            ? value
            : default;

    private static bool? _earlyModContentLoading;
    public static bool EarlyModContentLoading
    {
        get
        {
            _earlyModContentLoading ??= GetFasterGameLoadingSetting<bool>("earlyModContentLoading");
            return _earlyModContentLoading.Value;
        }
    }
}
