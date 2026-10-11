using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress;

// Where each kind of long event runs its own work, once per frame, so the post-load tracker
// times that work and not the rest of the frame.
[HarmonyPatch]
internal static class LongEventHandler_UpdateCurrentEvent_Patches
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        [
            AccessTools.Method(
                typeof(LongEventHandler),
                nameof(LongEventHandler.UpdateCurrentSynchronousEvent)
            ),
            AccessTools.Method(
                typeof(LongEventHandler),
                nameof(LongEventHandler.UpdateCurrentAsynchronousEvent)
            ),
            AccessTools.Method(
                typeof(LongEventHandler),
                nameof(LongEventHandler.UpdateCurrentEnumeratorEvent)
            ),
        ];

    private static readonly Action[] BeforeSteps = [PostLoadTracker.BeginEventWork];
    private static readonly Action[] AfterSteps = [PostLoadTracker.EndEventWork];

    // The keys Log.ErrorOnce takes for each step's failure.
    private const int BeforeStepsErrorKey = 0x4C50_0300;
    private const int AfterStepsErrorKey = 0x4C50_0400;

    private static void Prefix() =>
        LongEventHandler_LongEventsUpdate_Patches.RunEach(BeforeSteps, BeforeStepsErrorKey);

    private static void Postfix() =>
        LongEventHandler_LongEventsUpdate_Patches.RunEach(AfterSteps, AfterStepsErrorKey);
}
