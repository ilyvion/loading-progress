using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("StartupImpact")]
internal static class Mod_Constructor_Patches
{
    internal static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (var modType in typeof(Mod).InstantiableDescendantsAndSelf())
        {
            ConstructorInfo? constructorInfo = null;
            try
            {
                constructorInfo = AccessTools.Constructor(modType, [typeof(ModContentPack)]);
            }
            catch (Exception ex)
            {
                LoadingProgressMod.Warning(
                    $"Mod constructor patch failed for {modType.FullName}. This means Loading Progress can't track its loading time impact. Exception:\n{ex}"
                );
            }
            if (constructorInfo != null)
            {
                yield return constructorInfo;
            }
        }
    }

    public static Assembly? _currentModAssembly;

    private const string MultiplayerApiTypeName = "Multiplayer.API.MP";
    private const string RegisterAllName = "RegisterAll";

    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        MethodBase original
    ) =>
        original.DeclaringType is { } constructorType
            ? RedirectRegisterAll(instructions, constructorType, MultiplayerApiTypeName)
            : instructions;

    /// <summary>
    /// Turns each call to the API's parameterless <c>RegisterAll()</c> into a call to its
    /// <see cref="Assembly"/> overload, passing the assembly of <paramref name="constructorType"/>.
    /// </summary>
    /// <remarks>
    /// That method registers the assembly of the method calling it, found by walking the stack,
    /// and in a patched constructor that's Harmony's replacement, not the mod. The call site is
    /// rewritten because patching the API method would run its class's static constructor, which
    /// decides <c>MP.enabled</c> for good, before Multiplayer's assembly has loaded.
    /// </remarks>
    internal static IEnumerable<CodeInstruction> RedirectRegisterAll(
        IEnumerable<CodeInstruction> instructions,
        Type constructorType,
        string apiTypeName
    )
    {
        foreach (var instruction in instructions)
        {
            var overload =
                instruction.operand is MethodInfo { Name: RegisterAllName, IsStatic: true } called
                && called.DeclaringType?.FullName == apiTypeName
                && called.GetParameters().Length == 0
                    ? AccessTools.DeclaredMethod(
                        called.DeclaringType,
                        RegisterAllName,
                        [typeof(Assembly)]
                    )
                    : null;
            if (overload == null)
            {
                yield return instruction;
                continue;
            }

            yield return new CodeInstruction(OpCodes.Ldtoken, constructorType)
                .MoveLabelsFrom(instruction)
                .MoveBlocksFrom(instruction);
            yield return new CodeInstruction(
                OpCodes.Call,
                AccessTools.Method(typeof(Type), nameof(Type.GetTypeFromHandle))
            );
            yield return new CodeInstruction(
                OpCodes.Callvirt,
                AccessTools.PropertyGetter(typeof(Type), nameof(Type.Assembly))
            );
            yield return new CodeInstruction(OpCodes.Call, overload);
        }
    }

    internal static void Prefix(
        [HarmonyArgument(0)] ModContentPack modContentPack,
        MethodBase __originalMethod,
        out bool __state
    )
    {
        _currentModAssembly = __originalMethod.DeclaringType?.Assembly;
        StartupImpactProfilerUtil.StartModProfiler(
            modContentPack,
            "LoadingProgress.StartupImpact.ModConstructor"
        );
        __state = true;
    }

    internal static void Postfix(
        [HarmonyArgument(0)] ModContentPack modContentPack,
        ref bool __state
    ) => Stop(modContentPack, ref __state);

    // The engine catches a mod constructor that throws and goes on loading, and a postfix does
    // not run then, so the finalizer closes the category.
    internal static void Finalizer(
        [HarmonyArgument(0)] ModContentPack modContentPack,
        bool __state
    ) => Stop(modContentPack, ref __state);

    private static void Stop(ModContentPack modContentPack, ref bool started)
    {
        _currentModAssembly = null;
        StartupImpactProfilerUtil.StopModOnce(
            ref started,
            modContentPack,
            "LoadingProgress.StartupImpact.ModConstructor"
        );
    }
}

// This is frankly madness, but because we've patched all the mod constructors, we need to change
// what calling something like Harmony.PatchAll sees as the "parent" assembly, because once a
// method is patched, that suddenly lives in the patched assembly instead of the original. So
// we store the active constructor assembly in Mod_Constructor_Patches._currentModAssembly and
// then we adjust calls to the non-assembly specific Harmony methods to use the current mod assembly.

// We only do this during the LoadingModClasses stage though, to avoid messing with Harmony elsewhere.

