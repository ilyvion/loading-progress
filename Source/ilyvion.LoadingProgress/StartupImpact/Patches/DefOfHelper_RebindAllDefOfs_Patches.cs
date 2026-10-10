namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(typeof(DefOfHelper), nameof(DefOfHelper.RebindAllDefOfs))]
[HarmonyPatchCategory("StartupImpact")]
internal static class DefOfHelper_RebindAllDefOfs_Patches
{
    internal static void Prefix(bool earlyTryMode, out bool __state)
    {
        StartupImpactProfilerUtil.StartBaseGameProfiler(Category(earlyTryMode));
        __state = true;
    }

    internal static void Postfix(bool earlyTryMode, ref bool __state) =>
        StartupImpactProfilerUtil.StopBaseGameOnce(ref __state, Category(earlyTryMode));

    // A postfix does not run when the method throws, so the finalizer closes the category then.
    internal static void Finalizer(bool earlyTryMode, bool __state) =>
        StartupImpactProfilerUtil.StopBaseGameOnce(ref __state, Category(earlyTryMode));

    private static string Category(bool earlyTryMode) =>
        earlyTryMode
            ? "LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Early"
            : "LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Final";
}
