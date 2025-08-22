using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.ApplyPatches))]
[HarmonyPatchCategory("StartupImpact")]
internal static class LoadedModManager_ApplyPatches_Patches
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.SearchForward(i => i.Calls(AccessTools.PropertyGetter(typeof(IEnumerator<PatchOperation>), nameof(IEnumerator.Current))));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LoadedModManager.ApplyPatches: Could not find a call to IEnumerator.Current.");
            return original;
        }

        _ = codeMatcher.Advance(2).InsertAndAdvance([
                new(OpCodes.Ldloc_1),
                new(OpCodes.Call, AccessTools.Method(typeof(LoadedModManager_ApplyPatches_Patches), nameof(BeforeApplyPatches))),
            ]);

        _ = codeMatcher.SearchForward(i => i.Calls(AccessTools.Method(typeof(IEnumerator), nameof(IEnumerator.MoveNext))));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LoadedModManager.ApplyPatches: Could not find a call to IEnumerator.MoveNext.");
            return original;
        }
        _ = codeMatcher.Advance(1).InsertAndAdvance([
            new(OpCodes.Ldloc_1),
            new(OpCodes.Call, AccessTools.Method(typeof(LoadedModManager_ApplyPatches_Patches), nameof(AfterApplyPatches))),
        ]);

        return codeMatcher.Instructions();
    }

    private static void BeforeApplyPatches(PatchOperation patchOperation)
    {
        if (patchOperation == null)
        {
            return;
        }

        if (!ModContentPack_LoadPatches_Patches.modContentPackTable.TryGetValue(patchOperation, out var modContentPack))
        {
            LoadingProgressMod.Error("LoadedModManager.BeforeApplyPatches: Could not find mod content pack for " + patchOperation?.ToString());
            return;
        }
        StartupImpactProfilerUtil.StartModProfiler(modContentPack, "LoadingProgress.StartupImpact.ApplyPatches");
    }

    private static void AfterApplyPatches(PatchOperation patchOperation)
    {
        if (patchOperation == null)
        {
            return;
        }

        if (!ModContentPack_LoadPatches_Patches.modContentPackTable.TryGetValue(patchOperation, out var modContentPack))
        {
            LoadingProgressMod.Error("LoadedModManager.AfterApplyPatches: Could not find mod content pack for " + patchOperation?.ToString());
            return;
        }
        StartupImpactProfilerUtil.StopModProfiler(modContentPack, "LoadingProgress.StartupImpact.ApplyPatches");
    }
}
