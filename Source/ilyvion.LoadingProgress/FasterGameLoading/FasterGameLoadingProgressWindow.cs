using static ilyvion.LoadingProgress.Constants;

namespace ilyvion.LoadingProgress.FasterGameLoading;

internal static class FasterGameLoadingProgressWindow
{
    internal static Vector2 WindowSize =>
        !FasterGameLoadingUtils.HasFasterGameLoading
        || !FasterGameLoadingUtils.EarlyModContentLoading
        || FasterGameLoadingUtils.FasterGameLoadingEarlyModContentLoadingIsFinished
            ? Vector2.zero
            : new(
                776f,
                LoadingProgressWindow.BaseWindowHeight(
                    Utilities.LineHeightOf(GameFont.Small),
                    Utilities.LineHeightOf(GameFont.Medium)
                )
            );

    internal static ModContentPack? LoadingMod { get; set; }

    internal static void DrawContents(Rect rect)
    {
        if (
            !FasterGameLoadingUtils.HasFasterGameLoading
            || !FasterGameLoadingUtils.EarlyModContentLoading
            || FasterGameLoadingUtils.FasterGameLoadingEarlyModContentLoadingIsFinished
        )
        {
            return;
        }

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.UpperLeft;

        var loadingProgressRect = rect;
        loadingProgressRect.x += HorizontalMargin;
        loadingProgressRect.y += 10f;
        loadingProgressRect.width -= 2 * HorizontalMargin;
        loadingProgressRect.height = Text.LineHeight;

        Widgets.Label(
            loadingProgressRect,
            Translations.GetTranslation($"LoadingProgress.FasterGameLoadingEarlyModContentLoading")
        );

        var loadingActivityRect = loadingProgressRect;
        loadingProgressRect.y += loadingProgressRect.height + VerticalWidgetMargin;
        Text.Font = GameFont.Small;
        loadingActivityRect.height = Text.LineHeight;

        var label = string.Empty;
        if (LoadingMod != null)
        {
            label = Translations.GetTranslation(
                $"LoadingProgress.FasterGameLoadingEarlyModContentLoading.ForMod",
                LoadingMod.Name
            );
        }

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

        Widgets_Progressbar.DrawHorizontalProgressBar(
            progressRect,
            FasterGameLoadingUtils.LoadedMods?.Count ?? 0,
            LoadedModManager.RunningModsListForReading.Count,
            customBarColor: LoadingProgressMod.Settings.ProgressBarColor,
            customSmallBarColor: LoadingProgressMod.Settings.SmallBarColor
        );
    }
}
