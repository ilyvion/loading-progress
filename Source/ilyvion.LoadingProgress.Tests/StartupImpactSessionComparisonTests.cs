using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;
using ilyvion.LoadingProgress.StartupImpact.Dialog;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StartupImpactSessionComparisonTests
{
    private const string LoadDefs = "LoadingProgress.StartupImpact.LoadDefs";
    private const string ResolveReferences = "LoadingProgress.StartupImpact.ResolveReferences";
    private const string ModConstructor = "LoadingProgress.StartupImpact.ModConstructor";

    private static StartupImpactSessionModData Mod(string id, Dictionary<string, float> metrics) =>
        StartupImpactSessionModData.FromValues(id, id, metrics, []);

    private static StartupImpactSessionFigures Session(
        IEnumerable<StartupImpactSessionModData>? mods = null,
        IEnumerable<StartupImpactStageData>? stages = null,
        Dictionary<string, float>? metrics = null,
        float loadingTime = 10000f,
        float timeToMenu = 12000f,
        int modListHash = 1,
        bool? harmonyHooksTimed = false
    ) =>
        StartupImpactSessionFigures.From(
            StartupImpactSessionData.FromValues(
                loadingTime,
                timeToMenu,
                metrics ?? [],
                mods ?? [],
                stages ?? [],
                modListHash: modListHash,
                harmonyHooksTimed: harmonyHooksTimed
            )
        );

    private static ComparisonRow Row(IEnumerable<ComparisonRow> rows, string key) =>
        rows.First(row => row.Key == key);

    [Test]
    public static void ASessionComparedWithItselfHasNoIssues()
    {
        var session = Session(
            mods: [Mod("a", new() { [LoadDefs] = 100f }), Mod("b", new() { [LoadDefs] = 50f })]
        );

        var comparison = StartupImpactSessionComparison.Compare(session, session);

        Expect.IsEmpty(comparison.Issues);
        Expect.IsEmpty(comparison.AddedMods);
        Expect.IsEmpty(comparison.RemovedMods);
        Expect.All(comparison.Mods, row => row.Delta == 0f);
    }

    [Test]
    public static void AStageBegunTwiceIsOneRowWithItsPartsSummed()
    {
        const string SecondPass = nameof(LoadingStage.ExecuteToExecuteWhenFinished2);
        var reference = Session(stages: [new(SecondPass, 300f, 0f), new(SecondPass, 200f, 0f)]);
        var compared = Session(stages: [new(SecondPass, 700f, 0f)]);

        var comparison = StartupImpactSessionComparison.Compare(reference, compared);

        Expect.AreEqual(1, comparison.Stages.Count);
        Expect.AreApproximatelyEqual(500f, Row(comparison.Stages, SecondPass).Reference);
        Expect.AreApproximatelyEqual(200f, Row(comparison.Stages, SecondPass).Delta);
    }

    [Test]
    public static void StagesAreListedInLoadingOrderAndTheirChangesAddUp()
    {
        const string Xml = nameof(LoadingStage.LoadModXml);
        const string Resolve = nameof(LoadingStage.ResolveReferences);
        const string Atlas = nameof(LoadingStage.AtlasBaking);
        var reference = Session(stages: [new(Xml, 1000f, 0f), new(Resolve, 2000f, 0f)]);
        var compared = Session(
            stages: [new(Xml, 1500f, 0f), new(Resolve, 5000f, 0f), new(Atlas, 400f, 0f)]
        );

        var comparison = StartupImpactSessionComparison.Compare(reference, compared);

        Expect.That(() =>
            comparison.Stages.Select(row => row.Key).SequenceEqual([Xml, Resolve, Atlas])
        );
        Expect.AreApproximatelyEqual(500f + 3000f + 400f, comparison.StageDeltaSum);
    }

    [Test]
    public static void ModsOnlyInOneSessionAreAddedOrRemovedAndMakeTheListDifferent()
    {
        var reference = Session(
            mods:
            [
                Mod("kept", new() { [LoadDefs] = 100f }),
                Mod("gone", new() { [LoadDefs] = 80f }),
            ]
        );
        var compared = Session(
            mods: [Mod("kept", new() { [LoadDefs] = 100f }), Mod("new", new() { [LoadDefs] = 60f })]
        );

        var comparison = StartupImpactSessionComparison.Compare(reference, compared);

        Expect.That(() => comparison.AddedMods.SequenceEqual(["new"]));
        Expect.That(() => comparison.RemovedMods.SequenceEqual(["gone"]));
        Expect.IsTrue(comparison.Issues.Contains(ComparisonIssue.DifferentModList));
        Expect.IsFalse(Row(comparison.Mods, "new").InReference);
        Expect.IsTrue(Row(comparison.Mods, "new").InCompared);
        Expect.IsFalse(Row(comparison.Mods, "gone").InCompared);
    }

    [Test]
    public static void ADifferentModListHashMakesTheListDifferent()
    {
        var comparison = StartupImpactSessionComparison.Compare(
            Session(modListHash: 1),
            Session(modListHash: 2)
        );

        Expect.IsTrue(comparison.Issues.Contains(ComparisonIssue.DifferentModList));
    }

    [Test]
    public static void TheSameModsInAnotherOrderAreAnOrderChange()
    {
        var a = Mod("a", new() { [LoadDefs] = 100f });
        var b = Mod("b", new() { [LoadDefs] = 100f });

        var comparison = StartupImpactSessionComparison.Compare(
            Session(mods: [a, b]),
            Session(mods: [b, a])
        );

        Expect.IsTrue(comparison.ModOrderChanged);
        Expect.IsTrue(comparison.Issues.Contains(ComparisonIssue.ModOrderChanged));
        Expect.IsFalse(comparison.Issues.Contains(ComparisonIssue.DifferentModList));
    }

    [Test]
    public static void HookTimingOnInOneSessionAndOffInTheOtherIsAnIssue()
    {
        var comparison = StartupImpactSessionComparison.Compare(
            Session(harmonyHooksTimed: false),
            Session(harmonyHooksTimed: true)
        );

        Expect.IsTrue(comparison.Issues.Contains(ComparisonIssue.HarmonyHookTimingDiffers));
        Expect.IsFalse(comparison.Issues.Contains(ComparisonIssue.HarmonyHookTimingUnknown));
    }

    [Test]
    public static void HookTimingNotRecordedInOneSessionIsUnknownRatherThanDifferent()
    {
        var comparison = StartupImpactSessionComparison.Compare(
            Session(harmonyHooksTimed: true),
            Session(harmonyHooksTimed: null)
        );

        Expect.IsTrue(comparison.Issues.Contains(ComparisonIssue.HarmonyHookTimingUnknown));
        Expect.IsFalse(comparison.Issues.Contains(ComparisonIssue.HarmonyHookTimingDiffers));
    }

    [Test]
    public static void OnlyOneSessionTimedToTheMenuIsAnIssue()
    {
        var comparison = StartupImpactSessionComparison.Compare(
            Session(timeToMenu: 12000f),
            Session(timeToMenu: 0f)
        );

        Expect.IsTrue(comparison.Issues.Contains(ComparisonIssue.NotBothTimedToMenu));
    }

    [Test]
    public static void AStepIsOneRowAcrossItsParametersTheBaseGameAndEveryMod()
    {
        var hookA = HookTiming.Category + "|A.Method";
        var hookB = HookTiming.Category + "|B.Method";
        var reference = Session(
            metrics: new() { [hookA] = 10f },
            mods: [Mod("a", new() { [hookA] = 100f }), Mod("b", new() { [hookB] = 200f })]
        );

        var comparison = StartupImpactSessionComparison.Compare(reference, Session());

        Expect.AreEqual(1, comparison.Steps.Count(row => row.Key == HookTiming.Category));
        Expect.AreApproximatelyEqual(310f, Row(comparison.Steps, HookTiming.Category).Reference);
    }

    [Test]
    public static void ModsAndStepsAreListedLargestChangeFirst()
    {
        var reference = Session(
            mods:
            [
                Mod("small", new() { [ModConstructor] = 100f }),
                Mod("large", new() { [ResolveReferences] = 100f }),
            ]
        );
        var compared = Session(
            mods:
            [
                Mod("small", new() { [ModConstructor] = 150f }),
                Mod("large", new() { [ResolveReferences] = 1100f }),
            ]
        );

        var comparison = StartupImpactSessionComparison.Compare(reference, compared);

        Expect.AreEqual("large", comparison.Mods[0].Key);
        Expect.AreEqual(ResolveReferences, comparison.Steps[0].Key);
    }

    [Test]
    public static void FiguresUnderAMillisecondInBothSessionsAreLeftOut()
    {
        var comparison = StartupImpactSessionComparison.Compare(
            Session(mods: [Mod("tiny", new() { [LoadDefs] = 0.5f })]),
            Session(mods: [Mod("tiny", new() { [LoadDefs] = 0.2f })])
        );

        Expect.IsEmpty(comparison.Mods);
        Expect.IsEmpty(comparison.Steps);
    }

    [Test]
    public static void StagesKnownToVaryBetweenRunsAreFlagged()
    {
        const string Xml = nameof(LoadingStage.LoadModXml);
        const string Resolve = nameof(LoadingStage.ResolveReferences);
        var session = Session(stages: [new(Xml, 1000f, 0f), new(Resolve, 2000f, 0f)]);

        var comparison = StartupImpactSessionComparison.Compare(session, session);

        Expect.IsTrue(Row(comparison.Stages, Xml).Noisy);
        Expect.IsFalse(Row(comparison.Stages, Resolve).Noisy);
    }

    [Test]
    public static void AModWhoseChangeIsMostlyInStepsKnownToVaryIsFlagged()
    {
        var reference = Session(
            mods:
            [
                Mod("noisy", new() { [LoadDefs] = 100f, [ResolveReferences] = 100f }),
                Mod("steady", new() { [LoadDefs] = 100f, [ResolveReferences] = 100f }),
            ]
        );
        var compared = Session(
            mods:
            [
                Mod("noisy", new() { [LoadDefs] = 900f, [ResolveReferences] = 150f }),
                Mod("steady", new() { [LoadDefs] = 150f, [ResolveReferences] = 900f }),
            ]
        );

        var comparison = StartupImpactSessionComparison.Compare(reference, compared);

        Expect.IsTrue(Row(comparison.Mods, "noisy").Noisy);
        Expect.IsFalse(Row(comparison.Mods, "steady").Noisy);
    }

    // A mod that is new is a change to the list, not run-to-run noise, whatever steps its time
    // is in.
    [Test]
    public static void AnAddedModIsNeverFlaggedAsNoise()
    {
        var comparison = StartupImpactSessionComparison.Compare(
            Session(),
            Session(mods: [Mod("new", new() { [LoadDefs] = 900f })])
        );

        Expect.IsFalse(Row(comparison.Mods, "new").Noisy);
    }

    [Test]
    public static void AModsDetailsSayWhichOfItsCategoriesChanged()
    {
        var comparison = StartupImpactSessionComparison.Compare(
            Session(mods: [Mod("a", new() { [LoadDefs] = 100f, [ResolveReferences] = 100f })]),
            Session(mods: [Mod("a", new() { [LoadDefs] = 100f, [ResolveReferences] = 400f })])
        );

        var details = Row(comparison.Mods, "a").Details;
        Expect.AreEqual(ResolveReferences, details[0].Key);
        Expect.AreApproximatelyEqual(300f, details[0].Delta);
    }

    [Test]
    public static void AChangeInTimeIsWrittenWithItsSign()
    {
        Expect.AreEqual(
            "+" + ProfilerBar.TimeText(250f),
            DialogStartupImpactComparison.TimeChangeText(250f)
        );
        Expect.AreEqual(
            "−" + ProfilerBar.TimeText(250f),
            DialogStartupImpactComparison.TimeChangeText(-250f)
        );
        Expect.AreEqual(
            ProfilerBar.TimeText(0f),
            DialogStartupImpactComparison.TimeChangeText(0.2f)
        );
    }

    [Test]
    public static void TheLoadingTimeIsTheFirstTotal()
    {
        var comparison = StartupImpactSessionComparison.Compare(
            Session(loadingTime: 10000f),
            Session(loadingTime: 15000f)
        );

        Expect.AreEqual(StartupImpactSessionComparison.LoadingTimeKey, comparison.Totals[0].Key);
        Expect.AreApproximatelyEqual(5000f, comparison.Totals[0].Delta);
    }
}
