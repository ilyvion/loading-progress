namespace ilyvion.LoadingProgress;

[HarmonyPatch]
internal static class AlienPartGenerator_LoadGraphicsHook_Patches
{
    internal static bool Prepare() => ModsConfig.ActiveModsInLoadOrder.Any(mod => mod.PackageId.Equals("erdelf.humanoidalienraces", StringComparison.OrdinalIgnoreCase));

    internal static MethodInfo TargetMethod() => AccessTools.Method("AlienRace.AlienPartGenerator:LoadGraphicsHook");

    internal static bool Prefix() => LoadingProgressWindow.CurrentStage == LoadingStage.Finished;
}
