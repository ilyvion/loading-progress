using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

/// <summary>
/// Makes recording a category's time on the active thread throw while a test's action runs.
/// A recording's last step credits the stage ledger, and a prefix there throws.
/// </summary>
/// <remarks>
/// Nothing in a recording throws on its own, so this is how a test reaches the code that
/// handles a start whose recording fails. A start records the category open below the one it
/// starts, so the action needs one open on the same timer.
/// </remarks>
internal static class RecordingFailure
{
    private const string HarmonyId = "ilyvion.LoadingProgress.Tests.RecordingFailure";

    internal static void During(Action action)
    {
        var harmony = new Harmony(HarmonyId);
        var attribute = AccessTools.Method(typeof(StageLedger), nameof(StageLedger.Attribute));
        _ = harmony.Patch(
            attribute,
            prefix: new HarmonyMethod(typeof(RecordingFailure), nameof(Throw))
        );
        try
        {
            action();
        }
        finally
        {
            harmony.Unpatch(attribute, HarmonyPatchType.Prefix, HarmonyId);
        }
    }

    private static void Throw() =>
        throw new InvalidOperationException("A recording that fails, for the test.");
}
