using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.ErrorCheckPatches))]
[HarmonyPatchCategory("StartupImpact")]
internal static class LoadedModManager_ErrorCheckPatches_Patches
{
#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.SearchForward(i => i.Calls(AccessTools.PropertyGetter(typeof(List<ModContentPack>.Enumerator), nameof(IEnumerator.Current))));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LoadedModManager.ErrorCheckPatches: Could not find a call to IEnumerator.Current.");
            return original;
        }

        _ = codeMatcher.Advance(2).InsertAndAdvance([
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, AccessTools.Method(typeof(LoadedModManager_ErrorCheckPatches_Patches), nameof(BeforeErrorCheckPatches))),
            ]);

        _ = codeMatcher.SearchForward(i => i.Calls(AccessTools.Method(typeof(List<ModContentPack>.Enumerator), nameof(IEnumerator.MoveNext))));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LoadedModManager.ErrorCheckPatches: Could not find a call to IEnumerator.MoveNext.");
            return original;
        }
        _ = codeMatcher.Advance(1).InsertAndAdvance([
            new(OpCodes.Ldloc_1),
            new(OpCodes.Call, AccessTools.Method(typeof(LoadedModManager_ErrorCheckPatches_Patches), nameof(AfterErrorCheckPatches))),
        ]);

        return codeMatcher.Instructions();
    }

    private static void BeforeErrorCheckPatches(ModContentPack modContentPack)
        => StartupImpactProfilerUtil.StartModProfiler(modContentPack, "LoadingProgress.StartupImpact.ErrorCheckPatches");

    private static void AfterErrorCheckPatches(ModContentPack modContentPack)
        => StartupImpactProfilerUtil.StopModProfiler(modContentPack, "LoadingProgress.StartupImpact.ErrorCheckPatches");
}
