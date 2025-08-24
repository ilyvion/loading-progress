namespace ilyvion.LoadingProgress.FasterGameLoading;

[HarmonyPatch]
internal static class FasterGameLoading_DelayedActions_LateUpdate_Patches
{
    internal static bool _pauseFasterGameLoading_DelayedActions_LateUpdate;

    internal static bool Prepare() => FasterGameLoadingUtils.HasFasterGameLoading;

    internal static MethodInfo TargetMethod() => AccessTools.Method("FasterGameLoading.DelayedActions:LateUpdate");

    internal static bool Prefix() => !_pauseFasterGameLoading_DelayedActions_LateUpdate;
}
