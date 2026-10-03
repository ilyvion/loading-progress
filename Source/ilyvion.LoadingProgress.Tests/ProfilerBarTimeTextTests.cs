using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ProfilerBarTimeTextTests
{
    [Test]
    public static void UnderAMinuteHasNoClockText() =>
        Expect.IsNull(ProfilerBar.ClockTimeText(59_900f));

    // 59.95 s rounds to a full minute, so it must read 1:00.0, not 60.0 s.
    [Test]
    public static void AMinuteOnceRoundedToTenthsIsShownWithMinutes() =>
        Expect.AreEqual("1:00.0", ProfilerBar.ClockTimeText(59_950f));

    [Test]
    public static void MinutesAreShownWithSecondsAndTenths() =>
        Expect.AreEqual("5:24.3", ProfilerBar.ClockTimeText(324_300f));

    [Test]
    public static void SecondsBelowTenArePaddedToTwoDigits() =>
        Expect.AreEqual("1:05.0", ProfilerBar.ClockTimeText(65_000f));

    [Test]
    public static void HoursAreShownWithPaddedMinutes() =>
        Expect.AreEqual("1:02:05.4", ProfilerBar.ClockTimeText(3_725_400f));

    [Test]
    public static void JustUnderAnHourRoundsUpToAnHour() =>
        Expect.AreEqual("1:00:00.0", ProfilerBar.ClockTimeText(3_599_960f));

    [Test]
    public static void TimeTextUsesClockTextFromAMinuteUp() =>
        Expect.AreEqual("5:24.3", ProfilerBar.TimeText(324_300f, secondsOnly: false));

    [Test]
    public static void SecondsOnlyKeepsShowingSecondsFromAMinuteUp() =>
        Expect.AreEqual(
            "LoadingProgress.StartupImpact.Seconds".Translate("324.3").ToString(),
            ProfilerBar.TimeText(324_300f, secondsOnly: true)
        );

    [Test]
    public static void UnderAMinuteIsTheSameEitherWay()
    {
        Expect.AreEqual(
            ProfilerBar.TimeText(42_500f, secondsOnly: true),
            ProfilerBar.TimeText(42_500f, secondsOnly: false)
        );
        Expect.AreEqual(
            ProfilerBar.TimeText(140f, secondsOnly: true),
            ProfilerBar.TimeText(140f, secondsOnly: false)
        );
    }
}
