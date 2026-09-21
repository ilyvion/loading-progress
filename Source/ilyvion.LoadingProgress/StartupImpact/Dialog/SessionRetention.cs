namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

/// <summary>
/// Decides which stored sessions a save should drop.
/// </summary>
/// <remarks>
/// Deliberately free of game, IO and Scribe types, and generic over the entry
/// type, so the policy can be exercised directly from the test suite.
/// </remarks>
internal static class SessionRetention
{
    /// <summary>
    /// Smallest useful history: one run to look at and one to compare it against.
    /// </summary>
    internal const int MinimumSessionsToKeep = 2;

    /// <summary>
    /// At roughly 950 bytes per mod per session, thirty sessions on a 230-mod
    /// list is about 6 MB, which is as far as this should go without asking.
    /// </summary>
    internal const int MaximumSessionsToKeep = 30;

    internal const int DefaultSessionsToKeep = 10;

    /// <summary>
    /// Picks the sessions to delete so at most <paramref name="sessionsToKeep"/>
    /// ordinary sessions remain, dropping the oldest first.
    /// </summary>
    /// <param name="newestFirst">Stored sessions, ordered newest first.</param>
    /// <param name="sessionsToKeep">How many ordinary sessions to retain.</param>
    /// <param name="keepPinnedBeyondLimit">
    /// When true, pinned sessions are never dropped and do not occupy a slot.
    /// When false, a pin only affects display order and is evicted like anything else.
    /// </param>
    /// <param name="isPinned">Whether an entry is pinned.</param>
    /// <param name="isBaseline">
    /// Whether an entry is the chosen baseline. The baseline is never dropped
    /// automatically and never occupies a slot; the player removes it by hand.
    /// </param>
    /// <returns>The entries to delete, in the order they appeared.</returns>
    internal static List<T> SelectForEviction<T>(
        IReadOnlyList<T> newestFirst,
        int sessionsToKeep,
        bool keepPinnedBeyondLimit,
        Func<T, bool> isPinned,
        Func<T, bool> isBaseline
    )
    {
        if (newestFirst is null)
        {
            throw new ArgumentNullException(nameof(newestFirst));
        }
        if (isPinned is null)
        {
            throw new ArgumentNullException(nameof(isPinned));
        }
        if (isBaseline is null)
        {
            throw new ArgumentNullException(nameof(isBaseline));
        }

        var limit = Math.Max(sessionsToKeep, MinimumSessionsToKeep);

        List<T> evicted = [];
        var kept = 0;

        foreach (var entry in newestFirst)
        {
            if (isBaseline(entry))
            {
                continue;
            }

            if (keepPinnedBeyondLimit && isPinned(entry))
            {
                continue;
            }

            if (kept < limit)
            {
                kept++;
                continue;
            }

            evicted.Add(entry);
        }

        return evicted;
    }
}
