using System.Diagnostics;
using static ilyvion.LoadingProgress.Constants;

namespace ilyvion.LoadingProgress;

internal sealed partial class LoadingProgressWindow
{
    internal static Vector2 WindowSize
    {
        get
        {
            var windowSize = field;
            if (LoadingProgressMod.Settings.ShowLastLoadingTime)
            {
                windowSize.y += 30f;
                if (LoadingProgressMod.Settings.LoadingTimeSampleCount > 0)
                {
                    windowSize.y += Text.LineHeightOf(GameFont.Small) + VerticalWidgetMargin;
                }
                if (HasLastLoadAndHashChanged())
                {
                    windowSize.y += Text.LineHeightOf(GameFont.Small) + VerticalWidgetMargin;
                }
            }
            return windowSize;
        }
    } = new(776f, 110f);

    private static bool HasLastLoadAndHashChanged() =>
        _lastLoadingTime.HasValue
        && _currentModHash != LoadingProgressMod.Settings.LastLoadingModHash;

    /// <summary>
    /// The DeepProfiler label Root.Start's InitializingInterface event opens with, which runs
    /// after the loading event and every ExecuteWhenFinished action it queued.
    /// </summary>
    internal const string LoadingFinishedLabel = "Misc Init (InitializingInterface)";

    internal static Stopwatch? _loadingStopwatch;
    internal static TimeSpan? _lastLoadingTime;
    internal static int _currentModHash;

    /// <summary>
    /// How long this launch took to load, set once loading finishes.
    /// </summary>
    internal static TimeSpan? CurrentLoadingTime { get; private set; }

