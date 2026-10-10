using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class CategoryColorMapTests
{
    private const string Curated = "LoadingProgress.Tests.Curated";
    private static readonly Color CuratedColor = new(0.1f, 0.2f, 0.3f);

    private static readonly CategoryColorMap Map = new(
        new Dictionary<string, Color> { { Curated, CuratedColor } }
    );

    [Test]
    public static void ACuratedCategoryHasItsOwnColor() =>
        Expect.AreEqual(Map[Curated], CuratedColor);

    [Test]
    public static void ACategoryWithAParameterHasItsPhasesColor() =>
        Expect.AreEqual(Map[$"{Curated}|Some.Method"], CuratedColor);

    [Test]
    public static void ReplacedTimeHasTheColorOfWhatItReplaced() =>
        Expect.AreEqual(
            Map[StartupImpactProfilerUtil.ReplacedBy($"{Curated}|Some.Method", "Mod")],
            CuratedColor
        );

    [Test]
    public static void AnUncuratedCategoryHasAColorDerivedFromItsPhase()
    {
        Expect.IsTrue(Map.TryGetValue("LoadingProgress.Tests.Uncurated|A", out var color));
        Expect.AreEqual(
            color,
            StartupImpactProfilerUtil.HashColor("LoadingProgress.Tests.Uncurated")
        );
        Expect.AreEqual(Map["LoadingProgress.Tests.Uncurated|B"], color);
    }

    [Test]
    public static void ALinearBarIsAsWideAsItsShareOfTheSpan()
    {
        var bar = new ProfilerBar { ProgressBarPadding = 2f };
        Expect.AreApproximatelyEqual(bar.FillWidth(104f, 250f, 1000f), 25f);
    }

    [Test]
    public static void ALogScaleBarIsWiderThanItsLinearShare()
    {
        var bar = new ProfilerBar
        {
            ProgressBarPadding = 2f,
            UseLogScale = true,
            Tau = 1000f,
        };
        Expect.GreaterThan(bar.FillWidth(104f, 250f, 10000f), 2.5f);
    }
}
