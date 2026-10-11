using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactStartupPatchesTests
{
    [Test]
    public static void HookTimingIsTurnedOffBeforeThePatchesAreRemoved()
    {
        var steps = new List<string>();

        StartupImpact.StartupImpact.RemoveStartupPatches(
            () => steps.Add("deactivate"),
            () => steps.Add("unpatch")
        );

        Expect.AreEqual(steps.Count, 2);
        Expect.AreEqual(steps[0], "deactivate");
        Expect.AreEqual(steps[1], "unpatch");
    }

    [Test]
    [ErrorsAllowed("Could not remove the startup impact timing patches")]
    public static void AFailureToRemoveThePatchesIsLoggedNotThrown()
    {
        var deactivated = false;

        StartupImpact.StartupImpact.RemoveStartupPatches(
            () => deactivated = true,
            () => throw new InvalidOperationException("An unpatch that fails, for the test.")
        );

        Expect.IsTrue(deactivated);
    }
}
