using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// Times every prefix, postfix and finalizer other mods put on any method while the game
/// loads, each under the mod that owns it.
/// </summary>
/// <remarks>
/// <para>
/// Harmony builds a patched method's replacement as a list of instructions, one call per
/// hook, before it compiles it. Each such call is pointed at a generated method with the
/// hook's own signature that starts the owner's timer, calls the hook, and stops the timer
/// in a finally block, so the replacement's stack is untouched and a hook that throws still
/// stops its timer. A prefix that returns false ran in place of the original method, so its
/// time goes to the category the original would have run under, marked as replaced by the
/// prefix's mod. This only reaches replacements built after <see cref="Activate"/>.
/// </para>
/// <para>
/// Everything here runs inside every other mod's patching, so none of it may fail into
/// Harmony. Only <see cref="Internals"/> names Harmony's internal members; each of its methods
/// is called inside a try block, so a Harmony without them fails there, at compile time of
/// that method. Timing starts only for Harmony 2 with the internal methods shaped as expected,
/// and only after a self-test on this thread passes. Any later failure turns timing off for
/// good, and a replacement that fails to build after its hooks were redirected is built again
/// without them.
/// </para>
/// </remarks>
internal static class HookTiming
{
    internal const string Category = "LoadingProgress.StartupImpact.HarmonyHook";
    internal const string HarmonyId = "ilyvion.LoadingProgress.HookTiming";
    private const string SelfTestOpen = "LoadingProgress.StartupImpact.HookTimingSelfTest";

    private static readonly Harmony _harmony = new(HarmonyId);
    private static readonly object _lock = new();
    private static readonly Dictionary<
        (MethodBase Original, MethodInfo Hook),
        DynamicMethod?
    > _wrappers = [];

    private static readonly MethodInfo StartMethod = AccessTools.Method(
        typeof(HookTiming),
        nameof(Start)
    );
    private static readonly MethodInfo StopMethod = AccessTools.Method(
        typeof(HookTiming),
        nameof(Stop)
    );
    private static readonly MethodInfo StopPrefixMethod = AccessTools.Method(
        typeof(HookTiming),
        nameof(StopPrefix)
    );

    private static volatile Timed[] _byId = new Timed[256];
    private static int _count;
    private static volatile bool _active;
    private static volatile bool _faulted;
    private static int _warnedFault;

    // The replacements Harmony is building on this thread, innermost first.
    [ThreadStatic]
    private static Building? _building;

    // The thread running the self-test, which times hooks before _active is set.
    private static volatile Thread? _selfTestThread;

    // Replacer is the owner's name for a prefix that can skip the original, else null.
    private sealed record Timed(Profiler Profiler, string Category, string? Replacer);

    // A replacement being built: Harmony's MethodCreator for it, and whether any of its hook
    // calls were redirected.
    internal sealed class Building(object creator, Building? outer)
    {
        public object Creator { get; } = creator;
        public Building? Outer { get; } = outer;
        public bool Rewrote { get; set; }
    }

    private static bool Timing => !_faulted && (_active || _selfTestThread == Thread.CurrentThread);

    /// <summary>
    /// Starts timing hooks in every replacement Harmony builds from now on, after checking
    /// the running Harmony is one this was made for and that a timed hook runs and is
    /// recorded on a method of its own. Never throws: when anything fails, hooks run untimed
    /// and a warning gives the reason.
    /// </summary>
    internal static void Activate(ModContentPack ownMod)
    {
        _faulted = false;
        _warnedFault = 0;
        try
        {
            if (UnsupportedHarmony(typeof(Harmony).Assembly.GetName().Version) is { } reason)
            {
                LoadingProgressMod.Warning(
                    $"Other mods' Harmony hooks will not be timed on their own: {reason}"
                );
                return;
            }

            _ = _harmony.Patch(
                Internals.CreateReplacement(),
                prefix: new HarmonyMethod(typeof(HookTiming), nameof(CreateReplacementPrefix)),
                finalizer: new HarmonyMethod(typeof(HookTiming), nameof(CreateReplacementFinalizer))
            );
            _ = _harmony.Patch(
                Internals.Rewrite(),
                prefix: new HarmonyMethod(typeof(HookTiming), nameof(RewritePrefix))
            );

            if (SelfTest(ownMod) is { } failure)
            {
                Deactivate();
                LoadingProgressMod.Warning(
                    $"Other mods' Harmony hooks will not be timed on their own: {failure}"
                );
                return;
            }
            _active = true;
        }
        catch (Exception e)
        {
            Deactivate();
            LoadingProgressMod.Warning(
                $"Other mods' Harmony hooks will not be timed on their own: {e}"
            );
        }
    }

