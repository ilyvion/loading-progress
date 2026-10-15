using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// Times every prefix, postfix and finalizer other mods put on any method while the game
/// loads, each under the mod that owns it.
/// </summary>
/// <remarks>
/// <para>
/// Harmony builds a patched method's replacement as a list of instructions, one call per
/// hook, before it compiles it. Each such call gets a call to <see cref="Start"/> before it
/// and one to <see cref="Stop"/> after it, which leave the replacement's stack as it was. The
/// replacement gets no exception handler or call to a generated method from this, either of
/// which makes Harmony 3 compile it far more slowly. A hook that throws leaves its timer
/// running until the next stop below it on that thread: see
/// <see cref="Profiler.StartUnguarded"/>. A prefix that returns false ran in place of the
/// original method, so its time goes to the category the original would have run under,
/// marked as replaced by the prefix's mod. Calls inside the original method's body, transpiled
/// code included, are left alone. This only reaches replacements built after
/// <see cref="Activate"/>. After <see cref="Deactivate"/>, those replacements go on calling
/// <see cref="Start"/> and <see cref="Stop"/>, which then only check that timing is off.
/// </para>
/// <para>
/// Everything here runs inside every other mod's patching, so none of it may fail into
/// Harmony. Only <see cref="Internals"/> names Harmony's internal members; each of its methods
/// is called inside a try block, so a Harmony without them fails there, at compile time of
/// that method. Timing starts only for the major version of Harmony this was built against, with
/// the internal methods shaped as expected, and only after a self-test on this thread passes. Any later failure turns timing off for
/// good. A replacement that fails to build after its hook calls were timed is built again
/// without them, and timing only counts as failed when that second build succeeds.
/// </para>
/// </remarks>
internal static class HookTiming
{
    internal const string Category = "LoadingProgress.StartupImpact.HarmonyHook";
    internal const string HarmonyId = "ilyvion.LoadingProgress.HookTiming";
    private const string SelfTestOpen = "LoadingProgress.StartupImpact.HookTimingSelfTest";

#if v1_7
    private const int HarmonyMajor = 3;
#else
    private const int HarmonyMajor = 2;
#endif

    // The operands of the nops Harmony puts around the original method's body.
    private const string OriginalStart = "start original";
    private const string OriginalEnd = "end original";

    private static readonly Harmony _harmony = new(HarmonyId);
    private static readonly Lock _lock = new();
    private static readonly Dictionary<(MethodBase Original, MethodInfo Hook), int?> _ids = [];

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

    // Whether this thread is building a replacement again without timed hook calls.
    [ThreadStatic]
    private static bool _buildingUntimed;

    // The thread running the self-test, which times hooks before _active is set.
    private static volatile Thread? _selfTestThread;

    // Replacer is the owner's name for a prefix that can skip the original, else null.
    private sealed record Timed(Profiler Profiler, string Category, string? Replacer);

    // A replacement being built: Harmony's MethodCreator for it, and whether any of its hook
    // calls were timed.
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
    /// Stops timing hooks. Never throws.
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
        UnsupportedHarmony(
            version,
            static () => (Internals.CreateReplacement(), Internals.Rewrite())
        );

    /// <summary>
    /// Why hook timing cannot run on the Harmony of <paramref name="version"/>, finding its
    /// internal methods that build a replacement and rewrite its instructions with
    /// <paramref name="findInternals"/>, which fails on a Harmony without them, or
    /// <see langword="null"/> when it can.
    /// </summary>
    internal static string? UnsupportedHarmony(
        Version? version,
        Func<(MethodInfo? CreateReplacement, MethodInfo? Rewrite)> findInternals
    )
    {
        try
        {
            var (createReplacement, rewrite) = findInternals();
            return UnsupportedHarmony(version, createReplacement, rewrite);
        }
        catch (Exception)
        {
            return UnsupportedHarmony(version, null, null);
        }
    }

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
        version?.Major != HarmonyMajor
            ? $"this only works with Harmony {HarmonyMajor}, and Harmony {version} is running."
        : createReplacement is not { IsStatic: false }
        || createReplacement.GetParameters().Length != 0
        || createReplacement.ReturnType != typeof((MethodInfo, Dictionary<int, CodeInstruction>))
            ? $"Harmony {version} builds patched methods differently from what this expects."
        : !IsRewrite(rewrite)
            ? $"Harmony {version} rewrites patched methods differently from what this expects."
        : null;

