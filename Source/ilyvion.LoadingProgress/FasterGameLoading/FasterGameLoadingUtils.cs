namespace ilyvion.LoadingProgress.FasterGameLoading;

internal static class FasterGameLoadingUtils
{
    private static bool? _hasFasterGameLoading;
    public static bool HasFasterGameLoading
    {
        get
        {
            _hasFasterGameLoading ??= ModsConfig.ActiveModsInLoadOrder.Any(mod => mod.PackageId.Equals("taranchuk.fastergameloading", StringComparison.OrdinalIgnoreCase));
            return _hasFasterGameLoading.Value;
        }
    }

    private static HashSet<ModContentPack>? _loadedMods;
    public static HashSet<ModContentPack>? LoadedMods
    {
        get
        {
            _loadedMods ??= AccessTools.Field("FasterGameLoading.ModContentPack_ReloadContentInt_Patch:loadedMods").GetValue(null) as HashSet<ModContentPack>;
            return _loadedMods;
        }
    }

    public static bool FasterGameLoadingEarlyModContentLoadingIsFinished => FasterGameLoading_DelayedActions_LateUpdate_Patches._pauseFasterGameLoading_DelayedActions_LateUpdate || LoadingProgressWindow.CurrentStage >= LoadingStage.ExecuteToExecuteWhenFinished2;

    public static T? GetFasterGameLoadingSetting<T>(string settingName) => AccessTools.Field("FasterGameLoading.FasterGameLoadingSettings:" + settingName)
            ?.GetValue(null) is T value ? value : default;

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
