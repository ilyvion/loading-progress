using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class LoadingProgressWindowTests
{
    // A startup that ends without a loading time, at the limit on the wait after loading or by
    // going into a game, keeps its way into the startup impact window: the corner, the pause
    // menu and the settings say no time was recorded rather than showing nothing.
    [Test]
    public static void AStartupWithNoLoadingTimeStillShowsItsLink()
    {
        Expect.AreEqual(
            "LoadingProgress.LoadingTimeNotRecorded".Translate().ToString(),
            LoadingProgressWindow.LoadingTimeTextFor(null, notRecorded: true)
        );
        Expect.AreEqual(
            "LoadingProgress.LoadingTime"
                .Translate(Utilities.FormatDuration(TimeSpan.FromSeconds(90)))
                .ToString(),
            LoadingProgressWindow.LoadingTimeTextFor(TimeSpan.FromSeconds(90), notRecorded: false)
        );
        Expect.IsNull(LoadingProgressWindow.LoadingTimeTextFor(null, notRecorded: false));
    }

    [Test]
    public static void TheLoadingWindowStaysWhileLoadingRuns() =>
        Expect.AreEqual(
            OwnWindow.Loading,
            LoadingProgressWindow.OwnWindowFor(LoadingStage.LoadingDefs, false, true, false)
        );

    // Loading is over once the interface begins initializing, but the long events other mods
    // queue for after it, and any stall on the menu's first frame, are still the startup.
    [Test]
    public static void TheLoadingWindowStaysThroughTheStartupsTail() =>
        Expect.AreEqual(
            OwnWindow.Loading,
            LoadingProgressWindow.OwnWindowFor(LoadingStage.Finished, false, true, false)
        );

    [Test]
    public static void NoWindowOnceTheStartupHasReachedTheMenu() =>
        Expect.AreEqual(
            OwnWindow.None,
            LoadingProgressWindow.OwnWindowFor(LoadingStage.Finished, true, true, false)
        );

    [Test]
    public static void TheInGameWindowTakesOverForALoadOrAGeneration() =>
        Expect.AreEqual(
            OwnWindow.InGame,
            LoadingProgressWindow.OwnWindowFor(LoadingStage.Finished, true, false, true)
        );

    // A load or a generation started before the menu settled, as a quicktest's is, belongs to
    // the in-game window even though the startup has not completed.
    [Test]
    public static void TheInGameWindowTakesOverBeforeTheStartupCompletes()
    {
        Expect.AreEqual(
            OwnWindow.InGame,
            LoadingProgressWindow.OwnWindowFor(LoadingStage.Finished, false, true, true)
        );
        Expect.AreEqual(
            OwnWindow.InGame,
            LoadingProgressWindow.OwnWindowFor(LoadingStage.Finished, false, false, true)
        );
    }

    // While loading runs, the loading window is the one shown, whatever else is going on.
    [Test]
    public static void LoadingKeepsTheLoadingWindow() =>
        Expect.AreEqual(
            OwnWindow.Loading,
            LoadingProgressWindow.OwnWindowFor(LoadingStage.LoadingDefs, false, true, true)
        );

    // A quicktest leaves the menu behind before any idle frame; the tail ends there, and the
    // in-game window takes the generation that follows.
    [Test]
    public static void NoLoadingWindowOutsideTheMenuWithoutASession() =>
        Expect.AreEqual(
            OwnWindow.None,
            LoadingProgressWindow.OwnWindowFor(LoadingStage.Finished, false, false, false)
        );

    [Test]
    public static void AnEventsTextIsItsActivity() =>
        Expect.AreEqual("Doing a thing", LoadingProgressWindow.ActivityFor("Doing a thing"));

    // Vanilla's status box shows an event with no text as a bare "...".
    [Test]
    public static void AnEventWithNoTextGetsAWordForIt()
    {
        var expected = "LoadingProgress.FinishingUp".Translate().ToString();

        Expect.AreEqual(expected, LoadingProgressWindow.ActivityFor(""));
        Expect.AreEqual(expected, LoadingProgressWindow.ActivityFor(null));
    }

    // Regression. Clearing the translations, as the window does when the startup completes,
    // used to leave the next word asked for to be looked up in a table that was gone.
    [Test]
    public static void AWordAskedForAfterTheTranslationsAreClearedIsReadAgain()
    {
        Translations.Clear();

        Expect.AreEqual(
            "LoadingProgress.FinishingUp".Translate().ToString(),
            LoadingProgressWindow.ActivityFor("")
        );
    }

    private static Settings History(params float[] samples)
    {
        var settings = new Settings { LoadingTimesMeasuredToMenu = true, LastLoadingModHash = 7 };
        settings.LoadingTimes.AddRange(samples);
        return settings;
    }

    // Samples from before the window stayed up to the menu measured less; the first sample
    // measured to the menu replaces them.
    [Test]
    public static void TheFirstSampleToTheMenuClearsTheShorterOnes()
    {
        var settings = History(10f, 11f);
        settings.LoadingTimesMeasuredToMenu = false;

        LoadingProgressWindow.AddLoadingTimeSample(settings, 12f, 7);

        Expect.AreEqual("12", string.Join(",", settings.LoadingTimes));
        Expect.IsTrue(settings.LoadingTimesMeasuredToMenu);
    }

    [Test]
    public static void LaterSamplesAddToTheHistory()
    {
        var settings = History(10f, 11f);

        LoadingProgressWindow.AddLoadingTimeSample(settings, 12f, 7);

        Expect.AreEqual("10,11,12", string.Join(",", settings.LoadingTimes));
    }

    [Test]
    public static void AChangedModListStartsTheHistoryAgain()
    {
        var settings = History(10f, 11f);

        LoadingProgressWindow.AddLoadingTimeSample(settings, 12f, 8);

        Expect.AreEqual("12", string.Join(",", settings.LoadingTimes));
        Expect.AreEqual(8, settings.LastLoadingModHash);
    }

    [Test]
    public static void TheHistoryKeepsOnlyAsManySamplesAsItsCapacity()
    {
        var settings = History(10f, 11f);
        settings.LoadingTimesCapacity = 2;

        LoadingProgressWindow.AddLoadingTimeSample(settings, 12f, 7);

        Expect.AreEqual("11,12", string.Join(",", settings.LoadingTimes));
    }
}
