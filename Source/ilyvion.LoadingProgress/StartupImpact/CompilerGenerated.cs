using System.Collections.Concurrent;

namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// Tells the types the compiler writes, such as closures, lambda caches and iterators, from
/// the types a mod's author wrote.
/// </summary>
internal static class CompilerGenerated
{
    private static readonly ConcurrentDictionary<Type, bool> _byType = new();

    /// <summary>
    /// Whether the compiler wrote <paramref name="type"/>: its name starts with '&lt;&gt;', as
    /// a closure's, a lambda cache's or an anonymous type's does, or it carries
    /// <see cref="CompilerGeneratedAttribute"/>, as an iterator or an async state machine
    /// does. A file-local type's name starts with '&lt;' as well, but its author wrote it.
    /// </summary>
    /// <remarks>
    /// Kept per type: the attribute lookup is reflection, and a startup asks about the same
    /// few closure types for each of the tens of thousands of deferred actions a large mod
    /// list queues.
    /// </remarks>
    internal static bool Is(Type type) =>
        _byType.GetOrAdd(
            type,
            static candidate =>
                candidate.Name.StartsWith("<>", StringComparison.Ordinal)
                || candidate.IsDefined(typeof(CompilerGeneratedAttribute), false)
        );
}
