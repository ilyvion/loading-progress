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
                LoadingProgressMod.Warning($"Mod constructor patch failed for {modType.FullName}. This means Loading Progress can't track its loading time impact. Exception:\n{ex}");
            }
            if (constructorInfo != null)
            {
                yield return constructorInfo;
            }
        }
    }

    public static Assembly? _currentModAssembly;

    internal static void Prefix([HarmonyArgument(0)] ModContentPack modContentPack, MethodBase __originalMethod)
    {
        _currentModAssembly = __originalMethod.DeclaringType.Assembly;
        StartupImpactProfilerUtil.StartModProfiler(modContentPack, "LoadingProgress.StartupImpact.ModConstructor");
    }

    internal static void Postfix([HarmonyArgument(0)] ModContentPack modContentPack)
    {
        _currentModAssembly = null;
        StartupImpactProfilerUtil.StopModProfiler(modContentPack, "LoadingProgress.StartupImpact.ModConstructor");
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
            __instance.PatchAll(Mod_Constructor_Patches._currentModAssembly);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchAll))]
    [HarmonyPatch([typeof(Assembly)])]
    [HarmonyPrefix]
    internal static bool PatchAllPrefix(Assembly assembly)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses || Mod_Constructor_Patches._currentModAssembly == null)
        {
            return true;
        }

        if (Mod_Constructor_Patches._currentModAssembly != assembly)
        {
            if (Mod_Constructor_Patches._currentModAssembly.FullName.StartsWith("HugsLib"))
            {
                // HugsLib runs patches on behalf of other mods; we can ignore this warning for it.
            }
            else
            {
                LoadingProgressMod.Warning($"Mod called PatchAll with a different assembly than itself: {Mod_Constructor_Patches._currentModAssembly.FullName} != {assembly.FullName}. This may cause issues.");
            }
        }

        return true;
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
            __instance.PatchAllUncategorized(Mod_Constructor_Patches._currentModAssembly);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchAllUncategorized))]
    [HarmonyPatch([typeof(Assembly)])]
    [HarmonyPrefix]
    internal static bool PatchAllUncategorizedPrefix(Assembly assembly)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses || Mod_Constructor_Patches._currentModAssembly == null)
        {
            return true;
        }

        if (Mod_Constructor_Patches._currentModAssembly != assembly)
        {
            LoadingProgressMod.Warning($"Mod called PatchAllUncategorized with a different assembly than itself: {Mod_Constructor_Patches._currentModAssembly!.FullName} != {assembly.FullName}. This may cause issues.");
        }

        return true;
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
            __instance.PatchCategory(Mod_Constructor_Patches._currentModAssembly, category);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.PatchCategory))]
    [HarmonyPatch([typeof(Assembly), typeof(string)])]
    [HarmonyPrefix]
    internal static bool PatchCategoryPrefix(Assembly assembly)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses || Mod_Constructor_Patches._currentModAssembly == null)
        {
            return true;
        }

        if (Mod_Constructor_Patches._currentModAssembly != assembly)
        {
            LoadingProgressMod.Warning($"Mod called PatchCategory with a different assembly than itself: {Mod_Constructor_Patches._currentModAssembly!.FullName} != {assembly.FullName}. This may cause issues.");
        }

        return true;
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
            __instance.UnpatchCategory(Mod_Constructor_Patches._currentModAssembly, category);
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Harmony), nameof(Harmony.UnpatchCategory))]
    [HarmonyPatch([typeof(Assembly), typeof(string)])]
    [HarmonyPrefix]
    internal static bool UnpatchCategoryPrefix(Assembly assembly)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.LoadingModClasses || Mod_Constructor_Patches._currentModAssembly == null)
        {
            return true;
        }

        if (Mod_Constructor_Patches._currentModAssembly != assembly)
        {
            LoadingProgressMod.Warning($"Mod called UnpatchCategory with a different assembly than itself: {Mod_Constructor_Patches._currentModAssembly!.FullName} != {assembly.FullName}. This may cause issues.");
        }

        return true;
    }
}
