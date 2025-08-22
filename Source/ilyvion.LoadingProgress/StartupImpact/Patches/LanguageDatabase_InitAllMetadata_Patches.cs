using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch(typeof(LanguageDatabase), nameof(LanguageDatabase.InitAllMetadata))]
[HarmonyPatchCategory("StartupImpact")]
internal static class LanguageDatabase_InitAllMetadata_Patches
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var mcpLocal = generator.DeclareLocal(typeof(ModContentPack));

        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.SearchForward(i => i.Calls(AccessTools.PropertyGetter(typeof(IEnumerator<ModContentPack>), nameof(IEnumerator.Current))));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LanguageDatabase.InitAllMetadata: Could not find a call to IEnumerator.Current.");
            return original;
        }

        _ = codeMatcher.Advance(1).InsertAndAdvance([
                new(OpCodes.Dup),
                new(OpCodes.Dup),
                new(OpCodes.Stloc, mcpLocal.LocalIndex),
                new(OpCodes.Call, AccessTools.Method(typeof(LanguageDatabase_InitAllMetadata_Patches), nameof(BeforeInitAllMetadata))),
            ]);

        _ = codeMatcher.SearchForward(i => i.Calls(AccessTools.Method(typeof(IEnumerator), nameof(IEnumerator.MoveNext))));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LanguageDatabase.InitAllMetadata: Could not find a call to IEnumerator.MoveNext.");
            return original;
        }
        _ = codeMatcher.Advance(1).InsertAndAdvance([
            new(OpCodes.Ldloc, mcpLocal.LocalIndex),
            new(OpCodes.Call, AccessTools.Method(typeof(LanguageDatabase_InitAllMetadata_Patches), nameof(AfterInitAllMetadata))),
        ]);

        return codeMatcher.Instructions();
    }

    private static void BeforeInitAllMetadata(ModContentPack modContentPack)
        => StartupImpactProfilerUtil.StartModProfiler(modContentPack, "LoadingProgress.StartupImpact.LanguageDatabaseInitAllMetadata");

    private static void AfterInitAllMetadata(ModContentPack modContentPack)
        => StartupImpactProfilerUtil.StopModProfiler(modContentPack, "LoadingProgress.StartupImpact.LanguageDatabaseInitAllMetadata");
}