    internal static void DrawContents(Rect rect)
    {
        if (_loadingStopwatch is null)
        {
            _loadingStopwatch = Stopwatch.StartNew();
            var avgTime = LoadingProgressMod.Settings.AverageLoadingTime;
            _lastLoadingTime = avgTime.HasValue ? TimeSpan.FromSeconds(avgTime.Value) : null;
            _currentModHash = StableListHasher.ComputeListHash(
                LoadedModManager.RunningModsListForReading.Select(mod => mod.PackageId)
            );
            LoadingSessionStats.Reset();
        }

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.UpperLeft;

        var loadingProgressRect = rect;
        loadingProgressRect.x += HorizontalMargin;
        loadingProgressRect.y += 10f;
        loadingProgressRect.width -= 2 * HorizontalMargin;
        loadingProgressRect.height = Text.LineHeight;

        Widgets.Label(loadingProgressRect, Translations.GetTranslation("LoadingProgress.Title"));

        if (LoadingProgressMod.Settings.ShowMemoryUsage)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperRight;
            GUI.color = new Color(1f, 1f, 1f, 0.5f);
            Widgets.Label(
                loadingProgressRect,
                Translations.GetTranslation(
                    "LoadingProgress.MemoryUsage",
                    Utilities.FormatBytes(MemoryUsage.ManagedHeapBytes),
                    Utilities.FormatBytes(MemoryUsage.ProcessBytes)
                )
            );
            GUI.color = Color.white;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        var loadingActivityRect = loadingProgressRect;
        loadingProgressRect.y += loadingProgressRect.height + VerticalWidgetMargin;
        Text.Font = GameFont.Small;
        loadingActivityRect.height = Text.LineHeight;

        var rule = CurrentStageRule;
        string? label = null;
        if (rule.CustomLabel is StageDisplayLabel customLabel)
        {
            label = customLabel(_currentLoadingActivity);
        }
        label ??= GetStageTranslation(rule.Stage, _currentLoadingActivity);

        if (!string.IsNullOrEmpty(label))
        {
            var ellipsisRect = loadingProgressRect;
            ellipsisRect.width -= 10f;
            Widgets.Label(
                loadingProgressRect,
                Utilities.ClampTextWithEllipsisMarkupAware(ellipsisRect, label)
            );
        }

        var progressRect = loadingProgressRect;
        progressRect.y += loadingActivityRect.height + VerticalWidgetMargin;
        progressRect.height = ProgressBarHeight;
        var barColor = LoadingProgressMod.Settings.ProgressBarColor;
        var smallBarColor = LoadingProgressMod.Settings.SmallBarColor;
        if (StageProgress is (float currentValue, float maxValue))
        {
            Widgets_Progressbar.DrawHorizontalProgressBar(
                progressRect,
                (int)CurrentStage,
                (int)LoadingStage.Finished,
                currentValue,
                maxValue,
                barColor,
                smallBarColor
            );
        }
        else
        {
            Widgets_Progressbar.DrawHorizontalProgressBar(
                progressRect,
                (int)CurrentStage,
                (int)LoadingStage.Finished,
                customBarColor: barColor
            );
        }

        if (LoadingProgressMod.Settings.ShowLastLoadingTime)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Medium;

            var loadingTimeRect = progressRect;
            loadingTimeRect.y += progressRect.height + VerticalWidgetMargin;
            loadingTimeRect.height = Text.LineHeight;

            var elapsed = _loadingStopwatch.Elapsed;
            if (_lastLoadingTime.HasValue)
            {
                var totalSeconds = (float)_lastLoadingTime.Value.TotalSeconds;
                Widgets_Progressbar.DrawHorizontalProgressBar(
                    loadingTimeRect,
                    Math.Clamp((float)elapsed.TotalSeconds, 0f, totalSeconds),
                    totalSeconds,
                    (float)elapsed.TotalSeconds > totalSeconds
                        ? (float)elapsed.TotalSeconds - totalSeconds
                        : null,
                    (float)elapsed.TotalSeconds > totalSeconds ? 10f : null,
                    TimeBarColor,
                    TimerSmallBarColor
                );
            }

            var lastLoadingTimeText = _lastLoadingTime.HasValue
                ? $"~{Utilities.FormatDuration(_lastLoadingTime.Value)}"
                : "--:--";
            string loadingTimeText;
            if (LoadingProgressMod.Settings.ShowLoadingTimeAsCountDown)
            {
                var remainingTime = _lastLoadingTime.HasValue
                    ? _lastLoadingTime.Value - elapsed
                    : TimeSpan.Zero;
                loadingTimeText =
                    remainingTime > TimeSpan.Zero
                        ? Translations.GetTranslation(
                            "LoadingProgress.TimeRemaining",
                            Utilities.FormatDuration(remainingTime),
                            lastLoadingTimeText
                        )
                        : Translations.GetTranslation(
                            "LoadingProgress.TimeOverEstimate",
                            Utilities.FormatDuration(remainingTime),
                            lastLoadingTimeText
                        );
            }
            else
            {
                loadingTimeText = $"{Utilities.FormatDuration(elapsed)} / {lastLoadingTimeText}";
            }
            Widgets.Label(loadingTimeRect, loadingTimeText);

            var sampleCount = LoadingProgressMod.Settings.LoadingTimeSampleCount;
            if (sampleCount > 0)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                var sampleInfoRect = loadingTimeRect;
                sampleInfoRect.y += loadingTimeRect.height + VerticalWidgetMargin;
                sampleInfoRect.height = Text.LineHeight;
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                Widgets.Label(
                    sampleInfoRect,
                    sampleCount == 1
                        ? Translations.GetTranslation("LoadingProgress.EstimateBasedOnSingle")
                        : Translations.GetTranslation(
                            "LoadingProgress.EstimateBasedOn",
                            sampleCount
                        )
                );
                GUI.color = Color.white;
                loadingTimeRect = sampleInfoRect;
            }

            if (HasLastLoadAndHashChanged())
            {
                Text.Font = GameFont.Small;

                var modHashRect = loadingTimeRect;
                modHashRect.y += loadingTimeRect.height + VerticalWidgetMargin;
                modHashRect.height = Text.LineHeight;
                Widgets.Label(
                    modHashRect,
                    Translations.GetTranslation("LoadingProgress.ModHashChanged")
                );
            }
        }

        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static Color TimeBarColor => LoadingProgressMod.Settings.ProgressBarColor.Darken(0.2f);
    private static readonly Color TimerSmallBarColor = Color
        .white.Darken(0.2f)
        .ToTransparent(0.75f);
}
