using RimTestRedux;

namespace ilyvion.LoadingProgress.Tests;

[HotSwappable]
[TestSuite]
internal static class SettingsViewHeightTests
{
    private const float Viewport = 584f;
    private const float Padding = 12f;

    [Test]
    public static void TallContentGetsItsOwnHeightPlusPadding() =>
        Assert.That(Settings.NextViewHeight(608f, Viewport, Padding)).Is.EqualTo(620f);

    // Content shorter than the screen must not shrink the view rect, or the listing gains room
    // to break columns in.
    [Test]
    public static void ShortContentStillFillsTheViewport() =>
        Assert.That(Settings.NextViewHeight(200f, Viewport, Padding)).Is.EqualTo(Viewport);

    // Regression. A view rect shorter than the viewport lets Listing break to a second column;
    // CurHeight is then that column's own y, and feeding it back shrank the rect further each
    // frame until one row was left on screen.
    [Test]
    public static void ACollapsedMeasurementCannotShrinkTheViewBelowTheViewport()
    {
        Assert.That(Settings.NextViewHeight(32f, Viewport, Padding)).Is.EqualTo(Viewport);
        Assert.That(Settings.NextViewHeight(0f, Viewport, Padding)).Is.EqualTo(Viewport);
    }
}
