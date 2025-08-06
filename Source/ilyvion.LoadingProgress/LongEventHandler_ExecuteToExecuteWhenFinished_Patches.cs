using System.Reflection;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.ExecuteToExecuteWhenFinished))]
internal static partial class LongEventHandler_ExecuteToExecuteWhenFinished_Patches
{
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
        if (LongEventHandler.executingToExecuteWhenFinished)
        {
            Log.Warning("Already executing.");
            yield break;
        }

        HashSet<ModContentPack>? fasterGameLoadingLoadedMods = null;
        if (ModsConfig.ActiveModsInLoadOrder.Any(mod => mod.PackageId.Equals("taranchuk.fastergameloading", StringComparison.CurrentCultureIgnoreCase)))
        {
            fasterGameLoadingLoadedMods = AccessTools.Field("FasterGameLoading.ModContentPack_ReloadContentInt_Patch:loadedMods")
                .GetValue(null) as HashSet<ModContentPack>;
        }

        var methods = StaticConstructorOnStartupCallAllFinder.FindMethod();
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

        var methodFields = ReloadContentIntFinder.FindMethod();
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

            if (toExecuteWhenFinished is { } action
                && action.Method == reloadContentIntMethod
                && action.Target.GetType() == reloadContentIntMethod.DeclaringType
                && reloadContentIntmodContentPackField is not null)
            {
                ModContentPack modContentPack = (ModContentPack)reloadContentIntmodContentPackField.GetValue(action.Target)!;
                bool skipReload = false;
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
                    continue;
                }
            }
            else if (toExecuteWhenFinished is Action action2 && action2.Method == reloadContentIntMethod)
            {
                LoadingProgressMod.Error("ReloadContentInt was called with target being "
                + action2.Target.GetType().FullName + ":"
                + action2.Target + ", but we expected it to be " + reloadContentIntMethod.DeclaringType.FullName + ":" + reloadContentIntMethod);
            }

            string label = toExecuteWhenFinished.Method.DeclaringType.ToString() + " -> " + toExecuteWhenFinished.Method.ToString();
            DeepProfiler.Start(label);
            try
            {
                if (LoadingProgressWindow.CurrentStage is
                    LoadingStage.ExecuteToExecuteWhenFinished or LoadingStage.ExecuteToExecuteWhenFinished2)
                {
                    if (!label.Contains("ModContentPack") || !label.Contains("ReloadContent"))
                    {
                        LoadingProgressWindow.SetCurrentLoadingActivityRaw(label);
                    }
                    LoadingProgressWindow.StageProgress = (i + 1, LongEventHandler.toExecuteWhenFinished.Count);
                }
                toExecuteWhenFinished();
            }
            catch (Exception ex)
            {
                Log.Error("Could not execute post-long-event action. Exception: " + ex);
            }
            finally
            {
                DeepProfiler.End();
            }
            yield return null;
        }
        if (LongEventHandler.toExecuteWhenFinished.Count > 0)
        {
            DeepProfiler.End();
        }
        LongEventHandler.toExecuteWhenFinished.Clear();
        LongEventHandler.executingToExecuteWhenFinished = false;
    }
}
