using ilyvion.LoadingProgress.FasterGameLoading;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(ModContentPack), nameof(ModContentPack.ReloadContentInt))]
public static class ModContentPack_ReloadContentInt_Patch
{
    internal static ModContentPack? CurrentModContentPack;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.VeryHigh)]
    public static void ProgressPrefix(ModContentPack __instance)
    {
        FasterGameLoadingProgressWindow.LoadingMod = __instance;
    }

    [HarmonyPriority(Priority.VeryLow)]
    public static bool Prefix(ModContentPack __instance)
    {
        bool shouldRunOriginal = __instance != CurrentModContentPack;
        if (!shouldRunOriginal)
        {
            // NOTE: This will only happen if *no other mod* (such as Faster Game Loading) already destructively prefixed ReloadContentInt.
            // LoadingProgressMod.Debug("Skipping ReloadContentInt for " + __instance.Name + " because we're running our custom loader on it");
        }
        else
        {
            // LoadingProgressMod.Debug("Letting original ReloadContentInt for " + __instance.Name + " run because we're not the one controlling it");
        }
        return shouldRunOriginal;
    }
}
