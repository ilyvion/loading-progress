using UnityEngine;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(VersionControl), "DrawInfoInCorner")]
internal static class VersionControl_DrawInfoInCorner_Patch
{
    private static TimeSpan? _loadingTime;
    private static void Postfix()
    {
        if (!LoadingProgressMod.Settings.ShowLastLoadingTimeInCorner)
        {
            return;
        }

        _loadingTime ??= TimeSpan.FromSeconds(LoadingProgressMod.Settings.LastLoadingTime);
        string text = "LoadingProgress.LoadingTime".Translate(_loadingTime.Value.ToString("mm\\:ss"));
        Text.Font = GameFont.Small;
        var vector = Text.CalcSize(text);

        var rect = new Rect(UI.screenWidth - vector.x - 10f, UI.screenHeight - vector.y - 10f, vector.x, vector.y);
        LabelOutline(rect, text, Color.white, Color.black.ToTransparent(0.5f));
        if (Mouse.IsOver(rect))
        {
            var tip = new TipSignal("LoadingProgress.LoadingTime.Tip".Translate());
            TooltipHandler.TipRegion(rect, tip);
            Widgets.DrawHighlight(rect);
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