    // Whether rewrite takes the finished instructions of a replacement as RewritePrefix expects.
    private static bool IsRewrite(MethodInfo? rewrite) =>
#if v1_7
        rewrite is { IsStatic: true }
        && rewrite.ReturnType == typeof(void)
        && rewrite.GetParameters() is [_, _, { Name: "codeInstructions" } instructions]
        && instructions.ParameterType == typeof(List<CodeInstruction>);
#else
        rewrite is { IsStatic: true }
        && rewrite.ReturnType == typeof(List<CodeInstruction>)
        && rewrite.GetParameters() is [{ Name: "instructions" } instructions, ..]
        && instructions.ParameterType == typeof(List<CodeInstruction>);
#endif

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

    // Builds the replacement again without timed hook calls when it failed with them, and
    // turns timing off only when that build succeeds.
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

        _buildingUntimed = true;
        try
        {
            __result = Internals.CreateReplacementAgain(__instance);
        }
        catch (Exception e)
        {
            return e;
        }
        finally
        {
            _buildingUntimed = false;
        }

        Fault($"a patched method failed to build with its hooks timed: {__exception}");
        return null;
    }

#if v1_7
    // Harmony also emits the helper method of each infix here, each under a creator of its own.
    private static void RewritePrefix(object __0, List<CodeInstruction> codeInstructions)
    {
        if (_building is { } building && building.Creator == __0)
        {
            TimeHookCalls(building, codeInstructions);
        }
    }
#else
    private static void RewritePrefix(List<CodeInstruction> instructions)
    {
        if (_building is { } building)
        {
            TimeHookCalls(building, instructions);
        }
    }
