namespace ilyvion.LoadingProgress.StartupImpact;

[HotSwappable]
internal sealed class ProfilerBar
{
    public bool UseLogScale { get; set; }
    public float ProgressBarPadding { get; set; } = 4f;
    public Color DefaultColor { get; set; } = Color.gray;

    /// <summary>
    /// Reference scale (in ms) for the logarithmic transform: values well below this stay close
    /// to proportional, values well above it get compressed more heavily.
    /// </summary>
    public float Tau { get; set; } = 1000f;

    public static string TimeText(float ms) =>
        TimeText(ms, LoadingProgressMod.Settings.ShowStartupImpactTimesInSecondsOnly);

    internal static string TimeText(float ms, bool secondsOnly) =>
        !secondsOnly && ClockTimeText(ms) is { } clockText ? clockText
        : ms > 10000
            ? "LoadingProgress.StartupImpact.Seconds".Translate(
                (ms * 0.001f).ToString("F1", CultureInfo.InvariantCulture)
            )
        : (
            ms > 1000
                ? "LoadingProgress.StartupImpact.Seconds".Translate(
                    (ms * 0.001f).ToString("F2", CultureInfo.InvariantCulture)
                )
                : "LoadingProgress.StartupImpact.Milliseconds".Translate(ms)
        );

    /// <summary>
    /// <c>m:ss.f</c>, or <c>h:mm:ss.f</c> from an hour up, for durations of at least a minute
    /// once rounded to tenths of a second; <see langword="null"/> below that.
    /// </summary>
    internal static string? ClockTimeText(float ms)
    {
        var tenths = (long)Math.Round(ms / 100.0, MidpointRounding.AwayFromZero);
        if (tenths < 600)
        {
            return null;
        }
        var hours = tenths / 36000;
        var minutes = tenths / 600 % 60;
        var seconds = tenths / 10 % 60;
        var tenth = tenths % 10;
        var inv = CultureInfo.InvariantCulture;
        return hours > 0
            ? string.Format(inv, "{0}:{1:00}:{2:00}.{3}", hours, minutes, seconds, tenth)
            : string.Format(inv, "{0}:{1:00}.{2}", minutes, seconds, tenth);
    }

    public void Draw(
        Rect rect,
        IReadOnlyList<float> metrics,
        IReadOnlyList<string> categories,
        float maxImpact,
        IReadOnlyDictionary<string, Color> categoryColors,
        bool translateCategories = true
    )
    {
        // tau is in the same units as the metrics: ms.
        var tau = Tau;

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
            DrawLinearScale(
                metrics,
                categories,
                maxImpact,
                categoryColors,
                innerX,
                innerY,
                innerW,
                innerH
            );
        }
        else
        {
            // Overall fill 0..1 in the SAME transform space
            var denomCap = LogScaleTransform(maxImpact, tau);
            var barFill = denomCap > 0f ? LogScaleTransform(sumLinear, tau) / denomCap : 0f;
            barFill = Mathf.Clamp01(barFill);

            DrawLogScale(
                metrics,
                categories,
                categoryColors,
                tau,
                innerX,
                innerY,
                innerW,
                innerH,
                barFill
            );
        }

        string TooltipLabel(string category)
        {
            return translateCategories
                ? StartupImpactProfilerUtil.TranslateCategory(category)
                : category;
        }

        void DrawLinearScale(
            IReadOnlyList<float> metrics,
            IReadOnlyList<string> categories,
            float maxImpact,
            IReadOnlyDictionary<string, Color> categoryColors,
            float innerX,
            float innerY,
            float innerW,
            float innerH
        )
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

                TooltipHandler.TipRegion(
                    textRect,
                    new TipSignal($"{TooltipLabel(categories[i])}: {TimeText(impact)}")
                );

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
            float barFill
        )
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

                TooltipHandler.TipRegion(
                    textRect,
                    new TipSignal($"{TooltipLabel(categories[i])}: {TimeText(impact)}")
                );

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
    private static float LogScaleTransform(float x, float tau) =>
        Mathf.Log10(1f + (Mathf.Max(0f, x) / tau));

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
                shares[lastNonZero] += 1f - actualSum;
            }
        }

        // If all values are zero, return all zeros
        return shares;
    }
}
