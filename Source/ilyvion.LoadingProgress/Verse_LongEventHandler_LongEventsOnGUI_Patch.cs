using System.Reflection.Emit;
using ilyvion.LoadingProgress.FasterGameLoading;

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

        Vector2 loadingProgressWindowSize = LoadingProgressWindow.WindowSize;
        Vector2 fasterGameLoadingProgressWindowSize = FasterGameLoadingProgressWindow.WindowSize;
        float loadingProgressWindowOffset = 0f;
        switch (LoadingProgressMod.Settings.LoadingWindowPlacement)
        {
            case LoadingWindowPlacement.Top:
                loadingProgressWindowOffset = 10f + LongEventHandler.StatusRectSize.y + 10f;
                break;
            case LoadingWindowPlacement.Middle:
                loadingProgressWindowOffset = (UI.screenHeight - loadingProgressWindowSize.y - fasterGameLoadingProgressWindowSize.y) / 2f;
                break;
            case LoadingWindowPlacement.Bottom:
                loadingProgressWindowOffset = UI.screenHeight - loadingProgressWindowSize.y - fasterGameLoadingProgressWindowSize.y - 10f - (fasterGameLoadingProgressWindowSize.y > 0 ? 10f : 0f);
                break;
            case LoadingWindowPlacement.Custom:
                // Custom logic can be added here if needed
                break;
            default:
                break;
        }

        Vector2 loadingProgressWindowPosition = new((UI.screenWidth - loadingProgressWindowSize.x) / 2f, loadingProgressWindowOffset);
        Rect rect = new(loadingProgressWindowPosition.x, loadingProgressWindowPosition.y, loadingProgressWindowSize.x, loadingProgressWindowSize.y);

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

        Vector2 fasterGameLoadingProgressWindowPosition = new((UI.screenWidth - fasterGameLoadingProgressWindowSize.x) / 2f, rect.yMax + 10f);
        rect = new(fasterGameLoadingProgressWindowPosition.x, fasterGameLoadingProgressWindowPosition.y, fasterGameLoadingProgressWindowSize.x, fasterGameLoadingProgressWindowSize.y);
        if (!useStandardWindow || Find.UIRoot == null || Find.WindowStack == null)
        {
            Widgets.DrawShadowAround(rect);
            Widgets.DrawWindowBackground(rect);
            FasterGameLoadingProgressWindow.DrawContents(rect);
        }
        else
        {
            FasterGameLoadingProgressWindow.DrawWindow(rect);
        }
    }
}

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsOnGUI))]
internal class Verse_LongEventHandler_LongEventsOnGUI_Patch
{
    private static readonly MethodInfo _method_GenUI_Rounded = AccessTools.Method(typeof(GenUI), nameof(GenUI.Rounded), [typeof(Rect)]);
    private static readonly MethodInfo _methodAdjustStatusWindowRect = AccessTools.Method(typeof(Verse_LongEventHandler_LongEventsOnGUI_Patch), nameof(AdjustStatusWindowRect));

    private static Rect AdjustStatusWindowRect(Rect r)
    {
        if (LoadingProgressWindow.CurrentStage == LoadingStage.Finished)
        {
            return r;
        }

        Vector2 statusRectSize = LongEventHandler.StatusRectSize;
        Vector2 loadingProgressWindowSize = LoadingProgressWindow.WindowSize;
        Vector2 fasterGameLoadingProgressWindowSize = FasterGameLoadingProgressWindow.WindowSize;

        float statusRectTop = 0; ;
        switch (LoadingProgressMod.Settings.LoadingWindowPlacement)
        {
            case LoadingWindowPlacement.Top:
                statusRectTop = 10f;
                break;
            case LoadingWindowPlacement.Middle:
                statusRectTop = ((UI.screenHeight - loadingProgressWindowSize.y - fasterGameLoadingProgressWindowSize.y) / 2f) - statusRectSize.y - 10f;
                break;
            case LoadingWindowPlacement.Bottom:
                statusRectTop = UI.screenHeight - loadingProgressWindowSize.y - fasterGameLoadingProgressWindowSize.y - 10f - (fasterGameLoadingProgressWindowSize.y > 0 ? 10f : 0f) - statusRectSize.y - 10f;
                break;
            case LoadingWindowPlacement.Custom:
                // Custom logic can be added here if needed
                break;
            default:
                break;
        }
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
