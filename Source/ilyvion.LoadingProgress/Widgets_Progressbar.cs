namespace ilyvion.LoadingProgress;

internal static class Widgets_Progressbar
{
    public static void DrawHorizontalProgressBar(
        Rect progressRect,
        float currentValue,
        float maxValue,
        float? smallCurrentValue = null,
        float? smallMaxValue = null,
        Color? customBarColor = null,
        Color? customSmallBarColor = null
    )
    {
        // draw a box for the bar
        GUI.color = Color.gray;
        Widgets.DrawBox(progressRect.ContractedBy(1f));
        GUI.color = Color.white;

        // get the bar rect
        var barRect = progressRect.ContractedBy(2f);
        var unit = maxValue > 0f ? barRect.width / maxValue : 0f;
        barRect.width = currentValue * unit;

        if (smallCurrentValue.HasValue && smallMaxValue.HasValue && smallMaxValue.Value > 0f)
        {
            // The small bar's own value wraps every 2x its max, so a per-stage counter that
            // overshoots an underestimated max still animates as a repeating "lap" instead of
            // stopping dead at the right edge.
            var wrappedSmallCurrentValue = smallCurrentValue.Value % (2 * smallMaxValue.Value);

            // draw the small bar
            var smallBarRect = progressRect.ContractedBy(2f);
            smallBarRect.yMin += barRect.height / 2;
            var smallUnit = smallBarRect.width / smallMaxValue.Value;
            smallBarRect.width = wrappedSmallCurrentValue * smallUnit;

            if (wrappedSmallCurrentValue > smallMaxValue.Value)
            {
                // Once we're past max, start drawing the bar with a gap on the left side equal to the overflow.
                smallBarRect.width = smallMaxValue.Value * smallUnit;
                smallBarRect.xMin += (wrappedSmallCurrentValue - smallMaxValue.Value) * smallUnit;
            }

            if (currentValue < maxValue)
            {
                // The big/main bar's "internal" progress bonus is derived from the
                // un-wrapped value, clamped to a single lap, so it keeps climbing toward the
                // next stage instead of falling back every time the small bar's lap wraps
                // around.
                barRect.width +=
                    MainBarBonusFraction(smallCurrentValue.Value, smallMaxValue.Value) * unit;
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

    // Unlike the small bar's own display value, this never wraps back down: it's the fraction
    // of the current stage the main bar should visibly credit toward the next one, so it stays
    // pinned at 1 once the small bar's count reaches (or repeatedly overshoots) its max.
    internal static float MainBarBonusFraction(float smallCurrentValue, float smallMaxValue) =>
        smallMaxValue > 0f ? Math.Clamp(smallCurrentValue / smallMaxValue, 0f, 1f) : 0f;

    public static readonly Color BarColor = new(0.2f, 0.8f, 0.85f);
    public static readonly Color SmallBarColor = Color.white.ToTransparent(0.75f);
}
