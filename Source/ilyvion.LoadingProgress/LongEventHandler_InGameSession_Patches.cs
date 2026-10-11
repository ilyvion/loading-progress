using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress;

// LongEventsUpdate runs every frame regardless of whether an event is currently displayed,
// unlike DrawLongEventWindowContents (which only fires while currentEvent != null), so it's the
// only reliable place to observe "the queue went empty" and end a session, and likewise the
// place to see every long event that runs between the end of loading and the idle main menu.
[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.LongEventsUpdate))]
internal static class LongEventHandler_LongEventsUpdate_Patches
{
    // Before the frame's long events run: where a pause in the background ends.
    private static readonly Action[] BeforeSteps = [PostLoadTracker.BeginFrame];

    private static readonly Action[] AfterSteps =
    [
        PostLoadTracker.Update,
        () => LoadingProgressMod.instance?.StartupImpact?.UpdateEndOfStartup(),
        InGameLoadingSession.Update,
    ];

    // The keys Log.ErrorOnce takes for each step's failure, one per step.
    private const int BeforeStepsErrorKey = 0x4C50_0100;
    private const int AfterStepsErrorKey = 0x4C50_0200;

    private static void Prefix() => RunEach(BeforeSteps, BeforeStepsErrorKey);

    private static void Postfix() => RunEach(AfterSteps, AfterStepsErrorKey);

    /// <summary>
    /// Runs every one of <paramref name="steps"/>, whether or not one before it throws. A step
    /// that throws is logged the first time, under <paramref name="errorKey"/> plus its index,
    /// and never into the long events, which a throwing prefix would keep from running at all.
    /// </summary>
    internal static void RunEach(Action[] steps, int errorKey)
    {
        for (var i = 0; i < steps.Length; i++)
        {
            try
            {
                steps[i]();
            }
            catch (Exception e)
            {
                Log.ErrorOnce(
                    $"[Loading Progress] A long event update step failed: {e}",
                    errorKey + i
                );
            }
        }
    }
}

[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.ClearQueuedEvents))]
internal static class LongEventHandler_ClearQueuedEvents_Patches
{
    private static void Postfix() => InGameLoadingSession.SignalReset();
}

[HarmonyPatch(typeof(Scribe), nameof(Scribe.ForceStop))]
internal static class Scribe_ForceStop_Patches
{
    private static void Postfix() => InGameLoadingSession.SignalReset();
}
