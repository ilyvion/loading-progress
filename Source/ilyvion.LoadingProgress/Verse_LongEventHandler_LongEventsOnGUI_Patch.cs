using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.DrawLongEventWindowContents))]
internal class Verse_LongEventHandler_DrawLongEventWindowContents_Patch
{
    private static void Postfix()
    {
        if (LoadingProgressWindow.CurrentStage == LoadingStage.Finished)
        {
            return;
        }

        Vector2 windowSize = LoadingProgressWindow.WindowSize;
        Vector2 offset2 = new((UI.screenWidth - windowSize.x) / 2f, (UI.screenHeight - windowSize.y) / 2f);
        Rect rect = new(offset2.x, offset2.y, LoadingProgressWindow.WindowSize.x, LoadingProgressWindow.WindowSize.y);

        bool useStandardWindow = LongEventHandler.currentEvent.UseStandardWindow;
        if (!useStandardWindow || Find.UIRoot == null || Find.WindowStack == null)
        {
            Widgets.DrawShadowAround(rect);
            Widgets.DrawWindowBackground(rect);
            LoadingProgressWindow.DrawContents(rect);
        }
        else
        {
            LoadingProgressWindow.DrawWindow(rect);
        }
    }
}

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsOnGUI))]
internal class Verse_LongEventHandler_LongEventsOnGUI_Patch
{
    private static readonly MethodInfo _method_GenUI_Rounded = AccessTools.Method(typeof(GenUI), nameof(GenUI.Rounded), [typeof(Rect)]);
    private static readonly MethodInfo _methodAdjustStatusWindowRect = AccessTools.Method(typeof(Verse_LongEventHandler_LongEventsOnGUI_Patch), nameof(Verse_LongEventHandler_LongEventsOnGUI_Patch.AdjustStatusWindowRect));

    private static Rect AdjustStatusWindowRect(Rect r)
    {
        if (LoadingProgressWindow.CurrentStage == LoadingStage.Finished)
        {
            return r;
        }

        Vector2 statusRectSize = LongEventHandler.StatusRectSize;
        Vector2 windowSize = LoadingProgressWindow.WindowSize;
        var statusRectTop = ((UI.screenHeight - windowSize.y) / 2f) - statusRectSize.y - 10f;
        r.y = statusRectTop;
        return r;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var originalInstructionList = instructions.ToList();

        var codeMatcher = new CodeMatcher(originalInstructionList, generator);

        _ = codeMatcher.SearchForward(i => i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == _method_GenUI_Rounded);
        if (!codeMatcher.IsValid)
        {
            LoadingProgressMod.Error($"Could not patch LongEventHandler.LongEventsOnGUI, IL does not match expectations ([call GenUI.Rounded])");
            return originalInstructionList;
        }

        _ = codeMatcher.Advance(1).Insert([
            new(OpCodes.Call, _methodAdjustStatusWindowRect)
        ]);

        return codeMatcher.Instructions();
    }
}
