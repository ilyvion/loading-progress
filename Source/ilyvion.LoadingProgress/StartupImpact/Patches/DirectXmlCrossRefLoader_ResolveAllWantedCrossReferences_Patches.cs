namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(typeof(DirectXmlCrossRefLoader), nameof(DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences))]
[HarmonyPatchCategory("StartupImpact")]
internal static class DirectXmlCrossRefLoader_ResolveAllWantedCrossReferences_Patches
{
    internal static void Prefix(FailMode failReportMode)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.Finished)
        {
            switch (failReportMode)
            {
                case FailMode.Silent:
                    StartupImpactProfilerUtil.StartBaseGameProfiler("LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.NonImplied");
                    break;

                case FailMode.LogErrors:
                    StartupImpactProfilerUtil.StartBaseGameProfiler("LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.Implied");
                    break;
                default:
                    LoadingProgressMod.Warning($"Unknown fail report mode used with DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences: {failReportMode}");
                    break;
            }
        }
    }

    internal static void Postfix(FailMode failReportMode)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.Finished)
        {
            switch (failReportMode)
            {
                case FailMode.Silent:
                    StartupImpactProfilerUtil.StopBaseGameProfiler("LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.NonImplied");
                    break;

                case FailMode.LogErrors:
                    StartupImpactProfilerUtil.StopBaseGameProfiler("LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.Implied");
                    break;
                default:
                    LoadingProgressMod.Warning($"Unknown fail report mode used with DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences: {failReportMode}");
                    break;
            }
        }
    }
}
