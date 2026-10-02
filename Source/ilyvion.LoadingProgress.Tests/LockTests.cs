using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class LockTests
{
    private static bool IsFreeOnAnotherThread(Lock @lock)
    {
        var free = false;
        var thread = new Thread(() =>
        {
            free = @lock.TryEnter();
            if (free)
            {
                @lock.Exit();
            }
        });
        thread.Start();
        thread.Join();
        return free;
    }

    [Test]
    public static void ALockStatementHoldsTheLock()
    {
        var @lock = new Lock();

        lock (@lock)
        {
            Expect.IsTrue(@lock.IsHeldByCurrentThread);
            Expect.IsFalse(IsFreeOnAnotherThread(@lock));
        }

        Expect.IsFalse(@lock.IsHeldByCurrentThread);
        Expect.IsTrue(IsFreeOnAnotherThread(@lock));
    }

    [Test]
    public static void EnterAndExitTakeAndReleaseTheLock()
    {
        var @lock = new Lock();

        @lock.Enter();
        Expect.IsTrue(@lock.IsHeldByCurrentThread);
        Expect.IsFalse(IsFreeOnAnotherThread(@lock));
        @lock.Exit();

        Expect.IsFalse(@lock.IsHeldByCurrentThread);
        Expect.IsTrue(IsFreeOnAnotherThread(@lock));
    }

    [Test]
    public static void TryEnterTakesAFreeLock()
    {
        var @lock = new Lock();

        Expect.IsTrue(@lock.TryEnter());
        Expect.IsTrue(@lock.IsHeldByCurrentThread);
        @lock.Exit();

        Expect.IsTrue(@lock.TryEnter(0));
        @lock.Exit();

        Expect.IsTrue(@lock.TryEnter(TimeSpan.Zero));
        @lock.Exit();
    }

    [Test]
    public static void AScopeReleasesTheLockWhenDisposed()
    {
        var @lock = new Lock();

        using (@lock.EnterScope())
        {
            Expect.IsTrue(@lock.IsHeldByCurrentThread);
            Expect.IsFalse(IsFreeOnAnotherThread(@lock));
        }

        Expect.IsFalse(@lock.IsHeldByCurrentThread);
        Expect.IsTrue(IsFreeOnAnotherThread(@lock));
    }
}
