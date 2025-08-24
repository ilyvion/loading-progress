using System.Globalization;

namespace ilyvion.LoadingProgress.StartupImpact;

[HotSwappable]
internal sealed class ProfilerBar
{
    public bool UseLogScale
    {
        get; set;
    }
    public float ProgressBarPadding { get; set; } = 4f;
    public Color DefaultColor { get; set; } = Color.gray;

    public static string TimeText(float ms) => ms > 10000
            ? "LoadingProgress.StartupImpact.Seconds".Translate((ms * 0.001f).ToString("F1", CultureInfo.InvariantCulture))
            : (ms > 1000
                ? "LoadingProgress.StartupImpact.Seconds".Translate((ms * 0.001f).ToString("F2", CultureInfo.InvariantCulture))
                : "LoadingProgress.StartupImpact.Milliseconds".Translate(ms));

    public void Draw(
        Rect rect,
        IReadOnlyList<float> metrics,
        IReadOnlyList<string> categories,
        float maxImpact,
        IReadOnlyDictionary<string, Color> categoryColors
    )
    {
        // Choose tau in the same units as the metrics: 1000 since we're using ms.
        var tau = 1000f;

        var innerX = rect.x + ProgressBarPadding;
        var innerY = rect.y + ProgressBarPadding;
        var innerW = rect.width - (2f * ProgressBarPadding);
        var innerH = rect.height - (2f * ProgressBarPadding);

        // Linear total (how "full" the bar should be)
        float sumLinear = 0;
        for (var i = 0; i < metrics.Count; i++)
        {
            sumLinear += Math.Max(0, metrics[i]);
        }

        if (sumLinear <= 0)
        {
            return; // nothing to draw
        }

        if (!UseLogScale)
        {
            DrawLinearScale(metrics, categories, maxImpact, categoryColors, innerX, innerY, innerW, innerH);
        }
        else
        {
            // Overall fill 0..1 in the SAME transform space
            var denomCap = LogScaleTransform(maxImpact, tau);
            var barFill = denomCap > 0f ? LogScaleTransform(sumLinear, tau) / denomCap : 0f;
            barFill = Mathf.Clamp01(barFill);

            DrawLogScale(metrics, categories, categoryColors, tau, innerX, innerY, innerW, innerH, barFill);
        }

        void DrawLinearScale(
            IReadOnlyList<float> metrics,
            IReadOnlyList<string> categories,
            float maxImpact,
            IReadOnlyDictionary<string, Color> categoryColors,
            float innerX,
            float innerY,
            float innerW,
            float innerH)
        {
            var x = innerX;
            for (var i = 0; i < categories.Count; i++)
            {
                var impact = metrics[i];
                if (impact <= 0)
                {
                    continue;
                }

                var width = innerW * impact / Mathf.Max(1f, maxImpact);
                var textRect = new Rect(x, innerY, width, innerH);

                var color = categoryColors.TryGetValue(categories[i], out var c) ? c : DefaultColor;
                DrawSegment(textRect, color);

                TooltipHandler.TipRegion(textRect, new TipSignal(
                    $"{StartupImpactProfilerUtil.TranslateCategory(categories[i])}: {TimeText(impact)}"));

                x += width;
            }
        }

        void DrawLogScale(
            IReadOnlyList<float> metrics,
            IReadOnlyList<string> categories,
            IReadOnlyDictionary<string, Color> categoryColors,
            float tau,
            float innerX,
            float innerY,
            float innerW,
            float innerH,
            float barFill)
        {
            // Shares that sum to 1 in the *original* category order
            var vals = categories.Select((_, i) => Mathf.Max(0, metrics[i])).ToArray();
            var shares = LogShares(vals, tau);

            var targetTotalWidth = innerW * barFill;

            // Snap the last non-zero to avoid rounding gaps
            var lastIdx = -1;
            for (var i = categories.Count - 1; i >= 0; i--)
            {
                if (metrics[i] > 0 && shares[i] > 0f)
                {
                    lastIdx = i;
                    break;
                }
            }

            var xCursor = innerX;
            var drawn = 0f;

            for (var i = 0; i < categories.Count; i++)
            {
                var impact = metrics[i];
                var share = shares[i];
                if (impact <= 0 || share <= 0f)
                {
                    continue;
                }

                var width = targetTotalWidth * share;
                if (i == lastIdx)
                {
                    width = Mathf.Max(0f, targetTotalWidth - drawn); // snap
                }

                var textRect = new Rect(xCursor, innerY, width, innerH);

                var color = categoryColors.TryGetValue(categories[i], out var c) ? c : DefaultColor;
                DrawSegment(textRect, color);

                TooltipHandler.TipRegion(textRect, new TipSignal(
                    $"{StartupImpactProfilerUtil.TranslateCategory(categories[i])}: {TimeText(impact)}"));

                xCursor += width;
                drawn += width;
            }
        }

        static void DrawSegment(Rect r, Color color)
        {
            var stored = GUI.color;
            var hover = Mouse.IsOver(r);

            if (hover)
            {
                GUI.color = Color.Lerp(color * stored, Color.white, 0.25f);
                if (r.width > 6f)
                {
                    GUI.DrawTexture(r, BaseContent.WhiteTex);
                    GUI.color = color * stored;
                    GUI.DrawTexture(GenUI.ContractedBy(r, 3f), BaseContent.WhiteTex);
                }
                else
                {
                    GUI.DrawTexture(r, BaseContent.WhiteTex);
                }
            }
            else
            {
                GUI.color = color * stored;
                GUI.DrawTexture(r, BaseContent.WhiteTex);
            }
            GUI.color = stored;
        }
    }

