namespace ilyvion.LoadingProgress;

public static class Widgets_Progressbar
{
    public static void DrawHorizontalProgressBar(
        Rect progressRect,
        float currentValue,
        float maxValue,
        float? smallCurrentValue = null,
        float? smallMaxValue = null,
        Color? customBarColor = null,
        Color? customSmallBarColor = null)
    {
        // draw a box for the bar
        GUI.color = Color.gray;
        Widgets.DrawBox(progressRect.ContractedBy(1f));
        GUI.color = Color.white;

        // get the bar rect
        var barRect = progressRect.ContractedBy(2f);
        var unit = barRect.width / maxValue;
        barRect.width = currentValue * unit;

        if (smallCurrentValue.HasValue && smallMaxValue.HasValue)
        {
            smallCurrentValue = smallCurrentValue.Value % (2 * smallMaxValue.Value);

            // draw the small bar
            var smallBarRect = progressRect.ContractedBy(2f);
            smallBarRect.yMin += barRect.height / 2;
            var smallUnit = smallBarRect.width / smallMaxValue.Value;
            smallBarRect.width = smallCurrentValue.Value * smallUnit;

            float clampedSmallCurrentValue = Mathf.Clamp(smallCurrentValue.Value, 0f, smallMaxValue.Value);
            if (smallCurrentValue.Value > smallMaxValue.Value)
            {
                // Once we're past max, start drawing the bar with a gap on the left side equal to the overflow.
                smallBarRect.width = smallMaxValue.Value * smallUnit;
                smallBarRect.xMin += (smallCurrentValue.Value - smallMaxValue.Value) * smallUnit;
            }

            if (currentValue < maxValue)
            {
                // draw the big/main bar with "internal" progress
                barRect.width += clampedSmallCurrentValue / smallMaxValue.Value * unit;
            }
            Widgets.DrawBoxSolid(barRect, customBarColor ?? BarColor);

            // draw the small bar
            Widgets.DrawBoxSolid(smallBarRect, customSmallBarColor ?? SmallBarColor);
        }
        else
        {
            // draw the big/main bar
            Widgets.DrawBoxSolid(barRect, customBarColor ?? BarColor);
        }
    }

    public static readonly Color BarColor = new(0.2f, 0.8f, 0.85f);
    public static readonly Color SmallBarColor = Color.white.ToTransparent(0.75f);
}
