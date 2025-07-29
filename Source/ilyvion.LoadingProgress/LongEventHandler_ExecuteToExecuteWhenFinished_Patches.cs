using System.Reflection;
using System.Reflection.Emit;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.ExecuteToExecuteWhenFinished))]
internal static class LongEventHandler_ExecuteToExecuteWhenFinished_Patches
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

        var methods = FindMethodThatCallsStaticConstructorOnStartupUtilityCallAll();
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

        LongEventHandler.executingToExecuteWhenFinished = true;
        if (LongEventHandler.toExecuteWhenFinished.Count > 0)
        {
            DeepProfiler.Start("ExecuteToExecuteWhenFinished()");
        }
        var reloadContentCount = LongEventHandler.toExecuteWhenFinished.Count(te => te.Method.Name.Contains("ReloadContent"));
        for (int i = 0; i < LongEventHandler.toExecuteWhenFinished.Count; i++)
        {
            if (!StaticConstructorOnStartupUtilityReplacement._callAllCalled
                && LongEventHandler.toExecuteWhenFinished[i].Method == staticConstructorOnStartupUtilityCallAllMethod)
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

            string label = LongEventHandler.toExecuteWhenFinished[i].Method.DeclaringType.ToString() + " -> " + LongEventHandler.toExecuteWhenFinished[i].Method.ToString();
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
                    if (LongEventHandler.toExecuteWhenFinished[i].Method.Name.Contains("ReloadContent"))
                    {
                        LoadingProgressWindow.StageProgress = (i + 1, reloadContentCount);
                    }
                    else
                    {
                        LoadingProgressWindow.StageProgress = (i + 1, LongEventHandler.toExecuteWhenFinished.Count);
                    }
                }
                LongEventHandler.toExecuteWhenFinished[i]();
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

    private static readonly MethodInfo _method_StaticConstructorOnStartupUtility_CallAll
        = AccessTools.Method(
            typeof(StaticConstructorOnStartupUtility), nameof(StaticConstructorOnStartupUtility.CallAll));

    private static readonly CodeMatch[] toMatch =
    [
        new(OpCodes.Call, _method_StaticConstructorOnStartupUtility_CallAll),
    ];
    private static IEnumerable<MethodInfo> FindMethodThatCallsStaticConstructorOnStartupUtilityCallAll()
    {
        // Find all possible candidates, both from the wrapping type and all nested types.
        var candidates = AccessTools.GetDeclaredMethods(typeof(PlayDataLoader)).ToHashSet();
        candidates.AddRange(typeof(PlayDataLoader).GetNestedTypes(AccessTools.all).SelectMany(AccessTools.GetDeclaredMethods));

        //check all candidates for the target instructions, return those that match.
        foreach (var method in candidates)
        {
            var instructions = PatchProcessor.GetCurrentInstructions(method);
            var matched = instructions.Matches(toMatch);
            if (matched)
            {
                yield return method;
            }
        }
        yield break;
    }
}
