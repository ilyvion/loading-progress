using System.Reflection;
using ilyvion.LoadingProgress.FasterGameLoading;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.ExecuteToExecuteWhenFinished))]
internal static partial class LongEventHandler_ExecuteToExecuteWhenFinished_Patches
{
    private static bool _hasWarnedAboutReloadIntPatches;

    private static bool Prepare()
    {
        if (!LoadingProgressMod.Settings.PatchInitialization)
        {
            LoadingProgressMod.Message("Patching of initialization code is disabled in the settings, skipping patch.");
            return false;
        }
        return true;
    }

    private static bool Prefix()
    {
        if (LongEventHandler.toExecuteWhenFinished.Count > 0 && LoadingProgressWindow.CurrentStage != LoadingStage.Finished)
        {
            LoadingProgressMod.Debug("Running Enumerable version of ExecuteToExecuteWhenFinished() called with " + LongEventHandler.toExecuteWhenFinished.Count + " actions to execute.\n" + Environment.StackTrace);
            Utilities.LongEventHandlerPrependQueue(() =>
            {
                LongEventHandler.QueueLongEvent(ExecuteToExecuteWhenFinished(), "LoadingProgress.ExecuteToExecuteWhenFinished");
            });
            return false;
        }
        return true;
    }

    internal static IEnumerable ExecuteToExecuteWhenFinished()
    {
        var patchReloadContent = LoadingProgressMod.Settings.PatchReloadContent;

        if (LongEventHandler.executingToExecuteWhenFinished)
        {
            Log.Warning("Already executing.");
            yield break;
        }

        HashSet<ModContentPack>? fasterGameLoadingLoadedMods = null;
        if (FasterGameLoadingUtils.HasFasterGameLoading)
        {
            fasterGameLoadingLoadedMods = FasterGameLoadingUtils.LoadedMods;
        }

        var methods = StaticConstructorOnStartupCallAllFinder.FindMethodCalling();
        MethodInfo? staticConstructorOnStartupUtilityCallAllMethod = null;
        if (methods.Count() != 1)
        {
            LoadingProgressMod.Error("Could not find call to StaticConstructorOnStartupUtility.CallAll in PlayDataLoader; "
                + "static constructor execution will be done without showing progress.");
        }
        else
        {
            staticConstructorOnStartupUtilityCallAllMethod = methods.First();
        }

        var methodFields = ReloadContentIntFinder.FindMethodCalling();
        MethodInfo? reloadContentIntMethod = null;
        FieldInfo? reloadContentIntmodContentPackField = null;
        if (methodFields.Count() != 1)
        {
            LoadingProgressMod.Error("Could not find call to ModContentPack.ReloadContentInt in ModContentPack; "
                + "reloading content will be done without showing detailed progress.");
        }
        else
        {
            (reloadContentIntMethod, reloadContentIntmodContentPackField) = methodFields.First();
        }

        LongEventHandler.executingToExecuteWhenFinished = true;
        if (LongEventHandler.toExecuteWhenFinished.Count > 0)
        {
            DeepProfiler.Start("ExecuteToExecuteWhenFinished()");
        }
        var reloadContentStepCount = LongEventHandler.toExecuteWhenFinished.Count(te => te.Method.Name.Contains("ReloadContent")) * 4;
        var reloadContentStepCounter = 0;
        for (int i = 0; i < LongEventHandler.toExecuteWhenFinished.Count; i++)
        {
            Action toExecuteWhenFinished = LongEventHandler.toExecuteWhenFinished[i];

            if (!StaticConstructorOnStartupUtilityReplacement._callAllCalled
                && toExecuteWhenFinished.Method == staticConstructorOnStartupUtilityCallAllMethod)
            {
                // If this is the StaticConstructorOnStartupUtility.CallAll method, we want to run it and bail to
                // let it do its own QueueLongEvent.
                StaticConstructorOnStartupUtilityReplacement.Interject();

                DeepProfiler.End();
                LongEventHandler.toExecuteWhenFinished.RemoveRange(0, i);
                LongEventHandler.executingToExecuteWhenFinished = false;

                LoadingProgressMod.Debug("StaticConstructorOnStartupUtility.CallAll was up, "
                    + "interrupting ExecuteToExecuteWhenFinished.");

                yield break;
            }

            bool skipReload = false;
            if (patchReloadContent
                && toExecuteWhenFinished is { } action
                && action.Method == reloadContentIntMethod
                && action.Target.GetType() == reloadContentIntMethod.DeclaringType
                && reloadContentIntmodContentPackField is not null)
            {
                // Pause Faster Game Loading's content loader; we're taking over now.
                FasterGameLoading_DelayedActions_LateUpdate_Patches._pauseFasterGameLoading_DelayedActions_LateUpdate = true;

                // We replace ReloadContentInt with our own enumerated implementation and do not let the original run,
                // so transpilers might not work as expected. Warn players about potential issues.
                if (!_hasWarnedAboutReloadIntPatches)
                {
                    Utilities.WarnAboutPatches(
                        AccessTools.Method(typeof(ModContentPack), nameof(ModContentPack.ReloadContentInt)),
                        false,
                        warnKinds: PatchKinds.Transpiler);
                    _hasWarnedAboutReloadIntPatches = true;
                }

                ModContentPack modContentPack = (ModContentPack)reloadContentIntmodContentPackField.GetValue(action.Target)!;
                ModContentPack_ReloadContentInt_Patch.CurrentModContentPack = modContentPack;
                if (fasterGameLoadingLoadedMods is not null)
                {
                    skipReload = fasterGameLoadingLoadedMods.Contains(modContentPack);
                    if (skipReload)
                    {
                        // Skipping reloading content for {modContentPack.Name} because Faster Game Loading has already loaded it.
                        reloadContentStepCounter += 4; // Skip the 4 steps of reloading content.
                    }
                    else
                    {
                        // We add this mod to the list of loaded mods so Faster Game Loading skips it.
                        _ = fasterGameLoadingLoadedMods.Add(modContentPack);
                    }
                }

                if (!skipReload)
                {
                    // Reloading content for {modContentPack.Name}.
                    foreach (var value in ReloadContentIntReplacement.ReloadContentInt(modContentPack))
                    {
                        LoadingDataTracker.Current = modContentPack.Name;
                        LoadingProgressWindow.CurrentLoadingActivity = $"LP.Reload {value}";
                        LoadingProgressWindow.StageProgress = (reloadContentStepCounter + 1, reloadContentStepCount);
                        yield return value;
                        reloadContentStepCounter++;
                    }
                    // Run the original method to let other mods' prefixes and postfixes run
                    modContentPack.ReloadContentInt();
                    yield return null;
                }
                else
                {
                }
                continue;
            }
            else if (toExecuteWhenFinished is Action action2 && action2.Method == reloadContentIntMethod)
            {
                LoadingProgressMod.Error("ReloadContentInt was called with target being "
                + action2.Target.GetType().FullName + ":"
                + action2.Target + ", but we expected it to be " + reloadContentIntMethod.DeclaringType.FullName + ":" + reloadContentIntMethod);
            }

            ModContentPack_ReloadContentInt_Patch.CurrentModContentPack = null;

            string label = toExecuteWhenFinished.Method.DeclaringType.ToString() + " -> " + toExecuteWhenFinished.Method.ToString();
            if (LoadingProgressWindow.CurrentStage is
                LoadingStage.ExecuteToExecuteWhenFinished or LoadingStage.ExecuteToExecuteWhenFinished2)
            {
                if (!label.Contains("ModContentPack") || !label.Contains("ReloadContent"))
                {
                    LoadingProgressWindow.SetCurrentLoadingActivityRaw(label);
                }
                LoadingProgressWindow.StageProgress = (i + 1, LongEventHandler.toExecuteWhenFinished.Count);
            }
            yield return null;
            DeepProfiler.Start(label);
            try
            {
                DateTime now = DateTime.Now;
                LoadingProgressMod.Debug($"About to call {label} @ {now:HH:mm:ss.fff}");
                toExecuteWhenFinished();
                LoadingProgressMod.Debug($"Finished calling {label} @ {DateTime.Now:HH:mm:ss.fff}; took {DateTime.Now - now:mm\\:ss\\.fff}");
            }
            catch (Exception ex)
            {
                Log.Error("Could not execute post-long-event action. Exception: " + ex);
            }
            finally
            {
                DeepProfiler.End();
            }
        }
        if (LongEventHandler.toExecuteWhenFinished.Count > 0)
        {
            DeepProfiler.End();
        }
        LongEventHandler.toExecuteWhenFinished.Clear();
        LongEventHandler.executingToExecuteWhenFinished = false;
        FasterGameLoading_DelayedActions_LateUpdate_Patches._pauseFasterGameLoading_DelayedActions_LateUpdate = false;
    }
}
