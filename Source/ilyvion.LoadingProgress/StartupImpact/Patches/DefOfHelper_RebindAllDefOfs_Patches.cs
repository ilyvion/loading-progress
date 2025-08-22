namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(typeof(DefOfHelper), nameof(DefOfHelper.RebindAllDefOfs))]
[HarmonyPatchCategory("StartupImpact")]
internal static class DefOfHelper_RebindAllDefOfs_Patches
{
    internal static void Prefix(bool earlyTryMode)
    {
        if (earlyTryMode)
        {
            StartupImpactProfilerUtil.StartBaseGameProfiler("LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Early");
        }
        else
        {
            StartupImpactProfilerUtil.StartBaseGameProfiler("LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Final");
        }
    }

    internal static void Postfix(bool earlyTryMode)
    {
        if (earlyTryMode)
        {
            StartupImpactProfilerUtil.StopBaseGameProfiler("LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Early");
        }
        else
        {
            StartupImpactProfilerUtil.StopBaseGameProfiler("LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Final");
        }
    }
}
