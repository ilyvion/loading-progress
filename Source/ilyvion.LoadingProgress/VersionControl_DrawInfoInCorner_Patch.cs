using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(VersionControl), "DrawInfoInCorner")]
internal static class VersionControl_DrawInfoInCorner_Patch
{
    private const float CornerMargin = 10f;

    // The loading time shown in the main menu's corner, or null while it isn't shown there.
    internal static string? CornerLoadingTimeText =>
        LoadingProgressMod.Settings.ShowLastLoadingTimeInCorner
        && Current.ProgramState == ProgramState.Entry
            ? LoadingProgressWindow.LoadingTimeText
            : null;

    internal static void Finalizer()
    {
        if (CornerLoadingTimeText is not { } text)
        {
            return;
        }

        var rect = new Rect(UI.screenWidth - CornerMargin, UI.screenHeight - CornerMargin, 0, 0);
        DrawLoadingTime(rect, text);
    }

    // The y coordinate of the top of the loading time label in the main menu's corner, or null
    // while it isn't shown there.
    internal static float? CornerLabelTop()
    {
        if (CornerLoadingTimeText is not { } text)
        {
            return null;
        }

        var font = Text.Font;
        Text.Font = GameFont.Small;
        var height = Text.CalcSize(text).y;
        Text.Font = font;
        return ComputeCornerLabelTop(UI.screenHeight, height);
    }

    internal static float ComputeCornerLabelTop(float screenHeight, float labelHeight) =>
        screenHeight - CornerMargin - labelHeight;

    internal static void DrawLoadingTime(Rect rect, string text)
    {
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
