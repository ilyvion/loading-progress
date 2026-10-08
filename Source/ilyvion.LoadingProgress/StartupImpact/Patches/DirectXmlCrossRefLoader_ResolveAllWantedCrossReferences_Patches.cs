namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(
    typeof(DirectXmlCrossRefLoader),
    nameof(DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences)
)]
[HarmonyPatchCategory("StartupImpact")]
internal static class DirectXmlCrossRefLoader_ResolveAllWantedCrossReferences_Patches
{
    internal static void Prefix(FailMode failReportMode, out bool __state)
    {
        __state = false;
        if (LoadingProgressWindow.CurrentStage != LoadingStage.Finished)
        {
            switch (failReportMode)
            {
                case FailMode.Silent:
                    StartupImpactProfilerUtil.StartBaseGameProfiler(
                        "LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.NonImplied"
                    );
                    __state = true;
                    break;

                case FailMode.LogErrors:
                    StartupImpactProfilerUtil.StartBaseGameProfiler(
                        "LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.Implied"
                    );
                    __state = true;
                    break;
                default:
                    LoadingProgressMod.Warning(
                        $"Unknown fail report mode used with DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences: {failReportMode}"
                    );
                    break;
            }
        }
    }

    internal static void Postfix(FailMode failReportMode, ref bool __state)
    {
        if (__state)
        {
            Stop(failReportMode);
            __state = false;
        }
    }

    // A postfix does not run when the method throws, so the finalizer closes the category then.
    internal static void Finalizer(FailMode failReportMode, bool __state)
    {
        if (__state)
        {
            Stop(failReportMode);
        }
    }

    // Stops the category the prefix started, which it did only for these two modes.
    private static void Stop(FailMode failReportMode) =>
        StartupImpactProfilerUtil.StopBaseGameProfiler(
            failReportMode == FailMode.Silent
                ? "LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.NonImplied"
                : "LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.Implied"
        );
}
