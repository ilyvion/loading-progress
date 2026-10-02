using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(VersionControl), "DrawInfoInCorner")]
internal static class VersionControl_DrawInfoInCorner_Patch
{
    internal static void Finalizer()
    {
        if (!LoadingProgressMod.Settings.ShowLastLoadingTimeInCorner)
        {
            return;
        }

        if (Current.ProgramState != ProgramState.Entry)
        {
            // You are not in the main menu
            return;
        }

        if (LoadingProgressWindow.CurrentLoadingTime is not { } loadingTime)
        {
            // This launch's loading time wasn't recorded
            return;
        }

        var rect = new Rect(UI.screenWidth - 10f, UI.screenHeight - 10f, 0, 0);
        DrawLoadingTime(rect, loadingTime);
    }

    internal static void DrawLoadingTime(Rect rect, TimeSpan loadingTime)
    {
        string text = "LoadingProgress.LoadingTime".Translate(
            Utilities.FormatDuration(loadingTime)
        );
        Text.Font = GameFont.Small;
        var vector = Text.CalcSize(text);
        rect.x -= vector.x;
        rect.y -= vector.y;
        rect.width += vector.x;
        rect.height += vector.y;
        LabelOutline(rect, text, Color.white, Color.black.ToTransparent(0.5f));
        if (Mouse.IsOver(rect))
        {
            var tip = new TipSignal("LoadingProgress.LoadingTime.Tip".Translate());
            TooltipHandler.TipRegion(rect, tip);
            Widgets.DrawHighlight(rect);
        }
        if (Widgets.ButtonInvisible(rect))
        {
            Find.WindowStack.Add(new DialogStartupImpact());
        }
    }

    private static void LabelOutline(Rect rect, string label, Color textColor, Color outlineColor)
    {
        int[] offsets = [-2, 0, 2];

        GUI.color = outlineColor;
        foreach (var xOffset in offsets)
        {
            foreach (var yOffset in offsets)
            {
                var offsetIcon = rect;
                offsetIcon.x += xOffset;
                offsetIcon.y += yOffset;
                Widgets.Label(offsetIcon, label);
            }
        }

        GUI.color = textColor;
        Widgets.Label(rect, label);
    }
}
