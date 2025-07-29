using System.Reflection;

namespace ilyvion.LoadingProgress;

[HarmonyPatch]
internal static class AlienPartGenerator_LoadGraphicsHook_Patches
{
    private static bool Prepare()
    {
        return ModsConfig.ActiveModsInLoadOrder.Any(mod => mod.PackageId.Equals("erdelf.humanoidalienraces", StringComparison.CurrentCultureIgnoreCase));
    }

    private static MethodInfo TargetMethod()
    {
        return AccessTools.Method("AlienRace.AlienPartGenerator:LoadGraphicsHook");
    }

    private static bool Prefix()
    {
        return LoadingProgressWindow.CurrentStage == LoadingStage.Finished;
    }
}