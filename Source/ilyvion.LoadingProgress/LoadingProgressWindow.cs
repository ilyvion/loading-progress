using System.Diagnostics;
using UnityEngine;

namespace ilyvion.LoadingProgress;

public partial class LoadingProgressWindow
{
    private const float HorizontalMargin = 21f;
    private const float VerticalWidgetMargin = 4f;
    private const float ProgressBarHeight = 20f;

    private static readonly Vector2 BaseWindowSize = new(776f, 110f);
    internal static Vector2 WindowSize
    {
        get
        {
            var windowSize = BaseWindowSize;
            if (LoadingProgressMod.Settings.ShowLastLoadingTime)
            {
                windowSize.y += 30f;
                if (HasLastLoadAndHashChanged())
                {
                    windowSize.y += Text.LineHeightOf(GameFont.Small) + VerticalWidgetMargin;
                }
            }
            return windowSize;
        }
    }

    private static bool HasLastLoadAndHashChanged()
    {
        return _lastLoadingTime.HasValue && _currentModHash != LoadingProgressMod.Settings.LastLoadingModHash;
    }

    internal static void DrawWindow(Rect statusRect)
    {
        Find.WindowStack.ImmediateWindow(62893994, statusRect, WindowLayer.Super, delegate
        {
            DrawContents(statusRect.AtZero());
        });
    }

    internal static Stopwatch? _loadingStopwatch;
    internal static TimeSpan? _lastLoadingTime;
    internal static int _currentModHash;
    internal static void DrawContents(Rect rect)
    {
        _loadingStopwatch ??= Stopwatch.StartNew();
        _lastLoadingTime = LoadingProgressMod.Settings.LastLoadingTime > 0
            ? TimeSpan.FromSeconds(LoadingProgressMod.Settings.LastLoadingTime)
            : null;
        _currentModHash = StableListHasher.ComputeListHash(
            LoadedModManager.RunningModsListForReading
                .Select(mod => mod.PackageId));

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.UpperLeft;

        Rect loadingProgressRect = rect;
        loadingProgressRect.x += HorizontalMargin;
        loadingProgressRect.y += 10f;
        loadingProgressRect.width -= 2 * HorizontalMargin;
        loadingProgressRect.height = Text.LineHeight;

        Widgets.Label(loadingProgressRect, "Loading progress");

        Rect loadingActivityRect = loadingProgressRect;
        loadingProgressRect.y += loadingProgressRect.height + VerticalWidgetMargin;
        Text.Font = GameFont.Small;
        loadingActivityRect.height = Text.LineHeight;

        var rule = CurrentStageRule;
        string? label = rule.CustomLabel is not null
            ? rule.CustomLabel(_currentLoadingActivity)
            : GetStageTranslation(rule.Stage, _currentLoadingActivity);
        if (!string.IsNullOrEmpty(label))
        {
            Widgets.Label(loadingProgressRect, Text.ClampTextWithEllipsis(loadingProgressRect, label));
        }

        Rect progressRect = loadingProgressRect;
        progressRect.y += loadingActivityRect.height + VerticalWidgetMargin;
        progressRect.height = ProgressBarHeight;
        if (StageProgress is (float currentValue, float maxValue))
        {
            DrawHorizontalProgressBar(
                progressRect,
                (int)CurrentStage,
                (int)LoadingStage.Finished,
                currentValue,
                maxValue);
        }
        else
        {
            DrawHorizontalProgressBar(
                progressRect,
                (int)CurrentStage,
                (int)LoadingStage.Finished);
        }

        if (LoadingProgressMod.Settings.ShowLastLoadingTime)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;

            Rect loadingTimeRect = progressRect;
            loadingTimeRect.y += progressRect.height + VerticalWidgetMargin;
            loadingTimeRect.height = Text.LineHeight;

            TimeSpan elapsed = _loadingStopwatch!.Elapsed;
            if (_lastLoadingTime.HasValue)
            {
                float totalSeconds = (float)_lastLoadingTime.Value.TotalSeconds;
                DrawHorizontalProgressBar(
                    loadingTimeRect,
                    Math.Clamp((float)elapsed.TotalSeconds, 0f, totalSeconds),
                    totalSeconds,
                    (float)elapsed.TotalSeconds > totalSeconds ? (float)elapsed.TotalSeconds - totalSeconds : null,
                    (float)elapsed.TotalSeconds > totalSeconds ? 10f : null,
                    TimeBarColor,
                    TimerSmallBarColor);
            }
            var elapsedTimeText = elapsed.ToString("mm\\:ss");
            string lastLoadingTimeText = _lastLoadingTime.HasValue
                ? "~" + _lastLoadingTime.Value.ToString("mm\\:ss")
                : "--:--";
            Widgets.Label(loadingTimeRect, $"{elapsedTimeText} / {lastLoadingTimeText}");

            if (HasLastLoadAndHashChanged())
            {
                Text.Font = GameFont.Small;

                Rect modHashRect = loadingTimeRect;
                modHashRect.y += loadingTimeRect.height + VerticalWidgetMargin;
                modHashRect.height = Text.LineHeight;
                Widgets.Label(modHashRect, Translations.GetTranslation("LoadingProgress.ModHashChanged"));
            }
        }

        Text.Anchor = TextAnchor.UpperLeft;
    }

    protected static void DrawHorizontalProgressBar(
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

        // draw the bar
        Widgets.DrawBoxSolid(barRect, customBarColor ?? BarColor);

        if (smallCurrentValue.HasValue && smallMaxValue.HasValue)
        {
            smallCurrentValue = smallCurrentValue.Value % (2 * smallMaxValue.Value);

            // draw the small bar
            var smallBarRect = progressRect.ContractedBy(2f);
            smallBarRect.yMin += barRect.height / 2;
            var smallUnit = smallBarRect.width / smallMaxValue.Value;
            smallBarRect.width = smallCurrentValue.Value * smallUnit;

            if (smallCurrentValue.Value > smallMaxValue.Value)
            {
                // Once we're past max, start drawing the bar with a gap on the left side equal to the overflow.
                smallBarRect.width = smallMaxValue.Value * smallUnit;
                smallBarRect.xMin += (smallCurrentValue.Value - smallMaxValue.Value) * smallUnit;
            }

            Widgets.DrawBoxSolid(smallBarRect, customSmallBarColor ?? SmallBarColor);
        }
    }

    private static readonly Color BarColor = new(0.2f, 0.8f, 0.85f);
    private static readonly Color SmallBarColor = Color.white.ToTransparent(0.75f);

    private static readonly Color TimeBarColor = BarColor.Darken(0.2f);
    private static readonly Color TimerSmallBarColor = Color.white.Darken(0.2f).ToTransparent(0.75f);
}
