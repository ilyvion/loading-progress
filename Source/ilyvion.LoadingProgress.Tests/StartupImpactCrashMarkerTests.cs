using ilyvion.LoadingProgress.StartupImpact;
using RimTestRedux;

namespace ilyvion.LoadingProgress.Tests;

[HotSwappable]
[TestSuite]
internal static class StartupImpactCrashMarkerTests
{
    private const long Ticks = 639253208682638880L;

    [Test]
    public static void AWrittenMarkerReadsBackWithEveryFieldIntact()
    {
        var parsed = StartupImpactCrashMarker.Parse(
            StartupImpactCrashMarker.Format(Ticks, 1114224214, 229, "ErrorCheckAllDefs")
        );

        Assert.That(parsed.HasValue).Is.True();
        Assert.That(parsed!.Value.StartedAtUtc.Ticks).Is.EqualTo(Ticks);
        Assert.That(parsed.Value.ModListHash).Is.EqualTo(1114224214);
        Assert.That(parsed.Value.ModsLoaded).Is.EqualTo(229);
        Assert.That(parsed.Value.LastStage).Is.EqualTo("ErrorCheckAllDefs");
    }

    [Test]
    public static void AStageIsOptionalBecauseABootCanDieBeforeReachingOne()
    {
        var parsed = StartupImpactCrashMarker.Parse(
            StartupImpactCrashMarker.Format(Ticks, 0, 0, "")
        );

        Assert.That(parsed.HasValue).Is.True();
        Assert.That(parsed!.Value.LastStage).Is.EqualTo("");
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
            Assert.That(StartupImpactCrashMarker.Parse(complete[..cut]).HasValue).Is.False();
        }

        Assert.That(StartupImpactCrashMarker.Parse(complete).HasValue).Is.True();
    }

    [Test]
    public static void AMarkerWithoutAStartTimeIsNoMarker() =>
        Assert
            .That(StartupImpactCrashMarker.Parse("version=1\nstage=LoadModXml\n").HasValue)
            .Is.False();

    [Test]
    public static void GarbageIsNoMarker()
    {
        Assert.That(StartupImpactCrashMarker.Parse("").HasValue).Is.False();
        Assert.That(StartupImpactCrashMarker.Parse("\0\0\0").HasValue).Is.False();
        Assert
            .That(StartupImpactCrashMarker.Parse("startedAtUtcTicks=not-a-number\n").HasValue)
            .Is.False();
    }

    // DateTime would throw on either of these rather than return a bad value.
    [Test]
    public static void AnOutOfRangeStartTimeIsNoMarker()
    {
        Assert.That(StartupImpactCrashMarker.Parse("startedAtUtcTicks=0\n").HasValue).Is.False();
        Assert.That(StartupImpactCrashMarker.Parse("startedAtUtcTicks=-5\n").HasValue).Is.False();
        Assert
            .That(
                StartupImpactCrashMarker.Parse("startedAtUtcTicks=99999999999999999999\n").HasValue
            )
            .Is.False();
    }

    // Written with \n, but a marker that has been through anything Windows-shaped
    // comes back with \r\n.
    [Test]
    public static void CarriageReturnsDoNotLeakIntoTheStageName()
    {
        var parsed = StartupImpactCrashMarker.Parse(
            "startedAtUtcTicks=" + Ticks + "\r\nstage=LoadModXml\r\n"
        );

        Assert.That(parsed.HasValue).Is.True();
        Assert.That(parsed!.Value.LastStage).Is.EqualTo("LoadModXml");
    }
}
