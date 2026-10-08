using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

// These tests wait through the startup's tail, while other mods' post-load events run.
[TestFixture(TestType.MainMenu)]
[WarningsAllowed(TestStartup.OtherModsWarnings)]
internal sealed class LoadingTimeTests
{
    // Room for the frame between the two timers starting and the settings write between them
    // stopping.
    private const double AllowedDifferenceMilliseconds = 1000;

    private const int MaxFramesToWaitForTheStartupToComplete = 1200;

    // A command-line test run starts before Root.Start's InitializingInterface event, and the
    // loading window records its time later still, at the frame the main menu is usable (or
    // when a quicktest goes straight into a game).
    private static bool StillStartingUp(ref int framesWaited) =>
        !LoadingProgressWindow.StartupComplete
        && framesWaited++ < MaxFramesToWaitForTheStartupToComplete;

    [Test]
    public static IEnumerator ThisLaunchsLoadingTimeIsRecorded()
    {
        var framesWaited = 0;
        while (StillStartingUp(ref framesWaited))
        {
            yield return null;
        }

        var loadingTime = LoadingProgressWindow.CurrentLoadingTime;

        Expect.IsTrue(loadingTime.HasValue);
        Expect.GreaterThan(loadingTime.GetValueOrDefault(), TimeSpan.Zero);
    }

    // Regression. Startup Impact used to stop before the final garbage collection, asset
    // unload and remaining ExecuteWhenFinished actions while the corner kept counting, and
    // the corner used to stop where the interface began initializing while the startup impact
    // window went on to the main menu. Both now count to the frame the main menu is usable.
    [Test]
    public static IEnumerator StartupImpactAndTheCornerMeasureTheSameSpan()
    {
        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        if (!startupImpact.WasTrackingEnabledAtStartup)
        {
            Test.Skip("Startup impact tracking was off for this launch.");
            yield break;
        }

        var framesWaited = 0;
        while (StillStartingUp(ref framesWaited))
        {
            yield return null;
        }

        // A startup that reached the menu has its time to the menu; without this, a tracker
        // that never took it would pass whenever the tail is shorter than the allowance.
        if (Current.ProgramState == ProgramState.Entry)
        {
            Expect.GreaterThan(startupImpact.TimeToMenu, startupImpact.TotalLoadingTime);
        }

        var cornerMilliseconds = LoadingProgressWindow
            .CurrentLoadingTime.GetValueOrDefault()
            .TotalMilliseconds;
        var startupImpactMilliseconds =
            startupImpact.TimeToMenu > 0f
                ? startupImpact.TimeToMenu
                : startupImpact.TotalLoadingTime;

        Expect.LessThanOrEqualTo(
            Math.Abs(cornerMilliseconds - startupImpactMilliseconds),
            AllowedDifferenceMilliseconds
        );
    }
}
