using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactSessionDataTests
{
    private const int MaxFramesToWaitForTheMenu = 1200;

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

    // The stage ledger starts at the initial stage and closes when loading finishes, so a
    // tracked startup has at least that one entry, and the first stage every startup passes
    // through on the way to the menu.
    [Test]
    public static void ACapturedSessionCarriesTheStagesItWentThrough()
    {
        if (!LoadingProgressMod.instance.StartupImpact.WasTrackingEnabledAtStartup)
        {
            Test.Skip("Startup impact tracking was off for this launch.");
            return;
        }

        var session = StartupImpactSessionData.FromCurrentSession();

        Expect.IsNotEmpty(session.StageTimings);
        Expect.Any(
            session.StageTimings,
            stage => stage.Stage == nameof(LoadingStage.LoadingModClasses)
        );
        Expect.All(session.StageTimings, stage => stage.RemainingMs >= 0f);
    }

    // A command-line test run starts before the menu's first idle frames, where the tracker
    // takes the time to the menu, so this waits for the tracker rather than taking it early.
    [Test]
    public static IEnumerator ACapturedSessionCarriesTheTimeToTheMainMenu()
    {
        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        if (!startupImpact.WasTrackingEnabledAtStartup)
        {
            Test.Skip("Startup impact tracking was off for this launch.");
            yield break;
        }

        var framesWaited = 0;
        while (startupImpact.TimeToMenu <= 0f && framesWaited++ < MaxFramesToWaitForTheMenu)
        {
            yield return null;
        }

        var session = StartupImpactSessionData.FromCurrentSession();

        Expect.GreaterThan(session.TimeToMenu, session.LoadingTime);
        Expect.AreApproximatelyEqual(startupImpact.TimeToMenu, session.TimeToMenu);
    }

    // The game's process starts well before any mod's constructor runs, so a tracked startup
    // that could read its process's start time has early launch time.
    [Test]
    public static void ACapturedSessionCarriesTheEarlyLaunchTime()
    {
        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        if (!startupImpact.WasTrackingEnabledAtStartup)
        {
            Test.Skip("Startup impact tracking was off for this launch.");
            return;
        }

        var session = StartupImpactSessionData.FromCurrentSession();

        Expect.GreaterThan(startupImpact.EarlyLaunchTime, 0f);
        Expect.AreApproximatelyEqual(startupImpact.EarlyLaunchTime, session.EarlyLaunchTime);
    }

    // A process started 12.34 s after boot, read at 20.5 s after boot, has run for 8.16 s,
    // whatever its command name holds.
    [Test]
    public static void TheProcessAgeIsReadFromProc()
    {
        const string stat =
            "4242 (Rim World (x)) S 1 4242 4242 0 -1 4194304 100 0 0 0 5 3 0 0 20 0 30 0 1234 "
            + "1000000 500 18446744073709551615 0 0 0 0 0 0 0 0 0 0 0 0 17 3 0 0 0 0 0";

        Expect.AreApproximatelyEqual(
            8160f,
            LoadingProgress.StartupImpact.StartupImpact.ProcessAgeFromProc(stat, "20.50 80.00\n")
                ?? -1f
        );
        Expect.IsFalse(
            LoadingProgress
                .StartupImpact.StartupImpact.ProcessAgeFromProc("4242 (RimWorld) S", "20.50")
                .HasValue
        );
        Expect.IsFalse(
            LoadingProgress.StartupImpact.StartupImpact.ProcessAgeFromProc(stat, "").HasValue
        );
    }

    [Test]
    public static void TheEarlyLaunchTimeRunsFromTheProcessStartAndIsNeverNegative()
    {
        var processStart = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        Expect.AreApproximatelyEqual(
            2500f,
            LoadingProgress.StartupImpact.StartupImpact.TimeSinceProcessStart(
                processStart,
                processStart.AddMilliseconds(2500)
            )
        );
        Expect.AreApproximatelyEqual(
            0f,
            LoadingProgress.StartupImpact.StartupImpact.TimeSinceProcessStart(
                processStart,
                processStart.AddSeconds(-1)
            )
        );
    }
}
