using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class SettingsViewHeightTests
{
    private const float Viewport = 584f;
    private const float Padding = 12f;

    [Test]
    public static void TallContentGetsItsOwnHeightPlusPadding() =>
        Expect.AreEqual(620f, Settings.NextViewHeight(608f, Viewport, Padding));

    // Content shorter than the screen must not shrink the view rect, or the listing gains room
    // to break columns in.
    [Test]
    public static void ShortContentStillFillsTheViewport() =>
        Expect.AreEqual(Viewport, Settings.NextViewHeight(200f, Viewport, Padding));

    // Regression. A view rect shorter than the viewport lets Listing break to a second column;
    // CurHeight is then that column's own y, and feeding it back shrank the rect further each
    // frame until one row was left on screen.
    [Test]
    public static void ACollapsedMeasurementCannotShrinkTheViewBelowTheViewport()
    {
        Expect.AreEqual(Viewport, Settings.NextViewHeight(32f, Viewport, Padding));
        Expect.AreEqual(Viewport, Settings.NextViewHeight(0f, Viewport, Padding));
    }
}
