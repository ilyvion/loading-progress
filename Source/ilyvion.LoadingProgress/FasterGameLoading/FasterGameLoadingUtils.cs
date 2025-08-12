namespace ilyvion.LoadingProgress.FasterGameLoading;

public static class FasterGameLoadingUtils
{
    private static bool? _hasFasterGameLoading = null;
    public static bool HasFasterGameLoading
    {
        get
        {
            _hasFasterGameLoading ??= ModsConfig.ActiveModsInLoadOrder.Any(mod => mod.PackageId.Equals("taranchuk.fastergameloading", StringComparison.CurrentCultureIgnoreCase));
            return _hasFasterGameLoading.Value;
        }
    }

    private static HashSet<ModContentPack>? _loadedMods = null;
    public static HashSet<ModContentPack>? LoadedMods
    {
        get
        {
            _loadedMods ??= AccessTools.Field("FasterGameLoading.ModContentPack_ReloadContentInt_Patch:loadedMods").GetValue(null) as HashSet<ModContentPack>;
            return _loadedMods;
        }
    }

    public static bool FasterGameLoadingEarlyModContentLoadingIsFinished => FasterGameLoading_DelayedActions_LateUpdate_Patches._pauseFasterGameLoading_DelayedActions_LateUpdate || LoadingProgressWindow.CurrentStage >= LoadingStage.ExecuteToExecuteWhenFinished2;

    public static T? GetFasterGameLoadingSetting<T>(string settingName)
    {
        return AccessTools.Field("FasterGameLoading.FasterGameLoadingSettings:" + settingName)
            ?.GetValue(null) is T value ? value : default;
    }

    private static bool? _earlyModContentLoading = null;
    public static bool EarlyModContentLoading
    {
        get
        {
            _earlyModContentLoading ??= GetFasterGameLoadingSetting<bool>("earlyModContentLoading");
            return _earlyModContentLoading.Value;
        }
    }
}
