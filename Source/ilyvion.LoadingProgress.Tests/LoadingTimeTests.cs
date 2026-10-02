using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class LoadingTimeTests
{
    // Room for the frame between the two timers starting and the settings write between them
    // stopping.
    private const double AllowedDifferenceMilliseconds = 1000;

    private const int MaxFramesToWaitForLoadingToFinish = 600;

    // A command-line test run starts before Root.Start's InitializingInterface event, which is
    // where loading finishes.
    private static bool StillLoading(ref int framesWaited) =>
        LoadingProgressWindow.CurrentStage != LoadingStage.Finished
        && framesWaited++ < MaxFramesToWaitForLoadingToFinish;

    [Test]
    public static IEnumerator ThisLaunchsLoadingTimeIsRecorded()
    {
        var framesWaited = 0;
        while (StillLoading(ref framesWaited))
        {
            yield return null;
        }

        var loadingTime = LoadingProgressWindow.CurrentLoadingTime;

        Expect.IsTrue(loadingTime.HasValue);
        Expect.GreaterThan(loadingTime.GetValueOrDefault(), TimeSpan.Zero);
    }

    // Regression. Startup Impact stopped before the final garbage collection, asset unload and
    // remaining ExecuteWhenFinished actions, while the main menu corner kept counting through
    // them, so the two disagreed by however long those took.
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
        while (StillLoading(ref framesWaited))
        {
            yield return null;
        }

        var cornerMilliseconds = LoadingProgressWindow
            .CurrentLoadingTime.GetValueOrDefault()
            .TotalMilliseconds;

        Expect.LessThanOrEqualTo(
            Math.Abs(cornerMilliseconds - startupImpact.TotalLoadingTime),
            AllowedDifferenceMilliseconds
        );
    }
}
