using ilyvion.LoadingProgress.StartupImpact.Dialog;
using RimTestRedux;

namespace ilyvion.LoadingProgress.Tests;

[HotSwappable]
[TestSuite]
internal static class StartupImpactSessionDataTests
{
    // The timestamp used to be taken when the session was captured, so each startup impact
    // window stamped its own. Two saves from one startup then landed in the history as two
    // runs, and the picker listed a run as having happened when its window was opened.
    [Test]
    public static void EverySessionCapturedFromThisStartupCarriesTheSameTimestamp()
    {
        var first = StartupImpactSessionData.FromCurrentSession();
        var second = StartupImpactSessionData.FromCurrentSession();

        Assert.That(first.SavedAtUtc.Ticks).Is.EqualTo(second.SavedAtUtc.Ticks);
    }

    [Test]
    public static void ACapturedSessionIsStampedAgainstTheRunningModList()
    {
        var session = StartupImpactSessionData.FromCurrentSession();

        Assert.That(session.ModListHash).Is.EqualTo(StartupImpactSessionData.CurrentModListHash());
    }
}
