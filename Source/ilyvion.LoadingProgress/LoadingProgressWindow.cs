using UnityEngine;

namespace ilyvion.LoadingProgress;

public partial class LoadingProgressWindow
{
    internal static object windowLock = new();

    internal static readonly Vector2 WindowSize = new(776f, 110f);

    internal static void DrawWindow(Rect statusRect)
    {
        Find.WindowStack.ImmediateWindow(62893994, statusRect, WindowLayer.Super, delegate
        {
            DrawContents(statusRect.AtZero());
        });
    }

    internal static void DrawContents(Rect rect)
    {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.UpperLeft;
        float num2 = 17f;
        float num3 = num2 + 4f;
        Rect rect3 = rect;
        rect3.x += num3;
        rect3.y += 10f;
        rect3.width -= 2 * num3;

        Widgets.Label(rect3, "Loading progress");
        rect3.yMin += Text.LineHeight + 4f;

        Text.Font = GameFont.Small;

        lock (windowLock)
        {
            var rule = StageRules.Find(r => r.Stage == CurrentStage);
            if (rule is not null && rule.DisplayLabel is not null)
            {
                var label = rule.DisplayLabel(_currentLoadingActivity);
                if (!string.IsNullOrEmpty(label))
                {
                    Widgets.Label(rect3, label);
                }
            }
            // else: do nothing for Finished/default

            DrawHorizontalProgressBar(
                new Rect(rect3.x, rect3.y + Text.LineHeight + 4f, rect3.width, 20f),
                (int)CurrentStage,
                (int)LoadingStage.Finished);
        }

        Text.Anchor = TextAnchor.UpperLeft;
    }

    protected static void DrawHorizontalProgressBar(
        Rect progressRect,
        float currentValue,
        float maxValue)
    {
        // draw a box for the bar
        GUI.color = Color.gray;
        Widgets.DrawBox(progressRect.ContractedBy(1f));
        GUI.color = Color.white;

        // get the bar rect
        var barRect = progressRect.ContractedBy(2f);
        var unit = barRect.width / maxValue;
        barRect.width = currentValue * unit;

        // draw the bar
        Widgets.DrawBoxSolid(barRect, BarColor);
    }

    private static readonly Color BarColor = new(0.2f, 0.8f, 0.85f);
}