#endif

    private static void TimeHookCalls(Building building, List<CodeInstruction> instructions)
    {
        if (!Timing || _buildingUntimed)
        {
            return;
        }

        try
        {
            var (original, prefixes, postfixes, finalizers) = Internals.Hooks(building.Creator);
            var hooks = new HashSet<MethodInfo>(prefixes.Concat(postfixes).Concat(finalizers));
            var inOriginal = false;
            for (var i = 0; i < instructions.Count; i++)
            {
                var instruction = instructions[i];
                if (
                    instruction.opcode == OpCodes.Nop
                    && instruction.operand is OriginalStart or OriginalEnd
                )
                {
                    inOriginal = instruction.operand is OriginalStart;
                }
                else if (
                    !inOriginal
                    && instruction.opcode == OpCodes.Call
                    && instruction.operand is MethodInfo hook
                    && hooks.Contains(hook)
                )
                {
                    var canSkip = hook.ReturnType == typeof(bool) && prefixes.Contains(hook);
                    if (IdFor(original, hook, canSkip) is { } id)
                    {
                        building.Rewrote = true;
                        i += TimeCall(instructions, i, id, canSkip);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Fault($"could not time the hooks of a patched method: {e}");
        }
    }

    // The id that times hook on original, registered once per pair, or null for a hook that is
    // not timed: one of Loading Progress's own, one no mod owns, or one that failed to register.
    private static int? IdFor(MethodBase original, MethodInfo hook, bool canSkip)
    {
        lock (_lock)
        {
            if (_ids.TryGetValue((original, hook), out var known))
            {
                return known;
            }

            int? id = null;
            try
            {
                id = CreateId(original, hook, canSkip);
            }
            catch (Exception e)
            {
                LoadingProgressMod.Warning(
                    $"The Harmony hook {hook.DeclaringType?.FullName}.{hook.Name} will not be timed: {e}"
                );
            }
            _ids[(original, hook)] = id;
            return id;
        }
    }

    private static int? CreateId(MethodBase original, MethodInfo hook, bool canSkip)
    {
        var assembly = hook.DeclaringType?.Assembly;
        if (
            assembly == null
            || (assembly == typeof(HookTiming).Assembly && !SelfTestHooks.Contains(hook))
        )
        {
            return null;
        }

        var mod = Utilities.FindModByAssembly(assembly);
        var profiler =
            mod == null
                ? null
                : LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(mod)?.Profiler;
        return profiler == null
            ? null
            : Register(
                new Timed(
                    profiler,
                    $"{Category}|{original.DeclaringType?.Name}.{original.Name}",
                    canSkip ? mod!.Name : null
                )
            );
    }

    // Puts "ldc.i4 id; call Start" before the hook call at index and "ldc.i4 id; call Stop"
    // after it, or StopPrefix for a prefix that can skip the original, which takes the
    // prefix's result and returns it. The call's labels and the exception blocks it begins
    // move to the first inserted instruction, and the blocks it ends to the last. Returns how
    // many instructions were inserted.
    private static int TimeCall(List<CodeInstruction> instructions, int index, int id, bool canSkip)
    {
        var call = instructions[index];
        var loadIdBefore = new CodeInstruction(OpCodes.Ldc_I4, id);
        var stop = new CodeInstruction(OpCodes.Call, canSkip ? StopPrefixMethod : StopMethod);

        loadIdBefore.labels.AddRange(call.labels);
        call.labels.Clear();
        foreach (var block in call.blocks)
        {
            (
                block.blockType == ExceptionBlockType.EndExceptionBlock ? stop : loadIdBefore
            ).blocks.Add(block);
        }
        call.blocks.Clear();

        instructions.InsertRange(index + 1, [new CodeInstruction(OpCodes.Ldc_I4, id), stop]);
        instructions.InsertRange(
            index,
            [loadIdBefore, new CodeInstruction(OpCodes.Call, StartMethod)]
        );
        return 4;
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

    // Starts the hook's category. A hook called from inside a timer's own work runs untimed,
    // and Stop, called there too, then stops nothing. Nothing here may throw into the
    // replacement.
    internal static void Start(int id)
    {
        if (!Timing || Profiler.BusyOnThread)
        {
            return;
        }

        try
        {
            var timed = _byId[id];
            timed.Profiler.StartUnguarded(timed.Category);
        }
        catch (Exception e)
        {
            Fault($"timing a hook failed: {e}");
        }
    }

    // Stops the hook's category when Start started it.
    internal static void Stop(int id)
    {
        if (Profiler.BusyOnThread)
        {
            return;
        }

        try
        {
            var timed = _byId[id];
            timed.Profiler.StopUnguarded(timed.Category, null);
        }
        catch (Exception e)
        {
            Fault($"timing a hook failed: {e}");
        }
    }

    // Stops a prefix that can skip the original once it has returned runOriginal, which this
    // returns for the replacement.
    internal static bool StopPrefix(bool runOriginal, int id)
    {
        if (Profiler.BusyOnThread)
        {
            return runOriginal;
        }

        try
        {
            var timed = _byId[id];
            timed.Profiler.StopUnguarded(timed.Category, runOriginal ? null : timed.Replacer);
        }
        catch (Exception e)
        {
            Fault($"timing a hook failed: {e}");
        }
        return runOriginal;
    }

    private static readonly MethodInfo[] SelfTestHooks =
    [
        AccessTools.Method(typeof(HookTiming), nameof(SelfTestRunningPrefix)),
        AccessTools.Method(typeof(HookTiming), nameof(SelfTestPostfix)),
        AccessTools.Method(typeof(HookTiming), nameof(SelfTestPassThroughPostfix)),
        AccessTools.Method(typeof(HookTiming), nameof(SelfTestSkippingPrefix)),
    ];

    // Patches two methods of this class with one hook of each shape a timed call has,
    // calls them inside a category of Loading Progress's own, and checks that the hooks ran
    // as Harmony runs them and their time was recorded, then removes the patches and the
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

#if v1_7
        // Emits the finished instructions of a replacement, and of each infix's helper method.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static MethodInfo? Rewrite() =>
            AccessTools.Method(typeof(MethodCreatorTools), nameof(MethodCreatorTools.EmitCodes));

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (
            MethodBase Original,
            List<MethodInfo> Prefixes,
            List<MethodInfo> Postfixes,
            List<MethodInfo> Finalizers
        ) Hooks(object creator)
        {
            var config = ((MethodCreator)creator).config;
            return (
                config.original,
                config.prefixes.ConvertAll(call => call.method),
                config.postfixes.ConvertAll(call => call.method),
                config.finalizers.ConvertAll(call => call.method)
            );
        }
#else
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
#endif

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static (MethodInfo, Dictionary<int, CodeInstruction>) CreateReplacementAgain(
            object creator
        ) => new MethodCreator(((MethodCreator)creator).config).CreateReplacement();
    }
}
