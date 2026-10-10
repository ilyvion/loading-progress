namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// Holds an action back while a long event's thread is running and runs it, once, on the
/// main thread at the first look that finds no such thread.
/// </summary>
/// <remarks>
/// The engine's deferred-action list is no place to wait in: that thread adds to it with no
/// lock, so the main thread must not add to it while the thread runs.
/// </remarks>
internal sealed class EventThreadWait
{
    private Action? _waiting;

    /// <summary>
    /// Runs <paramref name="action"/> now when <paramref name="eventThread"/> is not running,
    /// else holds it until a later <see cref="Update"/> finds it stopped.
    /// </summary>
    internal void RunWhenStopped(Action action, Thread? eventThread)
    {
        _waiting = action;
        Update(eventThread);
    }

    /// <summary>
    /// Runs the waiting action, if any, when <paramref name="eventThread"/> is not running.
    /// </summary>
    internal void Update(Thread? eventThread)
    {
        if (_waiting is { } action && !IsRunning(eventThread))
        {
            _waiting = null;
            action();
        }
    }

    /// <summary>
    /// Whether <paramref name="eventThread"/> is running. One created but not yet started is
    /// not.
    /// </summary>
    internal static bool IsRunning(Thread? eventThread) => eventThread is { IsAlive: true };
}
