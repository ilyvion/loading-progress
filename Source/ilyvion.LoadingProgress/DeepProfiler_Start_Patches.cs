namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(DeepProfiler), nameof(DeepProfiler.Start))]
internal static class DeepProfiler_Start_Patches
{
    private static void Prefix(string label)
    {
        LoadingProgressWindow.CurrentLoadingActivity = label;
    }
}