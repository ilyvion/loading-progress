using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

/// <summary>
/// Startup impact tracking against the real Multiplayer, and Useful Marks, whose mod constructor
/// calls the parameterless <c>MP.RegisterAll()</c>. Run by the <c>Multiplayer</c> test plan.
/// </summary>
[TestFixture(TestType.MainMenu)]
[LoadIfModsActive("rwmt.Multiplayer", "Andromeda.UsefulMarks")]
internal sealed class MultiplayerIntegrationTests
{
    private static bool SkipUnlessTracked()
    {
        if (LoadingProgressMod.instance.StartupImpact.WasTrackingEnabledAtStartup)
        {
            return false;
        }
        Test.Skip(TestStartup.TrackingOff);
        return true;
    }

    [Test]
    public static void TheApiIsEnabled()
    {
        if (SkipUnlessTracked())
        {
            return;
        }

        var enabled = AccessTools.Field(AccessTools.TypeByName("Multiplayer.API.MP"), "enabled");

        Expect.IsTrue(enabled.GetValue(null) is true);
    }

    [Test]
    public static void TheSyncMethodsOfAModConstructorCallingRegisterAllAreRegistered()
    {
        if (SkipUnlessTracked())
        {
            return;
        }

        var handlers =
            AccessTools
                .Field(AccessTools.TypeByName("Multiplayer.Client.Sync"), "handlers")
                .GetValue(null) as IEnumerable;
        var syncMethodType = AccessTools.TypeByName("Multiplayer.Client.SyncMethod");
        var methodField = AccessTools.Field(syncMethodType, "method");

        var usefulMarks = AccessTools.TypeByName("UsefulMarks.UsefulMarksMod").Assembly;
        var registered = (handlers?.Cast<object>() ?? [])
            .Where(syncMethodType.IsInstanceOfType)
            .Select(handler => methodField.GetValue(handler) as MethodInfo)
            .Count(method => method?.DeclaringType?.Assembly == usefulMarks);

        Expect.GreaterThan(registered, 0);
    }
}
