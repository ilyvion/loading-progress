using ilyvion.LoadingProgress.FasterGameLoading;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.ExecuteToExecuteWhenFinished))]
internal static partial class LongEventHandler_ExecuteToExecuteWhenFinished_Patches
{
    private static bool _hasWarnedAboutReloadIntPatches;

    // Whether the settings asked for this patch: the deferred-task replacement during loading
    // and the in-game repaint run only then. With tracking on, the patch is applied without
    // them too, for the deferred actions after loading alone.
    private static bool _settingsAskForThePatch;

    private static bool Prepare()
    {
        _settingsAskForThePatch =
            LoadingProgressMod.Settings.PatchInitialization
            || LoadingProgressMod.Settings.PatchInGameDeferredRepaint;
        if (
            !_settingsAskForThePatch
            && !LoadingProgressMod.instance.StartupImpact.WasTrackingEnabledAtStartup
        )
        {
            LoadingProgressMod.Message(
                "Patching of initialization code and in-game deferred-block repaint are both "
                    + "disabled in the settings, skipping patch."
            );
            return false;
        }
        return true;
    }

    private static bool Prefix()
    {
        if (
            _settingsAskForThePatch
            && LongEventHandler.toExecuteWhenFinished.Count > 0
            && LoadingProgressWindow.CurrentStage != LoadingStage.Finished
        )
        {
            //LoadingProgressMod.Debug("Running Enumerable version of ExecuteToExecuteWhenFinished() called with " + LongEventHandler.toExecuteWhenFinished.Count + " actions to execute.\n" + Environment.StackTrace);
            Utilities.LongEventHandlerPrependQueue(() =>
            {
                LongEventHandler.QueueLongEvent(
                    ExecuteToExecuteWhenFinished(),
                    "LoadingProgress.ExecuteToExecuteWhenFinished"
                );
            });
            return false;
        }

        // In the wait after loading, each action is timed under its own mod, not the event it
        // runs after.
        if (
            HandsTheQueueToTheTail(
                LongEventHandler.toExecuteWhenFinished.Count,
                LoadingProgressWindow.CurrentStage,
                PostLoadTracker.IsTimingTheTail,
                UnityData.IsInMainThread
            )
        )
        {
            PostLoadTracker.RunDeferredActions();
            return false;
        }

        if (
            _settingsAskForThePatch
            && LongEventHandler.toExecuteWhenFinished.Count > 0
            && LoadingProgressWindow.CurrentStage == LoadingStage.Finished
            && LoadingProgressMod.Settings.PatchInGameDeferredRepaint
            && InGameLoadingSession.IsActive
            && InGameDeferredActionReplacement.ContainsKnownSlowAction(
                LongEventHandler.toExecuteWhenFinished
            )
        )
        {
            // Vanilla's callers (UpdateCurrentAsynchronousEvent etc.) call this method, the
            // event's callback and null out currentEvent all within the same Update(); since the
            // replacement below spreads toExecuteWhenFinished across multiple frames, the
            // callback is pulled out and null'd here so the vanilla caller's own
            // callback?.Invoke() right after this method returns becomes a no-op. It's invoked
            // for real from InGameDeferredActionReplacement.ExecuteToExecuteWhenFinished, only
            // once every deferred action has actually finished - preserving vanilla's "deferred
            // actions complete before the callback runs" order even though it's now spread
            // across frames. currentEvent itself still gets null'd early by the vanilla caller
            // regardless, same as it already does for the startup redirect above;
            // LongEventHandlerPrependQueue keeps the event queue non-empty across that gap so no
            // session-end/repaint hiccup results from it.
            var callback = LongEventHandler.currentEvent?.callback;
            // IDE0031 and IDE0058 disagree over whether this null check should collapse into a
            // null-conditional assignment; the explicit form satisfies both.
#pragma warning disable IDE0031
            if (LongEventHandler.currentEvent != null)
            {
                LongEventHandler.currentEvent.callback = null;
            }
#pragma warning restore IDE0031
            Utilities.LongEventHandlerPrependQueue(() =>
            {
                LongEventHandler.QueueLongEvent(
                    InGameDeferredActionReplacement.ExecuteToExecuteWhenFinished(callback),
                    InGameLoadingSession.DeferredRedirectEventTextKey
                );
            });
            return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the deferred actions are run by
    /// <see cref="PostLoadTracker.RunDeferredActions()"/>: some are queued, loading has
    /// finished, the wait after loading is being timed, and this is the main thread, where
    /// everything after loading runs. A call from another thread, which the engine makes run
    /// the queue there, is left to the engine, untimed.
    /// </summary>
    internal static bool HandsTheQueueToTheTail(
        int queued,
        LoadingStage stage,
        bool timingTheTail,
        bool onMainThread
    ) => queued > 0 && stage == LoadingStage.Finished && timingTheTail && onMainThread;

#pragma warning disable CA1502
    internal static IEnumerable ExecuteToExecuteWhenFinished()
#pragma warning restore CA1502
    {
        // Once we start with the ExecuteToExecuteWhenFinished,
        // we need to switch the active thread ID
        LoadingProgressMod.instance.StartupImpact.UpdateActiveThreadId();

        var patchReloadContent = LoadingProgressMod.Settings.PatchReloadContent;
        var timeDeferredActions = LoadingProgressMod
            .instance
            .StartupImpact
            .WasTrackingEnabledAtStartup;

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

        var methods = StaticConstructorOnStartupCallAllFinder.FindMethodCalling().ToList();
        MethodInfo? staticConstructorOnStartupUtilityCallAllMethod = null;
        if (methods.Count != 1)
        {
            LoadingProgressMod.Error(
                "Could not find call to StaticConstructorOnStartupUtility.CallAll "
                    + "in PlayDataLoader; "
                    + "static constructor execution will be done without showing progress."
            );
        }
        else
        {
            staticConstructorOnStartupUtilityCallAllMethod = methods.First();
        }

        var methodFields = ReloadContentIntFinder.FindMethodCalling().ToList();
        MethodInfo? reloadContentIntMethod = null;
        FieldInfo? reloadContentIntmodContentPackField = null;
        if (methodFields.Count != 1)
        {
            LoadingProgressMod.Error(
                "Could not find call to ModContentPack.ReloadContentInt in ModContentPack; "
                    + "reloading content will be done without showing detailed progress."
            );
        }
        else
        {
            (reloadContentIntMethod, reloadContentIntmodContentPackField) = methodFields.First();
        }

        // This resumed pass runs after the atlas baking and the garbage collection, so the
        // window's stage rule for it no longer fires. The stage ledger still begins the stage
        // here, or what the pass leaves untimed would be filed under the garbage collection.
        if (
            StaticConstructorOnStartupUtilityReplacement._callAllCalled
            && LoadingProgressWindow.CurrentStage > LoadingStage.ExecuteToExecuteWhenFinished2
            && LoadingProgressWindow.CurrentStage < LoadingStage.Finished
        )
        {
            LoadingProgressMod.instance.StartupImpact.NotifyStage(
                LoadingStage.ExecuteToExecuteWhenFinished2
            );
        }

        LongEventHandler.executingToExecuteWhenFinished = true;
        if (LongEventHandler.toExecuteWhenFinished.Count > 0)
        {
            DeepProfiler.Start("ExecuteToExecuteWhenFinished()");
        }
        var reloadContentStepCount =
            LongEventHandler.toExecuteWhenFinished.Count(te =>
                te.Method.Name.Contains("ReloadContent", StringComparison.Ordinal)
            ) * 4;
        var reloadContentStepCounter = 0;
        for (var i = 0; i < LongEventHandler.toExecuteWhenFinished.Count; i++)
        {
            var toExecuteWhenFinished = LongEventHandler.toExecuteWhenFinished[i];

            if (
                !StaticConstructorOnStartupUtilityReplacement._callAllCalled
                && toExecuteWhenFinished.Method == staticConstructorOnStartupUtilityCallAllMethod
            )
            {
                // If this is the StaticConstructorOnStartupUtility.CallAll method, we want to
                // run it and bail to let it do its own QueueLongEvent.
                StaticConstructorOnStartupUtilityReplacement.Interject();

                DeepProfiler.End();
                // Remove index i too (not just [0, i)): CallAllAndRest() already runs every
                // step of this closure itself (static ctors, FloatMenuMakerMap.Init, atlas
                // baking, GC), so the closure must not be invoked for real a second time when
                // we resume below - that would redo the non-idempotent-cost parts of it.
                LongEventHandler.toExecuteWhenFinished.RemoveRange(0, i + 1);
                LongEventHandler.executingToExecuteWhenFinished = false;

                // LoadingProgressMod.Debug("StaticConstructorOnStartupUtility.CallAll was up, "
                //     + "interrupting ExecuteToExecuteWhenFinished.");

                yield break;
            }

            var skipReload = false;
            if (
                patchReloadContent
                && toExecuteWhenFinished is { } action
                && action.Method == reloadContentIntMethod
                && action.Target.GetType() == reloadContentIntMethod.DeclaringType
                && reloadContentIntmodContentPackField is not null
            )
            {
                // Pause Faster Game Loading's content loader; we're taking over now.
                FasterGameLoading_DelayedActions_LateUpdate_Patches._pauseFasterGameLoading_DelayedActions_LateUpdate =
                    true;

                // We replace ReloadContentInt with our own enumerated implementation and do not
                // let the original run, so transpilers might not work as expected. Warn players
                // about potential issues.
                if (!_hasWarnedAboutReloadIntPatches)
                {
                    Utilities.WarnAboutPatches(
                        AccessTools.Method(
                            typeof(ModContentPack),
                            nameof(ModContentPack.ReloadContentInt)
                        ),
                        false,
                        warnKinds: PatchKinds.Transpiler
                    );
                    _hasWarnedAboutReloadIntPatches = true;
                }

                var modContentPack = (ModContentPack)
                    reloadContentIntmodContentPackField.GetValue(action.Target);
                ModContentPack_ReloadContentInt_Patch.CurrentModContentPack = modContentPack;
                if (fasterGameLoadingLoadedMods is not null)
                {
                    skipReload = fasterGameLoadingLoadedMods.Contains(modContentPack);
                    if (skipReload)
                    {
                        // Skipping reloading content for {modContentPack.Name} because
                        // Faster Game Loading has already loaded it.
                        reloadContentStepCounter += 4; // Skip the 4 steps of reloading content.
                    }
                    else
                    {
                        // We add this mod to the list of loaded mods so Faster Game Loading
                        // skips it.
                        _ = fasterGameLoadingLoadedMods.Add(modContentPack);
                    }
                }

                if (!skipReload)
                {
                    // Reloading content for {modContentPack.Name}.
                    // ReloadContentInt yields each step's label twice: once before doing the
                    // step's work and once again right after, purely so we get a chance to
                    // repaint with the correct label still showing before it moves on to the
                    // next step. Only count the first occurrence towards progress.
                    string? lastReloadStepLabel = null;
                    foreach (
                        var value in ReloadContentIntReplacement.ReloadContentInt(modContentPack)
                    )
                    {
                        var stepLabel = (string)value;
                        LoadingDataTracker.Current = modContentPack.Name;
                        LoadingProgressWindow.CurrentLoadingActivity = $"LP.Reload {stepLabel}";
                        if (
                            !string.Equals(stepLabel, lastReloadStepLabel, StringComparison.Ordinal)
                        )
                        {
                            reloadContentStepCounter++;
                            lastReloadStepLabel = stepLabel;
                        }
                        LoadingProgressWindow.StageProgress = (
                            reloadContentStepCounter,
                            reloadContentStepCount
                        );
                        // These labels are the ones most likely to sit right in front of a slow,
                        // synchronous step (a mod's texture/audio/asset reload), so they need to
                        // land on screen even if it means cutting the current 0.1s batch short.
                        LongEventHandler_UpdateCurrentEnumeratorEvent_Patches.RequestImmediateRepaint();
                        yield return value;
                    }
                    // Run the original method to let other mods' prefixes and postfixes run
                    modContentPack.ReloadContentInt();
                    yield return null;
                }
                continue;
            }
            else if (
                toExecuteWhenFinished is Action action2
                && action2.Method == reloadContentIntMethod
            )
            {
                LoadingProgressMod.Error(
                    "ReloadContentInt was called with target being "
                        + action2.Target.GetType().FullName
                        + ":"
                        + action2.Target
                        + ", but we expected it to be "
                        + reloadContentIntMethod.DeclaringType.FullName
                        + ":"
                        + reloadContentIntMethod
                );
            }

            ModContentPack_ReloadContentInt_Patch.CurrentModContentPack = null;

            var label = ProfilerLabel(toExecuteWhenFinished);
            if (
                LoadingProgressWindow.CurrentStage
                is LoadingStage.ExecuteToExecuteWhenFinished
                    or LoadingStage.ExecuteToExecuteWhenFinished2
            )
            {
                if (
                    !label.Contains("ModContentPack", StringComparison.Ordinal)
                    || !label.Contains("ReloadContent", StringComparison.Ordinal)
                )
                {
                    LoadingProgressWindow.SetCurrentLoadingActivityRaw(label);
                }
                LoadingProgressWindow.StageProgress = (
                    i + 1,
                    LongEventHandler.toExecuteWhenFinished.Count
                );
            }
            yield return null;
            RunLabelledDeferredAction(toExecuteWhenFinished, label, label, timeDeferredActions);

            // DeepProfiler.End() above just restored the parent scope's label (e.g.
            // "ExecuteToExecuteWhenFinished()") via DeepProfiler_End_Patches, undoing the
            // SetCurrentLoadingActivityRaw(label) call from before we ran this action. Put it
            // back so the repaint below still shows the label for the action that actually
            // just ran.
            if (
                LoadingProgressWindow.CurrentStage
                is LoadingStage.ExecuteToExecuteWhenFinished
                    or LoadingStage.ExecuteToExecuteWhenFinished2
            )
            {
                LoadingProgressWindow.SetCurrentLoadingActivityRaw(label);
            }

            // See the comment on the matching yield in
            // StaticConstructorOnStartupUtilityReplacement.CallAll(): without this, a slow
            // action above blows through LongEventHandler's MoveNext() time budget from
            // inside a single call, and the label for the *next* action gets set before we
            // ever get a chance to repaint, misattributing the stall.
            yield return null;
        }
        if (LongEventHandler.toExecuteWhenFinished.Count > 0)
        {
            DeepProfiler.End();
        }
        LongEventHandler.toExecuteWhenFinished.Clear();
        LongEventHandler.executingToExecuteWhenFinished = false;
        FasterGameLoading_DelayedActions_LateUpdate_Patches._pauseFasterGameLoading_DelayedActions_LateUpdate =
            false;
    }

    internal const string DeferredActionCategory =
        "LoadingProgress.StartupImpact.ExecuteToExecuteWhenFinished";

    /// <summary>
    /// The engine's profiler label for a deferred action: the type and the method it runs.
    /// </summary>
    internal static string ProfilerLabel(Action action) =>
        $"{action.Method.DeclaringType} -> {action.Method}";

    /// <summary>
    /// Runs one deferred action inside its profiler label, as the engine's pass does, through
    /// <see cref="RunDeferredAction(Action, string, bool, string)"/>, under
    /// <paramref name="categoryLabel"/>.
    /// </summary>
    internal static void RunLabelledDeferredAction(
        Action action,
        string profilerLabel,
        string categoryLabel,
        bool timed,
        string categoryKey = DeferredActionCategory
    )
    {
        DeepProfiler.Start(profilerLabel);
        try
        {
            RunDeferredAction(action, categoryLabel, timed, categoryKey);
        }
        finally
        {
            DeepProfiler.End();
        }
    }

    /// <summary>
    /// Runs one deferred initialization action, timed under the mod it is credited to when
    /// <paramref name="timed"/>, in a category under <paramref name="categoryKey"/>. Its
    /// category is closed whether or not the action throws, and an exception is logged rather
    /// than passed on, so the rest of the queue still runs.
    /// </summary>
    /// <remarks>
    /// Finding the owner walks the action's closure by reflection, for each of the tens of
    /// thousands of actions a large mod list queues, so it is skipped when nothing is timed. A
    /// failure in the timing itself leaves the action untimed, never unrun.
    /// </remarks>
    internal static void RunDeferredAction(
        Action action,
        string label,
        bool timed,
        string categoryKey = DeferredActionCategory
    ) => RunDeferredAction(action, label, timed, OwnerOf, categoryKey);

    /// <summary>
    /// <see cref="RunDeferredAction(Action, string, bool, string)"/>, finding the action's owner
    /// with <paramref name="findOwner"/>.
    /// </summary>
    internal static void RunDeferredAction(
        Action action,
        string label,
        bool timed,
        Func<Delegate, (ModContentPack? Owner, bool IsBaseGame)> findOwner,
        string categoryKey = DeferredActionCategory
    )
    {
        string? category = null;
        ModContentPack? owner = null;
        var isBaseGame = false;
        if (timed)
        {
            try
            {
                (owner, isBaseGame) = findOwner(action);
                category = $"{categoryKey}|{label}";
                StartupImpactProfilerUtil.Start(owner, isBaseGame, category);
            }
            catch (Exception ex)
            {
                category = null;
                LoadingProgressMod.Warning(
                    $"Could not time the deferred action {label}, so it runs untimed: {ex.Message}"
                );
            }
        }

        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error("Could not execute post-long-event action. Exception: " + ex);
        }
        finally
        {
            if (category != null)
            {
                StopTiming(owner, isBaseGame, category);
            }
        }
    }

    private static (ModContentPack? Owner, bool IsBaseGame) OwnerOf(Delegate action) =>
        (StartupImpactProfilerUtil.OwnerOfDeferredAction(action, out var isBaseGame), isBaseGame);

    // Never throws into the queue: a failure to stop is logged, and the next action runs.
    private static void StopTiming(ModContentPack? owner, bool isBaseGame, string category)
    {
        try
        {
            StartupImpactProfilerUtil.Stop(owner, isBaseGame, category);
        }
        catch (Exception ex)
        {
            LoadingProgressMod.Warning($"Could not stop timing {category}: {ex.Message}");
        }
    }
}