    /// <summary>
    /// Applies a log scaling transformation to the input value x, using tau as the scaling parameter.
    /// Used to compress large metric values for log-scale visualization of the profiler bar.
    /// </summary>
    private static float LogScaleTransform(float x, float tau) => Mathf.Log10(1f + (Mathf.Max(0f, x) / tau));

    // Hierarchical log split: greedy "largest vs rest" at each step.
    // Returns shares in the *same order* as the input values; shares sum to 1.
    //
    // Alternative implementation using hierarchical log splitting that maintains
    // a stronger "bigger value takes up more space" algorithm.
    // Currently not in use.
#pragma warning disable IDE0051 // Remove unused private members
    private static float[] HierLogShares(float[] values, float tau)
#pragma warning restore IDE0051 // Remove unused private members
    {
        // Number of input values
        var n = values.Length;
        if (n == 0)
        {
            // Return empty if no values
            return [];
        }

        // Pair each value with its original index, clamp to >= 0, and sort descending by value
        var items = values
            .Select((v, i) => (v: Mathf.Max(0f, v), i))
            .OrderByDescending(t => t.v)
            .ToArray();

        // Total sum of all values (after clamping)
        var total = items.Sum(t => t.v);
        // Fraction of the bar left to allocate (starts at 1, decreases as we assign shares)
        var leftover = 1f;

        // Will hold the shares in sorted order (largest to smallest)
        var sharesSorted = new float[n];

        // Greedily assign shares: at each step, split leftover between current largest and the rest
        for (var k = 0; k < n; k++)
        {
            var currentValue = items[k].v; // Current value (already clamped)
            var rest = Mathf.Max(0f, total - currentValue); // Sum of remaining values

            // Compute denominator for log split between currentValue and rest
            var denom = LogScaleTransform(currentValue, tau) + LogScaleTransform(rest, tau);
            // Fraction of leftover assigned to currentValue (log-proportional)
            var currentValueFraction = denom > 0f
                ? LogScaleTransform(currentValue, tau) / denom
                : (currentValue > 0f ? 1f : 0f);

            // Assign this share
            sharesSorted[k] = leftover * currentValueFraction;

            // Update leftover and total for next iteration
            leftover *= 1f - currentValueFraction;
            total -= currentValue;
        }

        // Snap last nonzero so all shares sum exactly to 1 (fixes rounding error)
        var lastNonZero = Array.FindLastIndex(sharesSorted, s => s > 0f);
        if (lastNonZero >= 0)
        {
            var sum = sharesSorted.Sum();
            sharesSorted[lastNonZero] += Mathf.Max(0f, 1f - sum);
        }

        // Map shares back to the original input order
        var shares = new float[n];
        for (var k = 0; k < n; k++)
        {
            shares[items[k].i] = sharesSorted[k];
        }

        return shares;
    }

    private static float[] LogShares(float[] values, float tau)
    {
        // Compute log-scaled values
        var shares = values.Select(v => LogScaleTransform(v, tau)).ToArray();
        var sum = shares.Sum();
        if (sum > 0f)
        {
            // Scale so that the sum is exactly 1
            for (var i = 0; i < shares.Length; i++)
            {
                shares[i] /= sum;
            }
            // Snap last nonzero so all shares sum exactly to 1 (fixes rounding error)
            var lastNonZero = Array.FindLastIndex(shares, s => s > 0f);
            if (lastNonZero >= 0)
            {
                var actualSum = shares.Sum();
                shares[lastNonZero] += Mathf.Max(0f, 1f - actualSum);
            }
        }

        // If all values are zero, return all zeros
        return shares;
    }

}