[HarmonyPatch]
[HarmonyPatchCategory("StartupImpact")]
internal static class Harmony_Patches
{
    private static Assembly? _lastAssembly;

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchAll))]
    [HarmonyPatch([])]
    [HarmonyPrefix]
    internal static bool PatchAllPrefix(Harmony __instance)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses)
        {
            return true;
        }

        if (Mod_Constructor_Patches._currentModAssembly != null)
        {
            _lastAssembly = Mod_Constructor_Patches._currentModAssembly;
            Mod_Constructor_Patches._currentModAssembly = null;
            __instance.PatchAll(_lastAssembly);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchAll))]
    [HarmonyPatch([])]
    [HarmonyFinalizer]
    internal static void PatchAllFinalizer()
    {
        Mod_Constructor_Patches._currentModAssembly = _lastAssembly;
        _lastAssembly = null;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchAll))]
    [HarmonyPatch([typeof(Assembly)])]
    [HarmonyPrefix]
    internal static void PatchAllPrefix(Assembly assembly)
    {
        if (
            LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses
            || Mod_Constructor_Patches._currentModAssembly == null
        )
        {
            return;
        }

        if (Mod_Constructor_Patches._currentModAssembly != assembly)
        {
            if (
                Mod_Constructor_Patches._currentModAssembly.FullName?.StartsWith(
                    "HugsLib",
                    StringComparison.Ordinal
                ) == true
            )
            {
                // HugsLib runs patches on behalf of other mods; we can ignore this warning for it.
            }
            else
            {
                LoadingProgressMod.Warning(
                    $"Mod called PatchAll with a different assembly than itself: {Mod_Constructor_Patches._currentModAssembly.FullName} != {assembly.FullName}. This may cause issues."
                );
            }
        }

        return;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchAllUncategorized))]
    [HarmonyPatch([])]
    [HarmonyPrefix]
    internal static bool PatchAllUncategorizedPrefix(Harmony __instance)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses)
        {
            return true;
        }

        if (Mod_Constructor_Patches._currentModAssembly != null)
        {
            _lastAssembly = Mod_Constructor_Patches._currentModAssembly;
            Mod_Constructor_Patches._currentModAssembly = null;
            __instance.PatchAllUncategorized(_lastAssembly);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchAllUncategorized))]
    [HarmonyPatch([])]
    [HarmonyFinalizer]
    internal static void PatchAllUncategorizedFinalizer()
    {
        Mod_Constructor_Patches._currentModAssembly = _lastAssembly;
        _lastAssembly = null;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchAllUncategorized))]
    [HarmonyPatch([typeof(Assembly)])]
    [HarmonyPrefix]
    internal static void PatchAllUncategorizedPrefix(Assembly assembly)
    {
        if (
            LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses
            || Mod_Constructor_Patches._currentModAssembly == null
        )
        {
            return;
        }

        if (Mod_Constructor_Patches._currentModAssembly != assembly)
        {
            LoadingProgressMod.Warning(
                $"Mod called PatchAllUncategorized with a different assembly than itself: {Mod_Constructor_Patches._currentModAssembly.FullName} != {assembly.FullName}. This may cause issues."
            );
        }

        return;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchCategory))]
    [HarmonyPatch([typeof(string)])]
    [HarmonyPrefix]
    internal static bool PatchCategoryPrefix(Harmony __instance, string category)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses)
        {
            return true;
        }

        if (Mod_Constructor_Patches._currentModAssembly != null)
        {
            _lastAssembly = Mod_Constructor_Patches._currentModAssembly;
            Mod_Constructor_Patches._currentModAssembly = null;
            __instance.PatchCategory(_lastAssembly, category);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchCategory))]
    [HarmonyPatch([typeof(string)])]
    [HarmonyFinalizer]
    internal static void PatchCategoryFinalizer()
    {
        Mod_Constructor_Patches._currentModAssembly = _lastAssembly;
        _lastAssembly = null;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchCategory))]
    [HarmonyPatch([typeof(Assembly), typeof(string)])]
    [HarmonyPrefix]
    internal static void PatchCategoryPrefix(Assembly assembly)
    {
        if (
            LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses
            || Mod_Constructor_Patches._currentModAssembly == null
        )
        {
            return;
        }

        if (Mod_Constructor_Patches._currentModAssembly != assembly)
        {
            LoadingProgressMod.Warning(
                $"Mod called PatchCategory with a different assembly than itself: {Mod_Constructor_Patches._currentModAssembly.FullName} != {assembly.FullName}. This may cause issues."
            );
        }

        return;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.UnpatchCategory))]
    [HarmonyPatch([typeof(string)])]
    [HarmonyPrefix]
    internal static bool UnpatchCategoryPrefix(Harmony __instance, string category)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses)
        {
            return true;
        }

        if (Mod_Constructor_Patches._currentModAssembly != null)
        {
            _lastAssembly = Mod_Constructor_Patches._currentModAssembly;
            Mod_Constructor_Patches._currentModAssembly = null;
            __instance.UnpatchCategory(_lastAssembly, category);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.UnpatchCategory))]
    [HarmonyPatch([typeof(string)])]
    [HarmonyFinalizer]
    internal static void UnpatchCategoryFinalizer()
    {
        Mod_Constructor_Patches._currentModAssembly = _lastAssembly;
        _lastAssembly = null;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.UnpatchCategory))]
    [HarmonyPatch([typeof(Assembly), typeof(string)])]
    [HarmonyPrefix]
    internal static void UnpatchCategoryPrefix(Assembly assembly)
    {
        if (
            LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses
            || Mod_Constructor_Patches._currentModAssembly == null
        )
        {
            return;
        }

        if (Mod_Constructor_Patches._currentModAssembly != assembly)
        {
            LoadingProgressMod.Warning(
                $"Mod called UnpatchCategory with a different assembly than itself: {Mod_Constructor_Patches._currentModAssembly.FullName} != {assembly.FullName}. This may cause issues."
            );
        }

        return;
    }
}
