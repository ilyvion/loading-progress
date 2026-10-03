using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactPhaseViewDataTests
{
    private const string Textures =
        "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.Textures";
    private const string AudioClips =
        "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AudioClips";
    private const string DelayedTask = "LoadingProgress.StartupImpact.ExecuteToExecuteWhenFinished";

    private static StartupImpactSessionModData Mod(
        string name,
        Dictionary<string, float> metrics,
        Dictionary<string, float>? offThreadMetrics = null
    ) =>
        StartupImpactSessionModData.FromValues(
            name,
            $"test.{name}",
            metrics,
            offThreadMetrics ?? []
        );

    private static StartupImpactPhaseViewData Phase(
        List<StartupImpactPhaseViewData> phases,
        string key
    ) => phases.Single(phase => phase.Key == key);

    [Test]
    public static void EachPhaseSumsTheTimeOfEveryMod()
    {
        var phases = StartupImpactPhaseViewData.FromMods([
            Mod("A", new() { [Textures] = 100f, [AudioClips] = 5f }),
            Mod("B", new() { [Textures] = 50f }),
        ]);

        Expect.AreEqual(2, phases.Count);
        Expect.AreEqual(150f, Phase(phases, Textures).TotalImpact);
        Expect.AreEqual(5f, Phase(phases, AudioClips).TotalImpact);
    }

    [Test]
    public static void ModsWithinAPhaseAreOrderedByTheirShareLargestFirst()
    {
        var textures = Phase(
            StartupImpactPhaseViewData.FromMods([
                Mod("Small", new() { [Textures] = 10f }),
                Mod("Large", new() { [Textures] = 90f }),
                Mod("Medium", new() { [Textures] = 40f }),
            ]),
            Textures
        );

        Expect.AreEqual("Large,Medium,Small", string.Join(",", textures.ModNames));
        Expect.AreEqual("90,40,10", string.Join(",", textures.Metrics));
    }

    [Test]
    public static void CategoriesWithAParameterAreSummedIntoOnePhase()
    {
        var phases = StartupImpactPhaseViewData.FromMods([
            Mod("A", new() { [$"{DelayedTask}|First"] = 30f, [$"{DelayedTask}|Second"] = 20f }),
            Mod("B", new() { [$"{DelayedTask}|Third"] = 7f }),
        ]);

        Expect.AreEqual(1, phases.Count);
        var delayed = Phase(phases, DelayedTask);
        Expect.AreEqual(57f, delayed.TotalImpact);
        Expect.AreEqual("50,7", string.Join(",", delayed.Metrics));
    }

    [Test]
    public static void OffThreadTimeIsKeptApartFromOnThreadTime()
    {
        var textures = Phase(
            StartupImpactPhaseViewData.FromMods([
                Mod("A", new() { [Textures] = 10f }, new() { [Textures] = 25f }),
                Mod("B", [], new() { [Textures] = 5f }),
            ]),
            Textures
        );

        Expect.AreEqual(10f, textures.TotalImpact);
        Expect.AreEqual(30f, textures.OffThreadTotalImpact);
        Expect.AreEqual("A,B", string.Join(",", textures.ModNames));
        Expect.AreEqual("10,0", string.Join(",", textures.Metrics));
        Expect.AreEqual("25,5", string.Join(",", textures.OffThreadMetrics));
    }

    [Test]
    public static void PhasesNoModSpentTimeInAreLeftOut()
    {
        var phases = StartupImpactPhaseViewData.FromMods([
            Mod("A", new() { [Textures] = 0f, [AudioClips] = 3f }),
        ]);

        Expect.AreEqual(AudioClips, phases.Single().Key);
    }

    [Test]
    public static void APhaseIsLabelledByItsCategory() =>
        Expect.AreEqual(
            Textures.Translate().ToString(),
            Phase(
                StartupImpactPhaseViewData.FromMods([Mod("A", new() { [Textures] = 1f })]),
                Textures
            ).Label
        );

    [Test]
    public static void APhaseWithAParameterIsLabelledByItsGroupedKey() =>
        Expect.AreEqual(
            $"{DelayedTask}.Grouped".Translate().ToString(),
            Phase(
                StartupImpactPhaseViewData.FromMods([
                    Mod("A", new() { [$"{DelayedTask}|First"] = 1f }),
                ]),
                DelayedTask
            ).Label
        );
}
