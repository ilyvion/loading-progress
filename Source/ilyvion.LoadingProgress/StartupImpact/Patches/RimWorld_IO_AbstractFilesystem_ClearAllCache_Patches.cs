namespace ilyvion.LoadingProgress.StartupImpact.Patches;

internal static partial class RimWorld_IO_AbstractFilesystem_ClearAllCache_Patches
{
    static partial void AfterPostfix()
    {
        LoadingProgressMod.instance.StartupImpact.FinishLoading();
    }
}
