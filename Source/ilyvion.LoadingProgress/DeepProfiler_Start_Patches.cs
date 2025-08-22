using System.Diagnostics;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(DeepProfiler), nameof(DeepProfiler.Start))]
internal static class DeepProfiler_Start_Patches
{
    private static void Prefix(string label)
    {
        if (label == null)
        {
            MethodBase method = new StackTrace().GetFrame(2).GetMethod();
            var mod = Utilities.FindModByAssembly(method.DeclaringType.Assembly);
            LoadingProgressMod.Warning($"Why is {method.DeclaringType.FullName}.{method.Name} from {mod?.Name ?? "{unknown}"} calling DeepProfiler.Start (and by extension our patch) with null?! Stop it.");
        }
        else
        {
            LoadingProgressWindow.CurrentLoadingActivity = label;
        }
    }
}
