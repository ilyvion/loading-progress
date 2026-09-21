using ilyvion.LoadingProgress.StartupImpact.Dialog;
using RimTestRedux;

namespace ilyvion.LoadingProgress.Tests;

[HotSwappable]
[TestSuite]
internal static class StartupImpactSessionIndexEntryTests
{
    // Regression. LoadingTime comes from ProfilerStopwatch, which returns milliseconds, but
    // the neighbouring Settings.LoadingTimes holds seconds. Reading this one as seconds showed
    // a real 10 second load as 2:48:27.
    [Test]
    public static void LoadingTimeIsReadAsMillisecondsRatherThanSeconds() =>
        Assert
            .That(StartupImpactSessionIndexEntry.FormatLoadingTime(10107.63f))
            .Is.EqualTo("00:10");

    [Test]
    public static void AnEmptyStageTranslatesToNothing() =>
        Assert.That(StartupImpactSessionIndexEntry.TranslateStage("")).Is.EqualTo("");

    [Test]
    public static void AStageWithNoTextOfItsOwnComesBackAsItWasRecorded() =>
        Assert
            .That(StartupImpactSessionIndexEntry.TranslateStage("NoSuchStage"))
            .Is.EqualTo("NoSuchStage");

    // The stage strings carry the mod being worked on as {0} and end in an ellipsis, and
    // neither belongs inside "Stopped at ...". Asserted without naming the English text, so
    // the suite does not depend on which language is active.
    [Test]
    public static void AStageLosesItsModPlaceholderAndItsEllipsis()
    {
        var text = StartupImpactSessionIndexEntry.TranslateStage("LoadModXml");

        Assert.That(text.IndexOf("{0}", StringComparison.Ordinal)).Is.EqualTo(-1);
        Assert.That(text.EndsWith('.')).Is.False();
        Assert.That(text).Is.Not.EqualTo("LoadModXml");
    }

    // The second delayed-initialization pass has no string of its own. It used to be listed
    // by its raw member name, in every language.
    [Test]
    public static void TheSecondDelayedInitialisationPassBorrowsTheFirstOnesText()
    {
        var second = StartupImpactSessionIndexEntry.TranslateStage(
            nameof(LoadingStage.ExecuteToExecuteWhenFinished2)
        );

        Assert
            .That(second)
            .Is.EqualTo(
                StartupImpactSessionIndexEntry.TranslateStage(
                    nameof(LoadingStage.ExecuteToExecuteWhenFinished)
                )
            );
        Assert.That(second).Is.Not.EqualTo(nameof(LoadingStage.ExecuteToExecuteWhenFinished2));
    }
}
