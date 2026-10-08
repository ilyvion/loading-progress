using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

/// <summary>
/// Lets a test that records time into the live session wait until the startup's tail is over,
/// and take that time back out afterwards.
/// </summary>
/// <remarks>
/// A command-line test run starts before the startup is over: before the tracking clock
/// stops, while the stage ledger is still open, and while a long event after loading may be
/// timed. A test that recorded time then would leave it in the ledger, which nothing takes
/// back, or take the current event's time with it. The tail is over once the menu has been
/// reached, which sets the time to the menu, or once a quicktest has left the menu. Only a
/// startup with tracking on reaches the first, so a test that waits skips first with
/// tracking off.
/// </remarks>
internal static class TestStartup
{
    private const int MaxFramesToWait = 1200;

    /// <summary>
    /// The warnings a fixture whose tests wait allows: every one but Loading Progress's own,
    /// which start with its name.
    /// </summary>
    /// <remarks>
    /// Other mods' post-load events run during the wait, and whatever they log lands in the
    /// first test that waits. None of it is that test's failure.
    /// </remarks>
    internal const string OtherModsWarnings = @"^(?!\[Loading Progress\])";

    /// <summary>
    /// Why a test that needs this startup tracked skips when it was not.
    /// </summary>
    internal const string TrackingOff = "Startup impact tracking is off.";

    /// <summary>
    /// Takes a test's category back out of the live session: its time on the active thread,
    /// with the total it went into, and its entry for other threads. A test that records time
    /// waits for the startup to end first, so the stage ledger, closed by then, holds none of
    /// it.
    /// </summary>
    internal static void Forget(Profiler profiler, string category)
    {
        if (profiler.Metrics.TryGetValue(category, out var ms))
        {
            profiler.Discount(category, ms);
            _ = profiler.Metrics.TryRemove(category, out _);
        }
        _ = profiler.OffThreadMetrics.TryRemove(category, out _);
    }

    /// <summary>
    /// Whether to wait another frame: the tail is not over and the wait has not run out. Use
    /// as <c>while (TestStartup.StillStartingUp(ref framesWaited)) yield return null;</c>
    /// </summary>
    internal static bool StillStartingUp(ref int framesWaited) =>
        LoadingProgressMod.instance.StartupImpact.TimeToMenu <= 0f
        && Current.ProgramState == ProgramState.Entry
        && framesWaited++ < MaxFramesToWait;
}
