using System.Reflection;

namespace ilyvion.LoadingProgress.FasterGameLoading;

[HarmonyPatch]
internal static class FasterGameLoading_DelayedActions_LateUpdate_Patches
{
    internal static bool _pauseFasterGameLoading_DelayedActions_LateUpdate;

    internal static bool Prepare()
    {
        return FasterGameLoadingUtils.HasFasterGameLoading;
    }

    internal static MethodInfo TargetMethod()
    {
        return AccessTools.Method("FasterGameLoading.DelayedActions:LateUpdate");
    }

    internal static bool Prefix()
    {
        return !_pauseFasterGameLoading_DelayedActions_LateUpdate;
    }
}
