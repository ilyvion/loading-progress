using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactSessionDataTests
{
    // The timestamp used to be taken when the session was captured, so each startup impact
    // window stamped its own. Two saves from one startup then landed in the history as two
    // runs, and the picker listed a run as having happened when its window was opened.
    [Test]
    public static void EverySessionCapturedFromThisStartupCarriesTheSameTimestamp()
    {
        var first = StartupImpactSessionData.FromCurrentSession();
        var second = StartupImpactSessionData.FromCurrentSession();

        Expect.AreEqual(second.SavedAtUtc.Ticks, first.SavedAtUtc.Ticks);
    }

    [Test]
    public static void ACapturedSessionIsStampedAgainstTheRunningModList()
    {
        var session = StartupImpactSessionData.FromCurrentSession();

        Expect.AreEqual(StartupImpactSessionData.CurrentModListHash(), session.ModListHash);
    }
}
