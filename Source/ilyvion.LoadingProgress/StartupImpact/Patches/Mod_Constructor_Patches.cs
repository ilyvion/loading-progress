namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("StartupImpact")]
internal static class Mod_Constructor_Patches
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var x in typeof(Mod).InstantiableDescendantsAndSelf())
        {
            ConstructorInfo? constructorInfo = null;
            try
            {
                constructorInfo = AccessTools.Constructor(x, [typeof(ModContentPack)]);
            }
            catch (System.Exception)
            {
                LoadingProgressMod.Warning($"Mod constructor patch failed for {x.FullName}. This means Loading Progress can't track its loading time impact.");
            }
            if (constructorInfo != null)
            {
                yield return constructorInfo;
            }
        }
    }

    internal static void Prefix([HarmonyArgument(0)] ModContentPack modContentPack)
        => StartupImpactProfilerUtil.StartModProfiler(modContentPack, "LoadingProgress.StartupImpact.ModConstructor");

    internal static void Postfix([HarmonyArgument(0)] ModContentPack modContentPack)
        => StartupImpactProfilerUtil.StopModProfiler(modContentPack, "LoadingProgress.StartupImpact.ModConstructor");
}
