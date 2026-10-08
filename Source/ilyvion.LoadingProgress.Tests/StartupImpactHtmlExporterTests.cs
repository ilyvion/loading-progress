using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Dialog;
using ilyvion.LoadingProgress.StartupImpact.Dialog.Export;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactHtmlExporterTests
{
    // Loading stops the clock at 90.6 s and the menu is usable at 110.6 s; two stages leave
    // time no step accounts for.
    private static readonly StartupImpactSessionData Session = StartupImpactSessionData.FromValues(
        90600f,
        110600f,
        new() { ["LoadingProgress.StartupImpact.AbstractFilesystemClearAllCache"] = 1000f },
        [],
        [new("LoadingDefs", 1500f, 500f), new("AtlasBaking", 800f, 600f)]
    );

    private static string Report(StartupImpactSessionViewData viewData) =>
        StartupImpactHtmlExporter.BuildReport(
            Session,
            viewData,
            new Dictionary<string, Color>(),
            Color.gray,
            showBaseGameOffThreadImpact: true,
            secondsOnly: false
        );

    // The report carries the remaining bar's segments, each with its colour and time, and the
    // session's time to the menu.
    [Test]
    public static void TheReportCarriesTheRemainingBar()
    {
        var viewData = new StartupImpactSessionViewData(Session);
        var html = Report(viewData);

        Expect.IsTrue(html.Contains("\"timeToMenuMs\":110600,", StringComparison.Ordinal));
        Expect.IsTrue(html.Contains("\"remainingTitle\":", StringComparison.Ordinal));
        Expect.IsTrue(html.Contains("id=\"remainingBar\"", StringComparison.Ordinal));
        Expect.AreEqual(3, viewData.RemainingByStage.Count);
        foreach (var entry in viewData.RemainingByStage)
        {
            var valueMs = entry.Ms.ToString("0.###", CultureInfo.InvariantCulture);
            Expect.IsTrue(
                html.Contains(
                    $"\"label\":\"{entry.Label}\",\"color\":\"#",
                    StringComparison.Ordinal
                )
            );
            Expect.IsTrue(html.Contains($"\"valueMs\":{valueMs}}}", StringComparison.Ordinal));
        }
    }
}
