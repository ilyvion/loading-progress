namespace ilyvion.LoadingProgress.StartupImpact.Patches;

internal static partial class PlayDataLoader_ResetStaticDataPost_Patches
{
    static partial void AfterPostfix() => StartupImpactProfilerUtil.StartBaseGameProfiler("LoadingProgress.StartupImpact.ErrorCheckAllDefs");
}

// We can't profile the "error check all defs" stage very accurately because it involves types with generics,
// but since it happens between ResetStaticDataPost and KeyPrefs.Init, we'll just pretend
// that's the scope of that stage.

internal static partial class KeyPrefs_Init_Patches
{
    static partial void BeforePrefix() => StartupImpactProfilerUtil.StopBaseGameProfiler("LoadingProgress.StartupImpact.ErrorCheckAllDefs");
}
