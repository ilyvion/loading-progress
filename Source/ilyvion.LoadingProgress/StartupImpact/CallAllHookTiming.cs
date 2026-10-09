namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// Times other mods' Harmony patches on one method, each under the mod that owns it, for the
/// duration of one call.
/// </summary>
/// <remarks>
/// The engine's <c>StaticConstructorOnStartupUtility.CallAll</c> is run a second time after
/// Loading Progress has run every constructor itself, purely so other mods' prefixes and
/// postfixes on it still fire. Every constructor is a no-op by then, so what that call costs
/// is the hooks, and one base-game heading naming their owners cannot be hidden along with the
/// mod a hook belongs to. Patching the patch methods themselves, with a prefix and a
/// finalizer, puts each hook's time on its own mod under its own category, and the patches
/// come off again as soon as the call returns. The base-game category covering the call is
/// paused while a hook runs, so no millisecond is counted twice.
/// </remarks>
internal sealed class CallAllHookTiming
{
    internal const string Category =
        "LoadingProgress.StartupImpact.StaticConstructorOnStartupUtilityCallAllHook";
    internal const string HarmonyId = "ilyvion.LoadingProgress.CallAllHookTiming";

    // The stage-ledger stage the timing patches go on and come off in, so their cost is an
    // entry of its own in the remaining time.
    internal const string Stage = "LoadingProgress.StartupImpact.Remaining.CallAllHookTiming";

    private static readonly Dictionary<MethodBase, Timed> _timed = [];
    private static int _depth;
    private static bool _warnedFromHook;

    private readonly Harmony _harmony = new(HarmonyId);
    private readonly List<MethodBase> _patched = [];
    private readonly List<string> _untimedOwners = [];
    private MethodBase? _rebuilt;

    private sealed record Timed(ModContentPack Mod, string Category);

    private CallAllHookTiming() { }

    /// <summary>
    /// The mods whose hooks could not be timed on their own, by name, sorted. Their time stays
    /// under the base-game category for the call.
    /// </summary>
    internal IReadOnlyList<string> UntimedOwners => _untimedOwners.AsReadOnly();

    /// <summary>
    /// The base-game category the call runs under, paused while a timed hook runs.
    /// </summary>
    internal static string? BaseCategory { get; set; }

    /// <summary>
    /// Patches every prefix, postfix, finalizer, inner prefix and inner postfix other mods have
    /// on <paramref name="target"/> so each is timed under its owner. Transpilers are not hooks
    /// and are left alone.
    /// </summary>
    internal static CallAllHookTiming Install(MethodBase target) =>
        Install(target, Utilities.FindModByAssembly);

    /// <summary>
    /// <see cref="Install(MethodBase)"/>, finding each hook's mod with
    /// <paramref name="findMod"/>.
    /// </summary>
    /// <remarks>
    /// Never throws. The engine's call runs to fire other mods' hooks, so a failure here must
    /// not keep it from running: every timing patch already in place comes off again, a
    /// warning gives the reason, and the hooks run untimed under the call's heading, which
    /// names their owners.
    /// </remarks>
    internal static CallAllHookTiming Install(
        MethodBase target,
        Func<Assembly, ModContentPack?> findMod
    )
    {
        var timing = new CallAllHookTiming();
        _depth = 0;
        _warnedFromHook = false;
        try
        {
            timing.PatchHooksOn(target, findMod);
        }
        catch (Exception e)
        {
            timing.Remove();
            timing.NameEveryHookOn(target, findMod);
            LoadingProgressMod.Warning(
                $"Could not time the hooks on {target.DeclaringType?.Name}.{target.Name}, so they run untimed: {e.Message}"
            );
        }
        return timing;
    }

    private static IEnumerable<Patch> HooksIn(HarmonyLib.Patches patches) =>
        patches
            .Prefixes.Concat(patches.Postfixes)
            .Concat(patches.Finalizers)
            .Concat(patches.InnerPrefixes)
            .Concat(patches.InnerPostfixes);

