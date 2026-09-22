using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class Widgets_ProgressbarTests
{
    [Test]
    public static void MainBarBonusFractionIsZeroAtStart() =>
        Expect.AreApproximatelyEqual(0f, Widgets_Progressbar.MainBarBonusFraction(0f, 10f));

    [Test]
    public static void MainBarBonusFractionIsHalfwayAtHalfProgress() =>
        Expect.AreApproximatelyEqual(0.5f, Widgets_Progressbar.MainBarBonusFraction(5f, 10f));

    [Test]
    public static void MainBarBonusFractionIsOneAtMax() =>
        Expect.AreApproximatelyEqual(1f, Widgets_Progressbar.MainBarBonusFraction(10f, 10f));

    // Regression test for the outer/main progress bar visibly jumping backwards: the small
    // (per-stage) bar's own displayed value wraps every 2x its max to animate a repeating "lap"
    // once an underestimated max is exceeded (see DrawHorizontalProgressBar), but that wrap must
    // not be allowed to shrink the fraction the main bar credits toward the next stage.
    [Test]
    public static void MainBarBonusFractionStaysAtOneAcrossSmallBarLapWraps()
    {
        // Just before the small bar's first lap wraps back to 0.
        Expect.AreApproximatelyEqual(1f, Widgets_Progressbar.MainBarBonusFraction(19.9f, 10f));
        // Just after the small bar wraps back to 0 for its second lap; the main bar's bonus
        // must not drop back down with it.
        Expect.AreApproximatelyEqual(1f, Widgets_Progressbar.MainBarBonusFraction(20.1f, 10f));
        // Many laps later, still pinned at 1.
        Expect.AreApproximatelyEqual(1f, Widgets_Progressbar.MainBarBonusFraction(1000f, 10f));
    }

    [Test]
    public static void MainBarBonusFractionIsZeroWhenMaxIsZero() =>
        Expect.AreApproximatelyEqual(0f, Widgets_Progressbar.MainBarBonusFraction(5f, 0f));
}
