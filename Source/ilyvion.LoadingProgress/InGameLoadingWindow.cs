using static ilyvion.LoadingProgress.Constants;

namespace ilyvion.LoadingProgress;

// In-game counterpart to LoadingProgressWindow: kind title, current phase, raw activity label,
// a phase/progress bar pair, and elapsed time.
internal static class InGameLoadingWindow
{
    internal static Vector2 WindowSize =>
        new(
            776f,
            10f
                + Text.LineHeightOf(GameFont.Medium)
                + VerticalWidgetMargin
                + Text.LineHeightOf(GameFont.Small)
                + VerticalWidgetMargin
                + Text.LineHeightOf(GameFont.Small)
                + VerticalWidgetMargin
                + ProgressBarHeight
                + VerticalWidgetMargin
                + Text.LineHeightOf(GameFont.Small)
                + 10f
        );

    internal static void DrawContents(Rect rect)
    {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.UpperLeft;

        var titleRect = rect;
        titleRect.x += HorizontalMargin;
        titleRect.y += 10f;
        titleRect.width -= 2 * HorizontalMargin;
        titleRect.height = Text.LineHeight;

        Widgets.Label(titleRect, $"LoadingProgress.InGame.{InGameLoadingSession.Kind}".Translate());

        Text.Font = GameFont.Small;

        var phaseRect = titleRect;
        phaseRect.y += titleRect.height + VerticalWidgetMargin;
        phaseRect.height = Text.LineHeight;

        Widgets.Label(
            phaseRect,
            $"LoadingProgress.InGame.Phase.{InGameLoadingSession.Phase}".Translate()
        );

        var labelRect = phaseRect;
        labelRect.y += phaseRect.height + VerticalWidgetMargin;
        labelRect.height = Text.LineHeight;

        // The static kind never receives a DeepProfiler-derived label (it's drawn on its one
        // painted frame, before the synchronous action that would emit any starts running), so
        // it shows vanilla's own event text instead - the only thing actually known about it at
        // that point.
        var label =
            InGameLoadingSession.Kind == InGameSessionKind.EncounterMapGenerationStatic
                ? LongEventHandler.currentEvent?.eventText ?? string.Empty
                : InGameLoadingSession.DisplayLabel;
        if (!string.IsNullOrEmpty(label))
        {
            var ellipsisRect = labelRect;
            ellipsisRect.width -= 10f;
            Widgets.Label(
                labelRect,
                Utilities.ClampTextWithEllipsisMarkupAware(ellipsisRect, label)
            );
        }

        var progressRect = labelRect;
        progressRect.y += labelRect.height + VerticalWidgetMargin;
        progressRect.height = ProgressBarHeight;

        var barColor = LoadingProgressMod.Settings.ProgressBarColor;
        var smallBarColor = LoadingProgressMod.Settings.SmallBarColor;
        var phaseIndex = InGameLoadingSession.PhaseIndex;
        var phaseCount = InGameLoadingSession.PhaseCount;

        if (InGameLoadingSession.Progress is (float current, float max))
        {
            Widgets_Progressbar.DrawHorizontalProgressBar(
                progressRect,
                phaseIndex,
                phaseCount,
                current,
                max,
                barColor,
                smallBarColor
            );
        }
        else
        {
            Widgets_Progressbar.DrawHorizontalProgressBar(
                progressRect,
                phaseIndex,
                phaseCount,
                customBarColor: barColor
            );
        }

        var elapsedRect = progressRect;
        elapsedRect.y += progressRect.height + VerticalWidgetMargin;
        elapsedRect.height = Text.LineHeight;

        Widgets.Label(elapsedRect, Utilities.FormatDuration(InGameLoadingSession.Elapsed));

        Text.Anchor = TextAnchor.UpperLeft;
    }
}
