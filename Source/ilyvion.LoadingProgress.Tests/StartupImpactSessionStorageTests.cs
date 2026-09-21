using ilyvion.LoadingProgress.StartupImpact.Dialog;
using RimTestRedux;

namespace ilyvion.LoadingProgress.Tests;

[HotSwappable]
[TestSuite]
internal static class StartupImpactSessionStorageTests
{
    // ForUnfinishedBoot is the one factory that builds an entry from plain values, so it
    // stands in for a stored session here. Nothing these tests assert depends on whether the
    // boot finished.
    private static StartupImpactSessionIndexEntry Entry(
        string id,
        bool pinned = false,
        bool baseline = false
    )
    {
        var entry = StartupImpactSessionIndexEntry.ForUnfinishedBoot(
            id,
            new DateTime(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc),
            0,
            0,
            ""
        );
        entry.Pinned = pinned;
        entry.Baseline = baseline;
        return entry;
    }

    private static StartupImpactSessionIndexEntry Find(
        List<StartupImpactSessionIndexEntry> entries,
        string id
    ) => entries.Find(entry => entry.Id == id);

    [Test]
    public static void SettingABaselineTakesItFromTheSessionThatHeldIt()
    {
        List<StartupImpactSessionIndexEntry> entries = [Entry("old", baseline: true), Entry("new")];

        StartupImpactSessionStorage.ApplyFlags(entries, "new", pinned: false, baseline: true);

        Assert.That(Find(entries, "new").Baseline).Is.True();
        Assert.That(Find(entries, "old").Baseline).Is.False();
    }

    // A session used to lose its pin when it became the baseline, so clearing the baseline
    // later dropped a run the player had protected back into the eviction pool.
    [Test]
    public static void SettingABaselineLeavesAPinInPlace()
    {
        List<StartupImpactSessionIndexEntry> entries = [Entry("kept", pinned: true)];

        StartupImpactSessionStorage.ApplyFlags(entries, "kept", pinned: true, baseline: true);

        Assert.That(Find(entries, "kept").Pinned).Is.True();
        Assert.That(Find(entries, "kept").Baseline).Is.True();
    }

    [Test]
    public static void PinningOneSessionLeavesEveryOtherSessionAlone()
    {
        List<StartupImpactSessionIndexEntry> entries =
        [
            Entry("target"),
            Entry("pinned", pinned: true),
            Entry("baseline", baseline: true),
        ];

        StartupImpactSessionStorage.ApplyFlags(entries, "target", pinned: true, baseline: false);

        Assert.That(Find(entries, "target").Pinned).Is.True();
        Assert.That(Find(entries, "pinned").Pinned).Is.True();
        Assert.That(Find(entries, "baseline").Baseline).Is.True();
    }

    // A session can hold both flags now that taking the baseline no longer clears a pin, and
    // the baseline has to win: the section it is listed under is the only thing on screen
    // saying which run everything else is being compared against.
    [Test]
    public static void ABaselineThatIsAlsoPinnedIsListedAsTheBaseline()
    {
        var entry = Entry("both", pinned: true, baseline: true);

        Assert
            .That(DialogStartupImpactHistory.GroupOf(entry))
            .Is.EqualTo(DialogStartupImpactHistory.SessionGroup.Baseline);
    }

    // The picker holds the list it loaded when it opened, so the session it names may have
    // been deleted from another window since.
    [Test]
    public static void FlagsForASessionThatIsNoLongerStoredChangeNothing()
    {
        List<StartupImpactSessionIndexEntry> entries =
        [
            Entry("a", pinned: true),
            Entry("b", baseline: true),
        ];

        StartupImpactSessionStorage.ApplyFlags(entries, "gone", pinned: true, baseline: false);

        Assert.That(Find(entries, "a").Pinned).Is.True();
        Assert.That(Find(entries, "b").Baseline).Is.True();
    }
}
