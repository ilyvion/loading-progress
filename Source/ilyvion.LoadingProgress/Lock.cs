#if !v1_7_OR_GREATER
namespace ilyvion.LoadingProgress;

/// <summary>
/// Stands in for <c>System.Threading.Lock</c>, which .NET Framework lacks. <c>lock</c> on it
/// takes the <see cref="Monitor"/> lock of this instance.
/// </summary>
#pragma warning disable CA2002 // A lock statement on this instance takes this instance's monitor.
internal sealed class Lock
{
    /// <summary>Waits until the lock is free, then takes it.</summary>
    public void Enter() => Monitor.Enter(this);

    /// <summary>Takes the lock if it is free.</summary>
    /// <returns>Whether the lock was taken.</returns>
    public bool TryEnter() => Monitor.TryEnter(this);

    /// <summary>Takes the lock, waiting at most <paramref name="millisecondsTimeout"/> for it.</summary>
    /// <param name="millisecondsTimeout">How long to wait, or <see cref="Timeout.Infinite"/>.</param>
    /// <returns>Whether the lock was taken.</returns>
    public bool TryEnter(int millisecondsTimeout) => Monitor.TryEnter(this, millisecondsTimeout);

    /// <summary>Takes the lock, waiting at most <paramref name="timeout"/> for it.</summary>
    /// <param name="timeout">How long to wait.</param>
    /// <returns>Whether the lock was taken.</returns>
    public bool TryEnter(TimeSpan timeout) => Monitor.TryEnter(this, timeout);

    /// <summary>Releases the lock, which the current thread holds.</summary>
    public void Exit() => Monitor.Exit(this);

    /// <summary>Whether the current thread holds the lock.</summary>
    public bool IsHeldByCurrentThread => Monitor.IsEntered(this);

    /// <summary>Takes the lock and returns a scope that releases it when disposed.</summary>
    /// <returns>The scope holding the lock.</returns>
    public Scope EnterScope()
    {
        Enter();
        return new Scope(this);
    }

    /// <summary>Holds a <see cref="Lock"/> taken by <see cref="EnterScope"/>.</summary>
#pragma warning disable CA1034 // Mirrors System.Threading.Lock.Scope.
    public readonly ref struct Scope
#pragma warning restore CA1034
    {
        private readonly Lock _owner;

        internal Scope(Lock owner)
        {
            _owner = owner;
        }

        /// <summary>Releases the lock.</summary>
        public void Dispose() => _owner.Exit();
    }
}
#pragma warning restore CA2002
#endif
