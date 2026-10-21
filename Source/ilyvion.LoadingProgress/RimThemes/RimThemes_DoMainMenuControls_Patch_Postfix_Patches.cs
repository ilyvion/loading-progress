using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.RimThemes;

// RimThemes draws its theme selector button in the main menu's bottom-right corner, on top of the
// loading time label; this moves the button up so it sits above the label.
[HarmonyPatch]
internal static class RimThemes_DoMainMenuControls_Patch_Postfix_Patches
{
    private const float ThemeSelectorSize = 96f;

    private static readonly MethodInfo _method_Rect_get_height = AccessTools.PropertyGetter(
        typeof(Rect),
        nameof(Rect.height)
    );
    private static readonly MethodInfo _methodAdjustThemeSelectorY = AccessTools.Method(
        typeof(RimThemes_DoMainMenuControls_Patch_Postfix_Patches),
        nameof(AdjustThemeSelectorY)
    );

    internal static bool Prepare() => RimThemesUtils.HasRimThemes;

    internal static MethodInfo TargetMethod() =>
        AccessTools.Method("aRandomKiwi.RimThemes.DoMainMenuControls_Patch:Postfix");

    private static float AdjustThemeSelectorY(float y) =>
        VersionControl_DrawInfoInCorner_Patch.CornerLabelTop() is float labelTop
            ? ComputeThemeSelectorY(y, labelTop)
            : y;

    internal static float ComputeThemeSelectorY(float y, float labelTop) =>
        Math.Min(y, labelTop - GenUI.Gap - ThemeSelectorSize);

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        var originalInstructionList = instructions.ToList();

        var codeMatcher = new CodeMatcher(originalInstructionList, generator);

        // The button's y: `position.height - 96f`
        _ = codeMatcher.MatchEndForward(
            new CodeMatch(OpCodes.Call, _method_Rect_get_height),
            new CodeMatch(OpCodes.Ldc_R4, ThemeSelectorSize),
            new CodeMatch(OpCodes.Sub)
        );
        if (!codeMatcher.IsValid)
        {
            LoadingProgressMod.Error(
                $"Could not patch RimThemes' DoMainMenuControls_Patch.Postfix, IL does not match expectations ([call Rect.get_height, ldc.r4 96, sub])"
            );
            return originalInstructionList;
        }

        _ = codeMatcher.Advance(1).Insert([new(OpCodes.Call, _methodAdjustThemeSelectorY)]);

        return codeMatcher.Instructions();
    }
}
