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

    private static void Prefix() => PostLoadTracker.BeginEventWork();

    private static void Postfix() => PostLoadTracker.EndEventWork();
}