    /// <summary>
    /// Stops timing hooks. Replacements built until now keep calling their hooks through the
    /// generated methods, which then only check that timing is off.
    /// </summary>
    internal static void Deactivate()
    {
        _active = false;
        try
        {
            _harmony.UnpatchAll(HarmonyId);
        }
        catch (Exception e)
        {
            LoadingProgressMod.Warning($"Could not remove the Harmony hook timing patches: {e}");
        }
    }

    /// <summary>
    /// Why hook timing cannot run on the Harmony of <paramref name="version"/>, or
    /// <see langword="null"/> when it can.
    /// </summary>
    internal static string? UnsupportedHarmony(Version? version) =>
        UnsupportedHarmony(version, Internals.CreateReplacement(), Internals.Rewrite());

    /// <summary>
    /// Why hook timing cannot run on the Harmony of <paramref name="version"/>, given its
    /// internal methods that build a replacement and rewrite its instructions, or
    /// <see langword="null"/> when it can.
    /// </summary>
    internal static string? UnsupportedHarmony(
        Version? version,
        MethodInfo? createReplacement,
        MethodInfo? rewrite
    ) =>
        version?.Major != 2 ? $"this only works with Harmony 2, and Harmony {version} is running."
        : createReplacement is not { IsStatic: false }
        || createReplacement.GetParameters().Length != 0
        || createReplacement.ReturnType != typeof((MethodInfo, Dictionary<int, CodeInstruction>))
            ? $"Harmony {version} builds patched methods differently from what this expects."
        : rewrite is not { IsStatic: true }
        || rewrite.ReturnType != typeof(List<CodeInstruction>)
        || rewrite.GetParameters() is not [{ Name: "instructions" } instructions, ..]
        || instructions.ParameterType != typeof(List<CodeInstruction>)
            ? $"Harmony {version} rewrites patched methods differently from what this expects."
        : null;

    /// <summary>
    /// Turns timing off for good and warns once with <paramref name="message"/>.
    /// </summary>
    private static void Fault(string message)
    {
        _faulted = true;
        _active = false;
        if (Interlocked.Exchange(ref _warnedFault, 1) == 0)
        {
            LoadingProgressMod.Warning(
                $"Other mods' Harmony hooks are no longer timed on their own: {message}"
            );
        }
    }

    private static void CreateReplacementPrefix(object __instance, out Building __state) =>
        __state = _building = new Building(__instance, _building);

    // Builds the replacement again without redirected hook calls when it failed with them.
    private static Exception? CreateReplacementFinalizer(
        Exception? __exception,
        object __instance,
        Building? __state,
        ref (MethodInfo, Dictionary<int, CodeInstruction>) __result
    )
    {
        if (__state != null)
        {
            _building = __state.Outer;
        }
        if (__exception == null || __state is not { Rewrote: true })
        {
            return __exception;
        }

        Fault($"a patched method failed to build with its hooks timed: {__exception}");
        try
        {
            __result = Internals.CreateReplacementAgain(__instance);
            return null;
        }
        catch (Exception e)
        {
            LoadingProgressMod.Warning(
                $"A patched method also failed to build with its hooks untimed: {e}"
            );
            return __exception;
        }
    }

    private static void RewritePrefix(List<CodeInstruction> instructions)
    {
        if (!Timing || _building is not { } building)
        {
            return;
        }

        try
        {
            var (original, prefixes, postfixes, finalizers) = Internals.Hooks(building.Creator);
            var hooks = new HashSet<MethodInfo>(prefixes.Concat(postfixes).Concat(finalizers));
            foreach (var instruction in instructions)
            {
                if (
                    instruction.opcode == OpCodes.Call
                    && instruction.operand is MethodInfo hook
                    && hooks.Contains(hook)
                    && WrapperFor(
                        original,
                        hook,
                        hook.ReturnType == typeof(bool) && prefixes.Contains(hook)
                    )
                        is { } wrapper
                )
                {
                    building.Rewrote = true;
                    instruction.operand = wrapper;
                }
            }
        }
        catch (Exception e)
        {
            Fault($"could not time the hooks of a patched method: {e}");
        }
    }

    // The generated method that times hook on original, made once per pair, or null for a
    // hook that is not timed: one of Loading Progress's own, one no mod owns, one whose
    // signature a generated method cannot repeat, or one a generated method failed for.
    private static DynamicMethod? WrapperFor(MethodBase original, MethodInfo hook, bool canSkip)
    {
        lock (_lock)
        {
            if (_wrappers.TryGetValue((original, hook), out var known))
            {
                return known;
            }

            DynamicMethod? wrapper = null;
            try
            {
                wrapper = CreateWrapper(original, hook, canSkip);
            }
            catch (Exception e)
            {
                LoadingProgressMod.Warning(
                    $"The Harmony hook {hook.DeclaringType?.FullName}.{hook.Name} will not be timed: {e}"
                );
            }
            _wrappers[(original, hook)] = wrapper;
            return wrapper;
        }
    }

