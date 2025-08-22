using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.ClearCachedPatches))]
[HarmonyPatchCategory("StartupImpact")]
internal static class LoadedModManager_ClearCachedPatches_Patches
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.SearchForward(i => i.opcode == OpCodes.Call && i.operand is MethodInfo method && method.Name == "get_Current");
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LoadedModManager.ClearCachedPatches: Could not find a call to IEnumerator.Current.");
            return original;
        }

        _ = codeMatcher.Advance(2).InsertAndAdvance([
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, AccessTools.Method(typeof(LoadedModManager_ClearCachedPatches_Patches), nameof(BeforeClearCachedPatches))),
            ]);

        _ = codeMatcher.SearchForward(i => i.Calls(AccessTools.Method(typeof(List<ModContentPack>.Enumerator), nameof(IEnumerator.MoveNext))));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LoadedModManager.ClearCachedPatches: Could not find a call to IEnumerator.MoveNext.");
            return original;
        }
        _ = codeMatcher.Advance(1).InsertAndAdvance([
            new(OpCodes.Ldloc_1),
            new(OpCodes.Call, AccessTools.Method(typeof(LoadedModManager_ClearCachedPatches_Patches), nameof(AfterClearCachedPatches))),
        ]);

        return codeMatcher.Instructions();
    }

    private static void BeforeClearCachedPatches(ModContentPack modContentPack)
        => StartupImpactProfilerUtil.StartModProfiler(modContentPack, "LoadingProgress.StartupImpact.ClearCachedPatches");

    private static void AfterClearCachedPatches(ModContentPack modContentPack)
        => StartupImpactProfilerUtil.StopModProfiler(modContentPack, "LoadingProgress.StartupImpact.ClearCachedPatches");
}
