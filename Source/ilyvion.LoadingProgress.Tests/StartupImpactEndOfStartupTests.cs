using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

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
        Expect.IsFalse(EventThreadWait.IsRunning(null));

        using var release = new ManualResetEventSlim();
        var thread = new Thread(release.Wait);
        Expect.IsFalse(EventThreadWait.IsRunning(thread));

        thread.Start();
        try
        {
            Expect.IsTrue(EventThreadWait.IsRunning(thread));
        }
        finally
        {
            release.Set();
            thread.Join();
        }
        Expect.IsFalse(EventThreadWait.IsRunning(thread));
    }

    [Test]
    public static void WithNoEventThreadRunningTheActionRunsAtOnce()
    {
        var wait = new EventThreadWait();
        var runs = 0;

        wait.RunWhenStopped(() => runs++, null);
        Expect.AreEqual(1, runs);

        wait.Update(null);
        Expect.AreEqual(1, runs, "The action ran again on a later frame.");
    }

    // The event thread adds to the engine's deferred-action list with no lock, so the wait
    // must keep the action off that list while the thread runs, and run it itself once,
    // on the first frame after the thread has stopped.
    [Test]
    public static void WhileAnEventThreadRunsTheActionWaitsOffTheDeferredActionList()
    {
        var wait = new EventThreadWait();
        var runs = 0;
        var queuedBefore = LongEventHandler.toExecuteWhenFinished.Count;

        using var release = new ManualResetEventSlim();
        var thread = new Thread(release.Wait);
        thread.Start();
        try
        {
            wait.RunWhenStopped(() => runs++, thread);
            Expect.AreEqual(0, runs, "The action ran while the event thread was running.");
            Expect.AreEqual(
                queuedBefore,
                LongEventHandler.toExecuteWhenFinished.Count,
                "The action was queued on the engine's deferred-action list."
            );

            wait.Update(thread);
            Expect.AreEqual(0, runs, "The action ran while the event thread was running.");
        }
        finally
        {
            release.Set();
            thread.Join();
        }

        wait.Update(thread);
        Expect.AreEqual(1, runs);

        wait.Update(thread);
        Expect.AreEqual(1, runs, "The action ran again on a later frame.");
    }
}
