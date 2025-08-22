using System.Reflection.Emit;

namespace ilyvion.LoadingProgress;

// This patch makes it so that even though we're loaded before Core, the game will use language
// data from Core. We do this by just skipping this mod when it's looking for the first mod
// with a language folder for the given language.
[HarmonyPatch(typeof(LoadedLanguage), nameof(LoadedLanguage.LoadMetadata))]
internal static class LoadedLanguage_LoadMetadata_Patches
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.SearchForward(i => i.Calls(AccessTools.PropertyGetter(typeof(IEnumerator<ModContentPack>), nameof(IEnumerator.Current))));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LoadedLanguage.LoadMetadata: Could not find a call to IEnumerator.Current.");
            return original;
        }

        var remember = codeMatcher.Pos;
        _ = codeMatcher.SearchBackwards(i => i.opcode == OpCodes.Br);
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LoadedLanguage.LoadMetadata: Could not find expected opcode [br].");
            return original;
        }
        var continueTarget = codeMatcher.Operand;

        _ = codeMatcher.Start().Advance(remember).Advance(1);

        _ = codeMatcher.CreateLabel(out var proceedTarget).Insert([
                new(OpCodes.Dup),
                new(OpCodes.Call, AccessTools.Method(typeof(LoadedLanguage_LoadMetadata_Patches), nameof(IsThisMod))),
                new(OpCodes.Brfalse, proceedTarget),
                new(OpCodes.Pop),
                new(OpCodes.Br, continueTarget)
            ]);

        return codeMatcher.Instructions();
    }

    private static bool IsThisMod(ModContentPack mod) => mod == LoadingProgressMod.instance.Content;
}
