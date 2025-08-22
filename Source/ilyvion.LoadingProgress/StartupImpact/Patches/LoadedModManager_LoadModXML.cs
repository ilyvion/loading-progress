using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(typeof(LoadedModManager), nameof(LoadedModManager.LoadModXML))]
[HarmonyPatchCategory("StartupImpact")]
internal static class LoadedModManager_LoadModXML
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.SearchForward(i => i.Calls(AccessTools.Method(typeof(ModContentPack), nameof(ModContentPack.LoadDefs))));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LoadedLanguage.LoadMetadata: Could not find a call to ModContentPack.LoadDefs.");
            return original;
        }

        _ = codeMatcher.Advance(1).InsertAndAdvance([
                new(OpCodes.Ldloc_2),
                new(OpCodes.Call, AccessTools.Method(typeof(LoadedModManager_LoadModXML), nameof(BeforeLoadDefs))),
            ]).Advance(1).InsertAndAdvance([
                new(OpCodes.Ldloc_2),
                new(OpCodes.Call, AccessTools.Method(typeof(LoadedModManager_LoadModXML), nameof(AfterLoadDefs))),
            ]);

        return codeMatcher.Instructions();
    }

    private static void BeforeLoadDefs(ModContentPack modContentPack)
        => StartupImpactProfilerUtil.StartModProfiler(modContentPack, "LoadingProgress.StartupImpact.LoadDefs");

    private static void AfterLoadDefs(ModContentPack modContentPack)
        => StartupImpactProfilerUtil.StopModProfiler(modContentPack, "LoadingProgress.StartupImpact.LoadDefs");
}
