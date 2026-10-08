using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactEndOfStartupTests
{
    // A save loading at startup runs on a thread of its own and uses Scribe, so the saving at
    // the end of the startup waits while such a thread runs. The game's loading, queued but not
    // started when the startup ends, cannot be using it, so the session is saved, and the
    // marker removed, without waiting for the whole of it.
    [Test]
    public static void TheSavingWaitsOnlyForAnEventThreadThatIsRunning()
    {
        Expect.IsFalse(StartupImpact.StartupImpact.SavingMustWait(null));

        using var release = new ManualResetEventSlim();
        var thread = new Thread(release.Wait);
        Expect.IsFalse(StartupImpact.StartupImpact.SavingMustWait(thread));

        thread.Start();
        try
        {
            Expect.IsTrue(StartupImpact.StartupImpact.SavingMustWait(thread));
        }
        finally
        {
            release.Set();
            thread.Join();
        }
        Expect.IsFalse(StartupImpact.StartupImpact.SavingMustWait(thread));
    }
}
