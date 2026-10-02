using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class LongEventHandler_UpdateCurrentEnumeratorEventTests
{
    private static bool IsTranspilerApplied()
    {
        var patchInfo = Harmony.GetPatchInfo(
            AccessTools.Method(
                typeof(LongEventHandler),
                nameof(LongEventHandler.UpdateCurrentEnumeratorEvent)
            )
        );
        return patchInfo?.Transpilers.Any(p =>
                p.PatchMethod.DeclaringType
                == typeof(LongEventHandler_UpdateCurrentEnumeratorEvent_Patches)
            ) == true;
    }

    [Test]
    public static void ForceImmediateRepaintsIsOffByDefault() =>
        Expect.IsFalse(new Settings().ForceImmediateRepaints);

    [Test]
    public static void TranspilerIsAppliedOnlyWhenForceImmediateRepaintsIsOn() =>
        Expect.AreEqual(LoadingProgressMod.Settings.ForceImmediateRepaints, IsTranspilerApplied());
}
