using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactSessionIndexEntryTests
{
    // Regression. LoadingTime comes from ProfilerStopwatch, which returns milliseconds, but
    // the neighbouring Settings.LoadingTimes holds seconds. Reading this one as seconds showed
    // a real 10 second load as 2:48:27.
    [Test]
    public static void LoadingTimeIsReadAsMillisecondsRatherThanSeconds() =>
        Expect.AreEqual("00:10", StartupImpactSessionIndexEntry.FormatLoadingTime(10107.63f));

    [Test]
    public static void AnEmptyStageTranslatesToNothing() =>
        Expect.AreEqual("", StartupImpactSessionIndexEntry.TranslateStage(""));

    [Test]
    public static void AStageWithNoTextOfItsOwnComesBackAsItWasRecorded() =>
        Expect.AreEqual(
            "NoSuchStage",
            StartupImpactSessionIndexEntry.TranslateStage("NoSuchStage")
        );

    // The stage strings carry the mod being worked on as {0} and end in an ellipsis, and
    // neither belongs inside "Stopped at ...". Asserted without naming the English text, so
    // the suite does not depend on which language is active.
    [Test]
    public static void AStageLosesItsModPlaceholderAndItsEllipsis()
    {
        var text = StartupImpactSessionIndexEntry.TranslateStage("LoadModXml");

        Expect.AreEqual(-1, text.IndexOf("{0}", StringComparison.Ordinal));
        Expect.IsFalse(text.EndsWith('.'));
        Expect.AreNotEqual("LoadModXml", text);
    }

    // The second delayed-initialization pass has no string of its own. It used to be listed
    // by its raw member name, in every language.
    [Test]
    public static void TheSecondDelayedInitialisationPassBorrowsTheFirstOnesText()
    {
        var second = StartupImpactSessionIndexEntry.TranslateStage(
            nameof(LoadingStage.ExecuteToExecuteWhenFinished2)
        );

        Expect.AreEqual(
            StartupImpactSessionIndexEntry.TranslateStage(
                nameof(LoadingStage.ExecuteToExecuteWhenFinished)
            ),
            second
        );
        Expect.AreNotEqual(nameof(LoadingStage.ExecuteToExecuteWhenFinished2), second);
    }
}
