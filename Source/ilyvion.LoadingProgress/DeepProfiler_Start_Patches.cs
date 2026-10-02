using System.Diagnostics;

namespace ilyvion.LoadingProgress;

// DeepProfiler.Start/End form a proper push/pop stack in the base game (see
// ThreadLocalDeepProfiler), but we only ever hooked Start. That meant the label of a short
// nested scope (e.g. "Reload strings") stuck around as "current activity" for however long
// the *parent* scope kept working afterward, since nothing ever restored it on End(). We
// mirror the base game's stack here so End() can put the parent's label back.
//
// This has to be thread-local, just like the base game's own ThreadLocalDeepProfiler:
// DeepProfiler.Start/End get called concurrently from worker threads during things like
// DirectXmlCrossRefLoader's GenThreading.ParallelForEach, and a single shared Stack<T> isn't
// thread-safe, so concurrent push/pop from multiple threads corrupted it (Peek() throwing
// "Stack empty" despite a Count check right before it).
internal static class DeepProfilerLabelStack
{
    [field: ThreadStatic]
    internal static Stack<string?> Labels => field ??= new Stack<string?>();
}

[HarmonyPatch(typeof(DeepProfiler), nameof(DeepProfiler.Start))]
internal static class DeepProfiler_Start_Patches
{
    private static void Prefix(string label)
    {
        if (label == null)
        {
            var method = new StackTrace().GetFrame(2)?.GetMethod();
            var declaringType = method?.DeclaringType;
            var mod =
                declaringType != null ? Utilities.FindModByAssembly(declaringType.Assembly) : null;
            var callerDescription =
                declaringType != null && method != null
                    ? $"{declaringType.FullName}.{method.Name}"
                    : "[unknown caller]";
            LoadingProgressMod.Warning(
                $"Why is {callerDescription} from {mod?.Name ?? "{unknown}"} calling DeepProfiler.Start (and by extension our patch) with null?! Stop it."
            );
        }
        else
        {
            // Once startup is finished, CurrentLoadingActivity's StageRules never match anything
            // again (the list is pruned down to just the Finished-stage rule by then), so calling
            // its setter for every in-game label would still take the dev-mode unmatched-activity
            // lock for nothing on every DeepProfiler.Start() in the game, session or not.
            if (LoadingProgressWindow.CurrentStage != LoadingStage.Finished)
            {
                LoadingProgressWindow.CurrentLoadingActivity = label;
            }
            if (
                label == LoadingProgressWindow.LoadingFinishedLabel
                && LoadingProgressMod.instance.StartupImpact.WasTrackingEnabledAtStartup
            )
            {
                LoadingProgressMod.instance.StartupImpact.FinishLoading();
            }
            InGameLoadingSession.OnProfilerLabel(label);
        }
        DeepProfilerLabelStack.Labels.Push(label);
    }
}

[HarmonyPatch(typeof(DeepProfiler), nameof(DeepProfiler.End))]
internal static class DeepProfiler_End_Patches
{
    private static void Prefix()
    {
        if (DeepProfilerLabelStack.Labels.Count == 0)
        {
            // Unbalanced End() call (started before we were around to see the matching
            // Start(), or vice versa); nothing sensible to restore.
            return;
        }
        _ = DeepProfilerLabelStack.Labels.Pop();
        if (DeepProfilerLabelStack.Labels.Count > 0)
        {
            var parentLabel = DeepProfilerLabelStack.Labels.Peek();
            if (parentLabel != null)
            {
                // Raw setter only: this is just restoring what's displayed, not a new
                // Start() event. Going through the CurrentLoadingActivity property setter
                // here would re-trigger stage/progress side effects (e.g. incrementing the
                // "X Worker"/"ParseValueAndReturnDef" progress counters a second time for
                // the same item) since some parent labels also match those patterns.
                LoadingProgressWindow.SetCurrentLoadingActivityRaw(parentLabel);
            }
            InGameLoadingSession.OnProfilerLabelRestored(parentLabel);
        }
    }
}
