using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class SessionRetentionTests
{
    private sealed class Entry(string name, bool pinned = false, bool baseline = false)
    {
        public string Name { get; } = name;
        public bool Pinned { get; } = pinned;
        public bool Baseline { get; } = baseline;

        public override string ToString() => Name;
    }

    private static List<string> Evict(
        int sessionsToKeep,
        bool keepPinnedBeyondLimit,
        params Entry[] newestFirst
    ) =>
        SessionRetention
            .SelectForEviction(
                newestFirst,
                sessionsToKeep,
                keepPinnedBeyondLimit,
                e => e.Pinned,
                e => e.Baseline
            )
            .ConvertAll(e => e.Name);

    [Test]
    public static void NothingIsEvictedWhileTheHistoryIsUnderTheLimit()
    {
        var evicted = Evict(10, true, new Entry("a"), new Entry("b"), new Entry("c"));
        Expect.IsEmpty(evicted);
    }

    [Test]
    public static void TheOldestOrdinarySessionIsEvictedFirst()
    {
        var evicted = Evict(2, true, new Entry("newest"), new Entry("middle"), new Entry("oldest"));
        Expect.AreEqual(1, evicted.Count);
        Expect.IsTrue(evicted.Contains("oldest"));
    }

    [Test]
    public static void PinnedSessionsSurviveTheLimitAndDoNotOccupyASlot()
    {
        // With the pin exempt, the two most recent ordinary sessions still fit, so only the
        // third ordinary one goes.
        var evicted = Evict(
            2,
            true,
            new Entry("pinned", pinned: true),
            new Entry("a"),
            new Entry("b"),
            new Entry("c")
        );
        Expect.AreEqual(1, evicted.Count);
        Expect.IsTrue(evicted.Contains("c"));
    }

    [Test]
    public static void PinnedSessionsAreEvictedNormallyWhenTheExemptionIsOff()
    {
        // Same history as above; with the exemption off the pin occupies a slot, so two
        // ordinary sessions fall outside the limit instead of one.
        var evicted = Evict(
            2,
            false,
            new Entry("pinned", pinned: true),
            new Entry("a"),
            new Entry("b"),
            new Entry("c")
        );
        Expect.AreEqual(2, evicted.Count);
        Expect.IsTrue(evicted.Contains("b"));
        Expect.IsTrue(evicted.Contains("c"));
    }

    [Test]
    public static void TheBaselineIsNeverEvictedAutomatically()
    {
        var evicted = Evict(
            2,
            true,
            new Entry("a"),
            new Entry("b"),
            new Entry("c"),
            new Entry("known good", baseline: true)
        );
        Expect.AreEqual(1, evicted.Count);
        Expect.IsTrue(evicted.Contains("c"));
    }

    [Test]
    public static void TheBaselineDoesNotOccupyASlotEvenWhenItIsTheOldestEntry()
    {
        // Three ordinary sessions and a limit of three: the baseline sitting below them must
        // not push the oldest ordinary one out.
        var evicted = Evict(
            3,
            true,
            new Entry("a"),
            new Entry("b"),
            new Entry("c"),
            new Entry("known good", baseline: true)
        );
        Expect.IsEmpty(evicted);
    }

    [Test]
    public static void ALimitBelowTheMinimumIsRaisedRatherThanObeyed()
    {
        // Guards against a settings value of 0 or 1 quietly erasing the history that makes a
        // comparison possible at all.
        var evicted = Evict(0, true, new Entry("a"), new Entry("b"), new Entry("c"));
        Expect.AreEqual(1, evicted.Count);
        Expect.IsTrue(evicted.Contains("c"));
    }

    [Test]
    public static void AnEmptyHistoryEvictsNothing()
    {
        var evicted = Evict(10, true);
        Expect.IsEmpty(evicted);
    }
}