    private static DynamicMethod? CreateWrapper(MethodBase original, MethodInfo hook, bool canSkip)
    {
        var assembly = hook.DeclaringType?.Assembly;
        if (
            assembly == null
            || (assembly == typeof(HookTiming).Assembly && !SelfTestHooks.Contains(hook))
            || !hook.IsStatic
            || hook.ContainsGenericParameters
            || hook.ReturnType.IsByRef
            || (hook.CallingConvention & CallingConventions.VarArgs) != 0
        )
        {
            return null;
        }

        var mod = Utilities.FindModByAssembly(assembly);
        var profiler =
            mod == null
                ? null
                : LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod)?.Profiler;
        if (profiler == null)
        {
            return null;
        }

        var wrapper = EmitWrapper(hook, _count, canSkip);
        _ = Register(
            new Timed(
                profiler,
                $"{Category}|{original.DeclaringType?.Name}.{original.Name}",
                canSkip ? mod!.Name : null
            )
        );
        return wrapper;
    }

    private static int Register(Timed timed)
    {
        var byId = _byId;
        if (_count == byId.Length)
        {
            var grown = new Timed[byId.Length * 2];
            Array.Copy(byId, grown, byId.Length);
            byId = grown;
        }
        byId[_count] = timed;
        _byId = byId;
        return _count++;
    }

    // static R Wrapper(args) { var started = Start(id); try { return hook(args); } finally { if (started) Stop(id); } }
    // For a prefix that can skip the original, the try block ends, before returning,
    // with: if (started) { started = false; StopPrefix(id, result); }
    private static DynamicMethod EmitWrapper(MethodInfo hook, int id, bool canSkip)
    {
        var parameterTypes = hook.GetParameters().Select(p => p.ParameterType).ToArray();
        var wrapper = new DynamicMethod(
            $"{hook.DeclaringType?.Name}.{hook.Name}_Timed",
            hook.ReturnType,
            parameterTypes,
            typeof(HookTiming).Module,
            skipVisibility: true
        );

        var il = wrapper.GetILGenerator();
        var started = il.DeclareLocal(typeof(bool));
        var result = hook.ReturnType == typeof(void) ? null : il.DeclareLocal(hook.ReturnType);

        il.Emit(OpCodes.Ldc_I4, id);
        il.Emit(OpCodes.Call, StartMethod);
        il.Emit(OpCodes.Stloc, started);

        _ = il.BeginExceptionBlock();
        for (var i = 0; i < parameterTypes.Length; i++)
        {
            il.Emit(OpCodes.Ldarg, (short)i);
        }
        il.Emit(OpCodes.Call, hook);
        if (result != null)
        {
            il.Emit(OpCodes.Stloc, result);
        }
        if (canSkip)
        {
            var stopped = il.DefineLabel();
            il.Emit(OpCodes.Ldloc, started);
            il.Emit(OpCodes.Brfalse_S, stopped);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Stloc, started);
            il.Emit(OpCodes.Ldc_I4, id);
            il.Emit(OpCodes.Ldloc, result!);
            il.Emit(OpCodes.Call, StopPrefixMethod);
            il.MarkLabel(stopped);
        }

        il.BeginFinallyBlock();
        var notStarted = il.DefineLabel();
        il.Emit(OpCodes.Ldloc, started);
        il.Emit(OpCodes.Brfalse_S, notStarted);
        il.Emit(OpCodes.Ldc_I4, id);
        il.Emit(OpCodes.Call, StopMethod);
        il.MarkLabel(notStarted);
        il.EndExceptionBlock();

        if (result != null)
        {
            il.Emit(OpCodes.Ldloc, result);
        }
        il.Emit(OpCodes.Ret);

        return wrapper;
    }

    // Starts the hook's category, and returns whether it did, so the wrapper stops only what
    // was started. Nothing here may throw into the mod's hook.
    internal static bool Start(int id)
    {
        if (!Timing)
        {
            return false;
        }

        try
        {
            var timed = _byId[id];
            timed.Profiler.Start(timed.Category);
            return true;
        }
        catch (Exception e)
        {
            Fault($"timing a hook failed: {e}");
            return false;
        }
    }

    internal static void Stop(int id)
    {
        try
        {
            var timed = _byId[id];
            _ = timed.Profiler.Stop(timed.Category);
        }
        catch (Exception e)
        {
            Fault($"timing a hook failed: {e}");
        }
    }

    // Stops a prefix that can skip the original once it has returned runOriginal.
    internal static void StopPrefix(int id, bool runOriginal)
    {
        try
        {
            var timed = _byId[id];
            _ =
                runOriginal || timed.Replacer == null
                    ? timed.Profiler.Stop(timed.Category)
                    : timed.Profiler.StopReplacing(timed.Category, timed.Replacer);
        }
        catch (Exception e)
        {
            Fault($"timing a hook failed: {e}");
        }
    }

    private static readonly MethodInfo[] SelfTestHooks =
    [
        AccessTools.Method(typeof(HookTiming), nameof(SelfTestRunningPrefix)),
        AccessTools.Method(typeof(HookTiming), nameof(SelfTestPostfix)),
        AccessTools.Method(typeof(HookTiming), nameof(SelfTestPassThroughPostfix)),
        AccessTools.Method(typeof(HookTiming), nameof(SelfTestSkippingPrefix)),
    ];

    // Patches two methods of this class with one hook of each shape a generated method has,
    // calls them inside a category of Loading Progress's own, and checks that the hooks ran
    // as Harmony 2 runs them and their time was recorded, then removes the patches and the
    // records. Returns what went wrong, or null.
    private static string? SelfTest(ModContentPack ownMod)
    {
        var profiler = LoadingProgressMod
            .instance.StartupImpact.Modlist.GetModInfoFor(ownMod)
            ?.Profiler;
        if (profiler == null)
        {
            return "Loading Progress has no timer of its own.";
        }

        var ran = AccessTools.Method(typeof(HookTiming), nameof(SelfTestTarget));
        var skipped = AccessTools.Method(typeof(HookTiming), nameof(SelfTestSkipped));
        var hookCategory = $"{Category}|{nameof(HookTiming)}.{nameof(SelfTestTarget)}";
        var replaced = StartupImpactProfilerUtil.ReplacedBy(SelfTestOpen, ownMod.Name);
        _selfTestThread = Thread.CurrentThread;
        try
        {
            _ = _harmony.Patch(
                ran,
                prefix: new HarmonyMethod(SelfTestHooks[0]),
                postfix: new HarmonyMethod(SelfTestHooks[1])
            );
            _ = _harmony.Patch(ran, postfix: new HarmonyMethod(SelfTestHooks[2]));
            _ = _harmony.Patch(skipped, prefix: new HarmonyMethod(SelfTestHooks[3]));
            if (_faulted)
            {
                return "the test hooks could not be timed.";
            }

            profiler.Start(SelfTestOpen);
            int ranResult;
            int skippedResult;
            try
            {
                ranResult = SelfTestTarget(1);
                skippedResult = SelfTestSkipped();
            }
            finally
            {
                _ = profiler.Stop(SelfTestOpen);
            }

            return ranResult != 4 ? "the test hooks did not run."
                : skippedResult != 2 ? "a prefix returning false did not skip the original."
                : _faulted ? "timing the test hooks failed."
                : !profiler.Metrics.ContainsKey(hookCategory)
                || !profiler.Metrics.ContainsKey(replaced)
                    ? "the test hooks' time was not recorded."
                : null;
        }
        finally
        {
            _selfTestThread = null;
            _harmony.Unpatch(ran, HarmonyPatchType.All, HarmonyId);
            _harmony.Unpatch(skipped, HarmonyPatchType.All, HarmonyId);
            foreach (var category in new[] { hookCategory, replaced, SelfTestOpen })
            {
                if (profiler.Metrics.TryGetValue(category, out var ms))
                {
                    profiler.Discount(category, ms);
                    _ = profiler.Metrics.TryRemove(category, out _);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int SelfTestTarget(int value) => value + 1;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int SelfTestSkipped() => 1;

    private static bool SelfTestRunningPrefix() => true;

    private static void SelfTestPostfix(ref int __result) => __result++;

    private static int SelfTestPassThroughPostfix(int __result) => __result + 1;

    private static bool SelfTestSkippingPrefix(ref int __result)
    {
        __result = 2;
        return false;
    }

    // The only code naming Harmony's internal members. A Harmony without one fails the
    // method naming it when that method is compiled, at its call inside a try block.
    private static class Internals
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static MethodInfo? CreateReplacement() =>
            AccessTools.Method(typeof(MethodCreator), nameof(MethodCreator.CreateReplacement));

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static MethodInfo? Rewrite() =>
            AccessTools.Method(typeof(FaultBlockRewriter), nameof(FaultBlockRewriter.Rewrite));

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (
            MethodBase Original,
            List<MethodInfo> Prefixes,
            List<MethodInfo> Postfixes,
            List<MethodInfo> Finalizers
        ) Hooks(object creator)
        {
            var config = ((MethodCreator)creator).config;
            return (config.original, config.prefixes, config.postfixes, config.finalizers);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (MethodInfo, Dictionary<int, CodeInstruction>) CreateReplacementAgain(
            object creator
        ) => new MethodCreator(((MethodCreator)creator).config).CreateReplacement();
    }
}
