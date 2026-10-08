using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactSessionTotalsTests
{
    private const string ClearCache =
        "LoadingProgress.StartupImpact.AbstractFilesystemClearAllCache";
    private const string PostLoadEvent =
        "LoadingProgress.StartupImpact.PostLoadLongEvent|Initializing interface";

    // Loading stops the clock at 90.6 s and the menu is usable at 110.6 s. The base game has
    // 14.1 s of loading and a 5.9 s long event after it; one mod has 50 s.
    private static StartupImpactSessionData Session(float timeToMenu, float loadingTime = 90600f) =>
        StartupImpactSessionData.FromValues(
            loadingTime,
            timeToMenu,
            new() { [ClearCache] = 14100f, [PostLoadEvent] = 5900f },
            [
                StartupImpactSessionModData.FromValues(
                    "A",
                    "test.a",
                    new() { ["LoadingProgress.StartupImpact.ModConstructor"] = 50000f },
                    []
                ),
            ],
            [new("LoadingDefs", 1500f, 500f)]
        );

    [Test]
    public static void TheTopBarSpansTheTimeToTheMenu()
    {
        var session = Session(110600f);
        var viewData = new StartupImpactSessionViewData(session);

        Expect.AreApproximatelyEqual(110600f, viewData.TotalWindow);
        // What no category timed, measured to the menu: the loading stage's second and the
        // 14.1 s after loading.
        Expect.AreApproximatelyEqual(1000f + 14100f, viewData.MetricsTotal[3]);
        // The stored loading time is the clock stop, which external tools read; it is left
        // as it was.
        Expect.AreApproximatelyEqual(90600f, session.LoadingTime);
    }

    // A startup that went into a game, or whose menu never settled, has no time to the menu.
    // Everything timed after loading ran after the clock stopped, so it took at least the two
    // together. The 5.9 s event used to come off the remaining time instead, which then fell
    // short of the loading time's own untimed part.
    [Test]
    public static void WithoutATimeToTheMenuTheTopBarSpansTheLoadingTimeAndWhatCameAfter()
    {
        var viewData = new StartupImpactSessionViewData(Session(0f));

        Expect.AreApproximatelyEqual(90600f + 5900f, viewData.TotalWindow);
        // The loading stage's untimed second.
        Expect.AreApproximatelyEqual(1000f, viewData.MetricsTotal[3]);
    }

    // The history used to list a session by its loading time or its time to the menu, while
    // the window's title also took in steps past both.
    [Test]
    public static void TheHistoryListsASessionByTheTimeTheWindowShows()
    {
        foreach (var session in new[] { Session(110600f), Session(0f), Session(0f, 60000f) })
        {
            Expect.AreApproximatelyEqual(
                new StartupImpactSessionViewData(session).TotalWindow,
                StartupImpactSessionIndexEntry.ListedTime(session)
            );
        }
    }

    // A quicktest records no time to the menu, yet its long events after loading are timed.
    // When they took the steps past the loading time, the view data used to rewrite the stored
    // loading time to fit them, and saving the session wrote the inflated figure.
    [Test]
    public static void StepsPastTheLoadingTimeWidenTheBarAndLeaveTheLoadingTime()
    {
        var session = Session(0f, loadingTime: 60000f);
        var viewData = new StartupImpactSessionViewData(session);

        Expect.AreApproximatelyEqual(70000f, viewData.TotalWindow);
        // The loading stage's untimed second, however much of the window the steps took.
        Expect.AreApproximatelyEqual(1000f, viewData.MetricsTotal[3]);
        Expect.AreApproximatelyEqual(60000f, session.LoadingTime);
    }

    // A mod's category that ran inside one of the base game's counts in both their totals,
    // while the stage ledger credits the shared second once. The remaining heading used to
    // take the window less both totals, a second short of the entries listed under it.
    [Test]
    public static void TheRemainingTimeCountsTimeTwoTotalsShareOnce()
    {
        var viewData = new StartupImpactSessionViewData(
            StartupImpactSessionData.FromValues(
                10000f,
                0f,
                new() { [ClearCache] = 6000f },
                [
                    StartupImpactSessionModData.FromValues(
                        "A",
                        "test.a",
                        new() { ["LoadingProgress.StartupImpact.ModConstructor"] = 3000f },
                        []
                    ),
                ],
                [new("LoadingDefs", 10000f, 8000f)]
            )
        );

        Expect.AreApproximatelyEqual(2000f, viewData.RemainingLoadingTime);
        Expect.AreApproximatelyEqual(
            viewData.RemainingByStage.Sum(entry => entry.Ms),
            viewData.RemainingLoadingTime
        );
        Expect.AreApproximatelyEqual(2000f, viewData.MetricsTotal[3]);
        // The top bar's segments come to the window and the shared second.
        Expect.AreApproximatelyEqual(viewData.TotalWindow + 1000f, viewData.MetricsTotal.Sum());
    }

    // The 5.9 s event after loading has an owner; only the rest of the 20 s is remaining.
    [Test]
    public static void TheTimeAfterLoadingLeavesOutTheTimedEvents()
    {
        var viewData = new StartupImpactSessionViewData(Session(110600f));

        var afterLoading = viewData.RemainingByStage.Single(entry =>
            entry.Key == StartupImpactSessionViewData.AfterLoadingKey
        );
        Expect.AreApproximatelyEqual(14100f, afterLoading.Ms);
    }

    [Test]
    public static void ASessionSavedAndReadBackKeepsItsStagesAndTimeToTheMenu()
    {
        var loaded = SaveAndLoad(Session(110600f));

        Expect.IsNotNull(loaded);
        Expect.AreApproximatelyEqual(110600f, loaded!.TimeToMenu);
        Expect.AreEqual(1, loaded.StageTimings.Count);
        Expect.AreEqual("LoadingDefs", loaded.StageTimings[0].Stage);
        Expect.AreApproximatelyEqual(1500f, loaded.StageTimings[0].WallMs);
        Expect.AreApproximatelyEqual(500f, loaded.StageTimings[0].AttributedMs);
    }

    // A session file written by 0.17.0 has neither node. It reads as a session with no stages
    // and no time to the menu, not as one that throws.
    [Test]
    public static void AFileFromBeforeStagesWereKeptReadsAsNoStages()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lp-session-{Guid.NewGuid():N}.xml");
        try
        {
            File.WriteAllText(
                path,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n"
                    + "<StartupImpactSession><sessionData>"
                    + "<loadingTime>90600</loadingTime>"
                    + $"<metrics><keys><li>{ClearCache}</li></keys><values><li>14100</li></values></metrics>"
                    + "<totalImpact>14100</totalImpact>"
                    + "<offThreadMetrics><keys /><values /></offThreadMetrics>"
                    + "<offThreadTotalImpact>0</offThreadTotalImpact>"
                    + "<mods />"
                    + "</sessionData></StartupImpactSession>"
            );

            var loaded = Load(path);

            Expect.IsNotNull(loaded);
            Expect.IsNotNull(loaded!.StageTimings);
            Expect.IsEmpty(loaded.StageTimings);
            Expect.AreApproximatelyEqual(0f, loaded.TimeToMenu);
            Expect.IsEmpty(new StartupImpactSessionViewData(loaded).RemainingByStage);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static StartupImpactSessionData? SaveAndLoad(StartupImpactSessionData session)
    {
        var path = Path.Combine(Path.GetTempPath(), $"lp-session-{Guid.NewGuid():N}.xml");
        try
        {
            Scribe.saver.InitSaving(path, "StartupImpactSession");
            Scribe_Deep.Look(ref session, "sessionData");
            Scribe.saver.FinalizeSaving();
            return Load(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static StartupImpactSessionData? Load(string path)
    {
        StartupImpactSessionData? loaded = null;
        Scribe.loader.InitLoading(path);
        Scribe_Deep.Look(ref loaded, "sessionData");
        Scribe.loader.FinalizeLoading();
        return loaded;
    }
}
