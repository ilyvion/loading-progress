using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactCrashMarkerTests
{
    private const long Ticks = 639253208682638880L;

    [Test]
    public static void AWrittenMarkerReadsBackWithEveryFieldIntact()
    {
        var parsed = StartupImpactCrashMarker.Parse(
            StartupImpactCrashMarker.Format(Ticks, 1114224214, 229, "ErrorCheckAllDefs")
        );

        Expect.IsTrue(parsed.HasValue);
        Expect.AreEqual(Ticks, parsed!.Value.StartedAtUtc.Ticks);
        Expect.AreEqual(1114224214, parsed.Value.ModListHash);
        Expect.AreEqual(229, parsed.Value.ModsLoaded);
        Expect.AreEqual("ErrorCheckAllDefs", parsed.Value.LastStage);
    }

    [Test]
    public static void AStageIsOptionalBecauseABootCanDieBeforeReachingOne()
    {
        var parsed = StartupImpactCrashMarker.Parse(
            StartupImpactCrashMarker.Format(Ticks, 0, 0, "")
        );

        Expect.IsTrue(parsed.HasValue);
        Expect.AreEqual("", parsed!.Value.LastStage);
    }

    // The marker is rewritten on every stage change and whatever stopped that
    // boot can cut the file off mid-write, so a partial one has to read as no
    // marker rather than as a boot starting at the epoch. Every cut point is
    // checked because the dangerous ones are in the middle: a cut inside the
    // tick digits leaves a marker whose fields all parse, and it used to come
    // back as a boot that started in the year 1.
    [Test]
    public static void NoTruncationOfAMarkerIsAMarker()
    {
        var complete = StartupImpactCrashMarker.Format(Ticks, 7, 9, "LoadModXml");

        for (var cut = 0; cut < complete.Length; cut++)
        {
            Expect.IsFalse(StartupImpactCrashMarker.Parse(complete[..cut]).HasValue);
        }

        Expect.IsTrue(StartupImpactCrashMarker.Parse(complete).HasValue);
    }

    [Test]
    public static void ARecordedStageAfterClearDoesNotRecreateTheMarker()
    {
        StartupImpactCrashMarker.Clear();

        StartupImpactCrashMarker.RecordStage("AfterFinish");

        Expect.IsFalse(File.Exists(StartupImpactCrashMarker.MarkerFilePath));
    }

    // The marker now stays through the wait after loading, so the end of loading is recorded
    // too, but only once the play data has loaded: the interface also initializes after a load
    // that failed, and that boot keeps the stage it failed at.
    [Test]
    public static void TheEndOfLoadingIsRecordedOnlyAfterALoadThatCompleted()
    {
        Expect.IsTrue(
            StartupImpactCrashMarker.Records(LoadingStage.Finished, playDataLoaded: true)
        );
        Expect.IsFalse(
            StartupImpactCrashMarker.Records(LoadingStage.Finished, playDataLoaded: false)
        );
        Expect.IsTrue(
            StartupImpactCrashMarker.Records(LoadingStage.LoadModXml, playDataLoaded: false)
        );
    }

    [Test]
    public static void AMarkerWithoutAStartTimeIsNoMarker() =>
        Expect.IsFalse(StartupImpactCrashMarker.Parse("version=1\nstage=LoadModXml\n").HasValue);

    [Test]
    public static void GarbageIsNoMarker()
    {
        Expect.IsFalse(StartupImpactCrashMarker.Parse("").HasValue);
        Expect.IsFalse(StartupImpactCrashMarker.Parse("\0\0\0").HasValue);
        Expect.IsFalse(StartupImpactCrashMarker.Parse("startedAtUtcTicks=not-a-number\n").HasValue);
    }

    // DateTime would throw on either of these rather than return a bad value.
    [Test]
    public static void AnOutOfRangeStartTimeIsNoMarker()
    {
        Expect.IsFalse(StartupImpactCrashMarker.Parse("startedAtUtcTicks=0\n").HasValue);
        Expect.IsFalse(StartupImpactCrashMarker.Parse("startedAtUtcTicks=-5\n").HasValue);
        Expect.IsFalse(
            StartupImpactCrashMarker.Parse("startedAtUtcTicks=99999999999999999999\n").HasValue
        );
    }

    // Written with \n, but a marker that has been through anything Windows-shaped
    // comes back with \r\n.
    [Test]
    public static void CarriageReturnsDoNotLeakIntoTheStageName()
    {
        var parsed = StartupImpactCrashMarker.Parse(
            "startedAtUtcTicks=" + Ticks + "\r\nstage=LoadModXml\r\n"
        );

        Expect.IsTrue(parsed.HasValue);
        Expect.AreEqual("LoadModXml", parsed!.Value.LastStage);
    }
}
