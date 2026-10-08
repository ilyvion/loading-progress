namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(typeof(DefOfHelper), nameof(DefOfHelper.RebindAllDefOfs))]
[HarmonyPatchCategory("StartupImpact")]
internal static class DefOfHelper_RebindAllDefOfs_Patches
{
    internal static void Prefix(bool earlyTryMode, out bool __state)
    {
        if (earlyTryMode)
        {
            StartupImpactProfilerUtil.StartBaseGameProfiler(
                "LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Early"
            );
        }
        else
        {
            StartupImpactProfilerUtil.StartBaseGameProfiler(
                "LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Final"
            );
        }
        __state = true;
    }

    internal static void Postfix(bool earlyTryMode, ref bool __state)
    {
        Stop(earlyTryMode);
        __state = false;
    }

    // A postfix does not run when the method throws, so the finalizer closes the category then.
    internal static void Finalizer(bool earlyTryMode, bool __state)
    {
        if (__state)
        {
            Stop(earlyTryMode);
        }
    }

    private static void Stop(bool earlyTryMode)
    {
        if (earlyTryMode)
        {
            StartupImpactProfilerUtil.StopBaseGameProfiler(
                "LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Early"
            );
        }
        else
        {
            StartupImpactProfilerUtil.StopBaseGameProfiler(
                "LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Final"
            );
        }
    }
}