    private void PatchHooksOn(MethodBase target, Func<Assembly, ModContentPack?> findMod)
    {
        var patches = Harmony.GetPatchInfo(target);
        if (patches == null)
        {
            return;
        }

        var handled = new HashSet<MethodBase>();
        foreach (var patch in HooksIn(patches))
        {
            var method = patch.PatchMethod;
            var assembly = method?.DeclaringType?.Assembly;
            var mod = assembly == null ? null : findMod(assembly);
            var handling = HandlingFor(assembly, method != null && !handled.Add(method), mod);
            if (handling == HookHandling.NameAsUntimed)
            {
                AddUntimed(mod?.Name ?? patch.owner);
            }
            else if (handling == HookHandling.Time && method != null && mod != null)
            {
                TimeHook(method, mod);
            }
        }

        if (_patched.Count > 0)
        {
            RebuildReplacementOf(target);
        }
        _untimedOwners.Sort(StringComparer.Ordinal);
    }

    // Names the owner of every hook on the target, for when none of them could be timed. Best
    // effort: whatever made the install fail may fail again here, and the warning already
    // logged says why.
    private void NameEveryHookOn(MethodBase target, Func<Assembly, ModContentPack?> findMod)
    {
        _untimedOwners.Clear();
        try
        {
            if (Harmony.GetPatchInfo(target) is { } patches)
            {
                foreach (var patch in HooksIn(patches))
                {
                    if (OwnerNameOf(patch, findMod) is { } name)
                    {
                        AddUntimed(name);
                    }
                }
            }
        }
        catch (Exception)
        {
            // Nothing more can be named; the hooks still run, under the plain heading.
        }
        _untimedOwners.Sort(StringComparer.Ordinal);
    }

    // A hook's owner as the heading names it: its mod's name, else its Harmony id; null for
    // one of Loading Progress's own.
    private static string? OwnerNameOf(Patch patch, Func<Assembly, ModContentPack?> findMod)
    {
        try
        {
            var assembly = patch.PatchMethod?.DeclaringType?.Assembly;
            return assembly == typeof(CallAllHookTiming).Assembly
                ? null
                : (assembly == null ? null : findMod(assembly)?.Name) ?? patch.owner;
        }
        catch (Exception)
        {
            return patch.owner;
        }
    }

    /// <summary>
    /// What <see cref="Install(MethodBase)"/> does with one hook on the call.
    /// </summary>
    internal enum HookHandling
    {
        /// <summary>Times it under its mod.</summary>
        Time,

        /// <summary>Names its owner on the call's heading, where its time stays.</summary>
        NameAsUntimed,

        /// <summary>
        /// Leaves it alone: one of Loading Progress's own, or one handled at an earlier entry.
        /// </summary>
        Skip,
    }

    /// <summary>
    /// What to do with a hook whose method comes from <paramref name="assembly"/> and belongs
    /// to <paramref name="mod"/>. The assembly is null when the method has no declaring type to
    /// find it by, as a generated method can. A method listed a second time, as both a prefix
    /// and a postfix for instance, was handled at its first entry, whether its patch took or
    /// not.
    /// </summary>
    internal static HookHandling HandlingFor(
        Assembly? assembly,
        bool alreadyHandled,
        ModContentPack? mod
    ) =>
        assembly == typeof(CallAllHookTiming).Assembly || alreadyHandled ? HookHandling.Skip
        : assembly == null || mod == null ? HookHandling.NameAsUntimed
        : HookHandling.Time;

    // Puts the timing patches on one hook, or names its owner when that fails.
    private void TimeHook(MethodInfo method, ModContentPack mod)
    {
        try
        {
            _timed[method] = new Timed(
                mod,
                $"{Category}|{method.DeclaringType?.Name}.{method.Name}"
            );
            _ = _harmony.Patch(
                method,
                prefix: new HarmonyMethod(typeof(CallAllHookTiming), nameof(Prefix)),
                finalizer: new HarmonyMethod(typeof(CallAllHookTiming), nameof(Finalizer))
            );
            _patched.Add(method);
        }
        catch (Exception e)
        {
            _ = _timed.Remove(method);
            AddUntimed(mod.Name);
            LoadingProgressMod.Warning(
                $"Could not time {mod.Name}'s hook {method.DeclaringType?.Name}.{method.Name} on its own: {e.Message}"
            );
        }
    }

