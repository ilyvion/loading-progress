using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactSessionViewDataTests
{
    // Wall time and what categories accounted for in it; the difference is what remains.
    private static readonly StartupImpactStageData[] Stages =
    [
        new("LoadingDefs", 1500f, 500f),
        new("StaticConstructorOnStartupCallAll", 27900f, 20000f),
        new("AtlasBaking", 8700f, 8000f),
        new("ResolveReferences", 2900f, 2900f),
    ];

    private static string AfterLoading => StartupImpactSessionViewData.AfterLoadingKey;

    [Test]
    public static void StagesComeLargestFirstWithTheirOwnRemainingTime()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries(Stages, 0f, 0f, 0f);

        Expect.IsTrue(entries.Count == 3);
        Expect.IsTrue(entries[0].Key == "StaticConstructorOnStartupCallAll");
        Expect.IsTrue(entries[0].Ms == 7900f);
        Expect.IsTrue(entries[1].Key == "LoadingDefs");
        Expect.IsTrue(entries[1].Ms == 1000f);
        Expect.IsTrue(entries[2].Key == "AtlasBaking");
        Expect.IsTrue(entries[2].Ms == 700f);
    }

    // A stage whose categories account for all of it (ResolveReferences above) has no entry.
    [Test]
    public static void AStageWithNothingRemainingHasNoEntry()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries(Stages, 0f, 0f, 0f);

        Expect.IsFalse(entries.Any(entry => entry.Key == "ResolveReferences"));
    }

    [Test]
    public static void WhatCameAfterLoadingIsAnEntryOfItsOwn()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries(Stages, 90600f, 110600f, 5900f);

        var afterLoading = entries.Single(entry => entry.Key == AfterLoading);
        Expect.IsTrue(afterLoading.Ms == 14100f);
        Expect.AreEqual(AfterLoading.Translate().ToString(), afterLoading.Label);
        // Larger than any stage's share, so it leads.
        Expect.IsTrue(entries[0].Key == AfterLoading);
    }

    [Test]
    public static void NothingComesAfterLoadingWhenTheMenuWasNeverReached()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries(Stages, 90600f, 0f, 0f);

        Expect.IsFalse(entries.Any(entry => entry.Key == AfterLoading));
    }

    // Twenty seconds after loading, all of it in long events timed under their owners:
    // nothing remains.
    [Test]
    public static void TheLongEventsTimedAfterLoadingLeaveNothingRemaining()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries([], 90600f, 110600f, 20000f);

        Expect.IsEmpty(entries);
    }

    // The second delayed-initialization pass borrows the first one's text everywhere else; in
    // the remaining time both are listed, and two lines with one name read as a mistake.
    [Test]
    public static void TheSecondDelayedInitializationPassIsToldApart()
    {
        var entries = StartupImpactSessionViewData.RemainingEntries(
            [
                new(nameof(LoadingStage.ExecuteToExecuteWhenFinished), 1000f, 0f),
                new(nameof(LoadingStage.ExecuteToExecuteWhenFinished2), 500f, 0f),
            ],
            0f,
            0f,
            0f
        );

        Expect.AreEqual(2, entries.Count);
        Expect.AreNotEqual(entries[0].Label, entries[1].Label);
        Expect.IsTrue(entries[1].Label.Contains(entries[0].Label, StringComparison.Ordinal));
    }

    // A session saved before stages were kept has no entries to add up, so its remaining time
    // is what the window leaves once the timed steps have had theirs, and never less than none.
    [Test]
    public static void ASessionWithoutStagesTakesWhatTheWindowLeaves()
    {
        Expect.AreApproximatelyEqual(
            3000f,
            StartupImpactSessionViewData.RemainingTotal(false, [], 10000f, 7000f)
        );
        Expect.AreApproximatelyEqual(
            0f,
            StartupImpactSessionViewData.RemainingTotal(false, [], 7000f, 9000f)
        );
        Expect.AreApproximatelyEqual(
            1500f,
            StartupImpactSessionViewData.RemainingTotal(true, [1000f, 500f], 10000f, 7000f)
        );
    }

    // The top bar's segments can come to more than the startup time, by time two totals share.
    // A linear bar is drawn to its segments' total then, instead of past its end.
    [Test]
    public static void ALinearBarWhoseSegmentsComeToMoreThanItsSpanIsDrawnToThem()
    {
        Expect.AreApproximatelyEqual(11000f, ProfilerBar.LinearSpan(10000f, 11000f));
        Expect.AreApproximatelyEqual(10000f, ProfilerBar.LinearSpan(10000f, 9000f));
    }
}
