namespace ilyvion.LoadingProgress.StartupImpact.Patches;

internal static partial class PlayDataLoader_ResetStaticDataPre_Patches
{
    static partial void AfterPostfix() => StartupImpactProfilerUtil.StartBaseGameProfiler("LoadingProgress.StartupImpact.ResolveReferences");
}

// We can't profile the "resolve references" stage very accurately because it involves types with generics,
// but since it happens between ResetStaticDataPre and GenerateImpliedDefs_PostResolve, we'll just pretend
// that's the scope of that stage.

internal static partial class DefGenerator_GenerateImpliedDefs_PostResolve_Patches
{
    static partial void BeforePrefix() => StartupImpactProfilerUtil.StopBaseGameProfiler("LoadingProgress.StartupImpact.ResolveReferences");
}