    /// <summary>
    /// Has Harmony emit and compile <paramref name="target"/>'s replacement again, now that
    /// its hooks carry their timing patches.
    /// </summary>
    /// <remarks>
    /// The replacement was compiled when the last mod patched the target, and Mono's JIT
    /// inlines a hook of under about twenty bytes of IL into it then, so the detour a timing
    /// patch puts on such a hook is never reached from there: a thin wrapper around a mod's
    /// real initialization would stay on the base-game heading unnoticed. Patching a hook
    /// marks it as not to be inlined from then on, and one more patch on the target makes
    /// Harmony build the replacement afresh, so it calls the hook through its detour. The
    /// patch does nothing itself and comes off with the rest. A rebuild that fails fails the
    /// install, since any inlined hook would then run untimed and unnamed.
    /// </remarks>
    private void RebuildReplacementOf(MethodBase target)
    {
        // Set first, so Remove takes the prefix off even after a patch that failed partway.
        _rebuilt = target;
        _ = _harmony.Patch(
            target,
            prefix: new HarmonyMethod(typeof(CallAllHookTiming), nameof(NoOpPrefix))
        );
    }

    /// <summary>
    /// Takes the timing patches off again, leaving the hooks as they were.
    /// </summary>
    internal void Remove()
    {
        foreach (var method in _patched)
        {
            try
            {
                _harmony.Unpatch(method, HarmonyPatchType.All, _harmony.Id);
            }
            catch (Exception e)
            {
                LoadingProgressMod.Warning(
                    $"Could not remove the timing patch from {method.DeclaringType?.Name}.{method.Name}: {e.Message}"
                );
            }
            _ = _timed.Remove(method);
        }
        _patched.Clear();
        if (_rebuilt is { } target)
        {
            try
            {
                _harmony.Unpatch(target, HarmonyPatchType.Prefix, _harmony.Id);
            }
            catch (Exception e)
            {
                LoadingProgressMod.Warning(
                    $"Could not remove the rebuild patch from {target.DeclaringType?.Name}.{target.Name}: {e.Message}"
                );
            }
            _rebuilt = null;
        }
        BaseCategory = null;
        _depth = 0;
    }

    private void AddUntimed(string owner)
    {
        if (!_untimedOwners.Contains(owner))
        {
            _untimedOwners.Add(owner);
        }
    }

    // Does nothing: patching the target with it is what rebuilds the target's replacement.
    private static void NoOpPrefix() { }

    // Hands the finalizer whether the hook's category was started.
    private static void Prefix(MethodBase __originalMethod, out bool __state)
    {
        __state = false;
        if (!_timed.TryGetValue(__originalMethod, out var timed))
        {
            return;
        }

        if (_depth++ == 0 && BaseCategory is { } paused)
        {
            _ = Quietly(() => StartupImpactProfilerUtil.StopBaseGameProfiler(paused));
        }
        __state = Quietly(() =>
            StartupImpactProfilerUtil.StartModProfiler(timed.Mod, timed.Category)
        );
    }

    // Stops the hook's category only if the prefix started it: a start that threw opened
    // nothing, and a stop would close whatever category the mod had open below it. It returns
    // nothing, so an exception from the hook goes on as it was: a finalizer that returns one
    // has Harmony throw it again, which resets its stack trace to the hook's replacement and
    // drops the frames below it, where the mod's error is.
    private static void Finalizer(MethodBase __originalMethod, bool __state)
    {
        if (_timed.TryGetValue(__originalMethod, out var timed))
        {
            if (__state)
            {
                _ = Quietly(() =>
                    StartupImpactProfilerUtil.StopModProfiler(timed.Mod, timed.Category)
                );
            }
            if (--_depth == 0 && BaseCategory is { } paused)
            {
                _ = Quietly(() => StartupImpactProfilerUtil.StartBaseGameProfiler(paused));
            }
        }
    }

    // The prefix and finalizer run inside other mods' hooks, so nothing in them may throw into
    // one: a failure is logged once, and the hook and the call go on. Returns whether the step
    // ran without throwing.
    private static bool Quietly(Action step)
    {
        try
        {
            step();
            return true;
        }
        catch (Exception e)
        {
            if (!_warnedFromHook)
            {
                _warnedFromHook = true;
                LoadingProgressMod.Warning(
                    $"Timing a hook on the static constructor pass failed, so some hook time may sit under the wrong heading: {e.Message}"
                );
            }
            return false;
        }
    }
}
