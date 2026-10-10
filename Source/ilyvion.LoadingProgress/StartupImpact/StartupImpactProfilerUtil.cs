namespace ilyvion.LoadingProgress.StartupImpact;

internal static class StartupImpactProfilerUtil
{
    /// <summary>
    /// The mod a deferred initialization action is credited to: the mod whose def it sets up
    /// when it can be traced to one, else the owner of the code it runs, by
    /// <see cref="OwnerOfCode"/>.
    /// </summary>
    /// <remarks>
    /// The engine queues one such action per def for graphics and references, every one from
    /// its own assembly, so the def is the better guide: a framework's per-def work belongs to
    /// the def's mod as well.
    /// </remarks>
    public static ModContentPack? OwnerOfDeferredAction(Delegate action, out bool isBaseGame)
    {
        if (DeferredActionOwner.OwningContentPack(action) is { } defOwner)
        {
            isBaseGame = false;
            return defOwner;
        }

        return OwnerOfCode(action.Method.DeclaringType?.Assembly, out isBaseGame);
    }

    /// <summary>
    /// The mod that loaded <paramref name="assembly"/>. Null with <paramref name="isBaseGame"/>
    /// true for the engine's own assembly, and null with it false for an assembly no mod
    /// loaded, such as the runtime's or a library's, whose code is credited to no one and left
    /// untimed.
    /// </summary>
    public static ModContentPack? OwnerOfCode(Assembly? assembly, out bool isBaseGame)
    {
        var owner = assembly == null ? null : Utilities.FindModByAssembly(assembly);
        isBaseGame =
            owner == null
            && assembly != null
            && assembly.FullName.StartsWith("Assembly-CSharp", StringComparison.Ordinal);
        return owner;
    }

    /// <summary>
    /// Starts <paramref name="category"/> on the timer of an owner <see cref="OwnerOfCode"/>
    /// names: the mod's, else the base game's when <paramref name="isBaseGame"/>. Code no mod
    /// owns is left untimed.
    /// </summary>
    public static void Start(ModContentPack? owner, bool isBaseGame, string category) =>
        ProfilerFor(owner, isBaseGame)?.Start(category);

    /// <summary>
    /// Stops <paramref name="category"/> on the timer <see cref="Start"/> uses for the same
    /// owner, and takes <paramref name="discountMs"/> back off what the stop recorded: time it
    /// was open that was not the startup's, such as the game sitting paused in the background.
    /// </summary>
    public static void Stop(
        ModContentPack? owner,
        bool isBaseGame,
        string category,
        float discountMs = 0f
    ) => _ = ProfilerFor(owner, isBaseGame)?.Stop(category, discountMs);

    /// <summary>
    /// The timer <see cref="Start"/> and <see cref="Stop"/> use for an owner: the mod's, else
    /// the base game's when <paramref name="isBaseGame"/>, else none.
    /// </summary>
    internal static Profiler? ProfilerFor(ModContentPack? owner, bool isBaseGame)
    {
        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        return owner != null ? startupImpact.Modlist.GetModInfoFor(owner)?.Profiler
            : isBaseGame ? startupImpact.BaseGameProfiler
            : null;
    }

    public static void StartModProfiler(ModContentPack? mod, string key)
    {
        if (mod == null)
        {
            return;
        }

        var info = LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod);
        info?.Start(key);
    }

    public static void StopModProfiler(ModContentPack? mod, string key)
    {
        if (mod == null)
        {
            return;
        }

        var info = LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod);
        _ = info?.Stop(key);
    }

    /// <summary>
    /// Stops <paramref name="category"/> on the base game's timer for a timing patch, once.
    /// The patch's postfix calls this on a normal return, and its finalizer when the method
    /// threw and the postfix did not run. <paramref name="started"/> is the state the prefix
    /// set when it started the category; it is cleared before the stop, so a stop that throws
    /// is not tried again.
    /// </summary>
    internal static void StopBaseGameOnce(ref bool started, string category)
    {
        if (TakeStarted(ref started))
        {
            StopBaseGameProfiler(category);
        }
    }

    /// <summary>
    /// <see cref="StopBaseGameOnce"/> for a category on <paramref name="mod"/>'s timer.
    /// </summary>
    internal static void StopModOnce(ref bool started, ModContentPack? mod, string category)
    {
        if (TakeStarted(ref started))
        {
            StopModProfiler(mod, category);
        }
    }

    /// <summary>
    /// The rule both stop-once helpers follow: whether <paramref name="started"/> was set,
    /// clearing it first, so the stop that follows happens once even if it throws.
    /// </summary>
    internal static bool TakeStarted(ref bool started)
    {
        var wasStarted = started;
        started = false;
        return wasStarted;
    }

    public static void StartBaseGameProfiler(string key) =>
        // LoadingProgressMod.DevMessage($"Starting base game profiler for {key}");
        LoadingProgressMod.instance.StartupImpact.BaseGameProfiler.Start(key);

    public static void StopBaseGameProfiler(string key) =>
        // LoadingProgressMod.DevMessage($"Stopping base game profiler for {key}");
        _ = LoadingProgressMod.instance.StartupImpact.BaseGameProfiler.Stop(key);

    /// <summary>
    /// Translates a category string, supporting optional parameter after '|'.
    /// If the string contains '|', the part before is used as the key, the part after as a parameter.
    /// </summary>
    public static string TranslateCategory(string? category)
    {
        if (category == null)
        {
            return string.Empty;
        }

        var pipeIdx = category.IndexOf('|', StringComparison.Ordinal);
        if (pipeIdx < 0)
        {
            return category.Translate();
        }
        var key = category[..pipeIdx];
        var param = category[(pipeIdx + 1)..];
        return key.Translate(param);
    }

    /// <summary>
    /// Derives a stable color from an arbitrary string (e.g. a mod package ID), for cases where
    /// there's no curated color available, such as base-game profiler categories or per-mod bars.
    /// </summary>
    public static Color HashColor(string key)
    {
        var hash = key.GetHashCode(StringComparison.Ordinal);
        return new Color(
            (hash & 0xff) / 255f,
            ((hash >> 8) & 0xff) / 255f,
            ((hash >> 16) & 0xff) / 255f
        );
    }
}
