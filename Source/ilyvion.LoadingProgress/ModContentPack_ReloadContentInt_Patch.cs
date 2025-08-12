using System.Reflection;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(ModContentPack), nameof(ModContentPack.ReloadContentInt))]
public static class ModContentPack_ReloadContentInt_Patch
{
    internal static ModContentPack? CurrentModContentPack;

    [HarmonyPriority(Priority.VeryLow)]
    public static bool Prefix(ModContentPack __instance)
    {
        bool shouldRunOriginal = __instance != CurrentModContentPack;
        if (!shouldRunOriginal)
        {
            // NOTE: This will only happen if *no other mod* (such as Faster Game Loading) already destructively prefixed ReloadContentInt.
            LoadingProgressMod.Debug("Skipping ReloadContentInt for " + __instance.Name + " because we're running our custom loader on it");
        }
        else
        {
            LoadingProgressMod.Debug("Letting original ReloadContentInt for " + __instance.Name + " run because we're not the one controlling it");
        }
        return shouldRunOriginal;
    }
}

[HarmonyPatch]
internal static class FasterGameLoading_DelayedActions_LateUpdate_Patches
{
    internal static bool _pauseFasterGameLoading_DelayedActions_LateUpdate;

    private static bool Prepare()
    {
        return ModsConfig.ActiveModsInLoadOrder.Any(mod => mod.PackageId.Equals("taranchuk.fastergameloading", StringComparison.CurrentCultureIgnoreCase));
    }

    private static MethodInfo TargetMethod()
    {
        return AccessTools.Method("FasterGameLoading.DelayedActions:LateUpdate");
    }

    private static bool Prefix()
    {
        return !_pauseFasterGameLoading_DelayedActions_LateUpdate;
    }
}
