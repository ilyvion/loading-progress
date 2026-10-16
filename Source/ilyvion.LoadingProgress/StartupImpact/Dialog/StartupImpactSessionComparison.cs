namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

/// <summary>
/// One figure as the reference session and the compared session have it.
/// </summary>
internal sealed class ComparisonRow(
    string key,
    string label,
    float reference,
    float compared,
    bool noisy = false
)
{
    public string Key { get; } = key;
    public string Label { get; } = label;
    public float Reference { get; } = reference;
    public float Compared { get; } = compared;

    /// <summary>
    /// Whether the figure is known to vary a lot between runs with nothing changed.
    /// </summary>
    public bool Noisy { get; } = noisy;

    /// <summary>
    /// For a mod, whether the session has it loaded at all; always true for any other figure.
    /// </summary>
    public bool InReference { get; init; } = true;

    /// <inheritdoc cref="InReference"/>
    public bool InCompared { get; init; } = true;

    /// <summary>
    /// For a mod, how its change splits by category, largest change first; empty otherwise.
    /// </summary>
    public IReadOnlyList<ComparisonRow> Details { get; init; } = [];

    public float Delta => Compared - Reference;
}

/// <summary>
/// Something about a pair of sessions that makes their figures less comparable.
/// </summary>
internal enum ComparisonIssue
{
    DifferentModList,
    ModOrderChanged,
    HarmonyHookTimingDiffers,
    HarmonyHookTimingUnknown,
    NotBothTimedToMenu,
}

/// <summary>
/// Two stored sessions lined up figure by figure: the compared session's figures against the
/// reference session's.
/// </summary>
internal sealed class StartupImpactSessionComparison
{
    internal const string LoadingTimeKey = "LoadingProgress.StartupImpact.Compare.LoadingTime";
    internal const string StartupTimeKey = "LoadingProgress.StartupImpact.Compare.StartupTime";
    internal const string TimeToMenuKey = "LoadingProgress.StartupImpact.Compare.TimeToMenu";
    internal const string EarlyLaunchKey = StartupImpactSessionViewData.EarlyLaunchKey;
    internal const string ModsLoadedKey = "LoadingProgress.StartupImpact.Compare.ModsLoaded";
    internal const string DefsParsedKey = "LoadingProgress.StartupImpact.Compare.DefsParsed";
    internal const string PatchOperationsKey =
        "LoadingProgress.StartupImpact.Compare.PatchOperations";

    // A figure under this in both sessions is left out of the lists.
    private const float MinShownMs = 1f;

    // The stages and steps that varied by seconds between runs with nothing changed.
    private static readonly HashSet<string> NoisyStages =
    [
        nameof(LoadingStage.LoadModXml),
        nameof(LoadingStage.ExecuteToExecuteWhenFinished),
        nameof(LoadingStage.ExecuteToExecuteWhenFinished2),
        nameof(LoadingStage.AtlasBaking),
    ];

    private static readonly HashSet<string> NoisySteps =
    [
        "LoadingProgress.StartupImpact.LoadDefs",
        "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AudioClips",
        "LoadingProgress.StartupImpact.ExecuteToExecuteWhenFinished",
        "LoadingProgress.StartupImpact.GlobalTextureAtlasManagerBakeStaticAtlases",
        PostLoadTracker.Category,
    ];

    private StartupImpactSessionComparison(
        StartupImpactSessionFigures reference,
        StartupImpactSessionFigures compared
    )
    {
        Reference = reference;
        Compared = compared;
    }

    public StartupImpactSessionFigures Reference { get; }
    public StartupImpactSessionFigures Compared { get; }

    /// <summary>
    /// The loading time, the startup time, the time to the main menu and the early launch time.
    /// </summary>
    public IReadOnlyList<ComparisonRow> Totals { get; private init; } = [];

    /// <summary>
    /// Mods loaded, defs parsed and patch operations applied, each only when both sessions
    /// recorded it.
    /// </summary>
    public IReadOnlyList<ComparisonRow> Counts { get; private init; } = [];

    /// <summary>
    /// Every loading stage's wall time, in loading order.
    /// </summary>
    public IReadOnlyList<ComparisonRow> Stages { get; private init; } = [];

    /// <summary>
    /// What the stages' changes add up to. The stages make up the loading time, so this is the
    /// part of its change that falls in a stage.
    /// </summary>
    public float StageDeltaSum { get; private init; }

    /// <summary>
    /// Time per step, the base game's and every mod's summed, largest change first.
    /// </summary>
    public IReadOnlyList<ComparisonRow> Steps { get; private init; } = [];

    /// <summary>
    /// Every mod's time, largest change first.
    /// </summary>
    public IReadOnlyList<ComparisonRow> Mods { get; private init; } = [];

    /// <summary>
    /// The names of the mods only the compared session has.
    /// </summary>
    public IReadOnlyList<string> AddedMods { get; private init; } = [];

    /// <summary>
    /// The names of the mods only the reference session has.
    /// </summary>
    public IReadOnlyList<string> RemovedMods { get; private init; } = [];

    /// <summary>
    /// Whether the mods both sessions have were loaded in a different order.
    /// </summary>
    public bool ModOrderChanged { get; private init; }

    public IReadOnlyList<ComparisonIssue> Issues { get; private init; } = [];

    internal static bool IsNoisyStage(string stage) => NoisyStages.Contains(stage);

    internal static bool IsNoisyStep(string phaseKey) => NoisySteps.Contains(phaseKey);

    internal static StartupImpactSessionComparison Compare(
        StartupImpactSessionFigures reference,
        StartupImpactSessionFigures compared
    )
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(compared);

        List<ComparisonRow> counts = [];
        AddCount(ModsLoadedKey, f => f.ModsLoaded);
        AddCount(DefsParsedKey, f => f.DefsParsed);
        AddCount(PatchOperationsKey, f => f.PatchOperationsApplied);

        var stages = Pair(
            reference.StageOrder.Concat(compared.StageOrder).Distinct(),
            reference.Stages,
            compared.Stages,
            StartupImpactSessionViewData.StageLabel,
            IsNoisyStage
        );

        List<string> added =
        [
            .. compared
                .ModOrder.Where(key => !reference.Mods.ContainsKey(key))
                .Select(key => compared.Mods[key].Name),
        ];
        List<string> removed =
        [
            .. reference
                .ModOrder.Where(key => !compared.Mods.ContainsKey(key))
                .Select(key => reference.Mods[key].Name),
        ];
        var orderChanged = !reference
            .ModOrder.Where(compared.Mods.ContainsKey)
            .SequenceEqual(compared.ModOrder.Where(reference.Mods.ContainsKey));

        return new StartupImpactSessionComparison(reference, compared)
        {
            Totals =
            [
                Row(LoadingTimeKey, f => f.LoadingTime),
                Row(StartupTimeKey, f => f.StartupTime),
                Row(TimeToMenuKey, f => f.TimeToMenu, noisy: true),
                Row(EarlyLaunchKey, f => f.EarlyLaunchTime),
            ],
            Counts = counts,
            Stages = stages,
            StageDeltaSum = stages.Sum(row => row.Delta),
            Steps = ByLargestChange(
                Pair(
                    reference.Steps.Keys.Concat(compared.Steps.Keys).Distinct(),
                    reference.Steps,
                    compared.Steps,
                    StartupImpactPhaseViewData.LabelFor,
                    IsNoisyStep
                )
            ),
            Mods = ByLargestChange(CompareMods(reference, compared)),
            AddedMods = added,
            RemovedMods = removed,
            ModOrderChanged = orderChanged,
            Issues = IssuesOf(
                reference,
                compared,
                added.Count > 0 || removed.Count > 0,
                orderChanged
            ),
        };

        ComparisonRow Row(
            string key,
            Func<StartupImpactSessionFigures, float> figure,
            bool noisy = false
        )
        {
            return new(key, key.Translate(), figure(reference), figure(compared), noisy);
        }

        void AddCount(string key, Func<StartupImpactSessionFigures, int?> figure)
        {
            if (figure(reference) is int referenceCount && figure(compared) is int comparedCount)
            {
                counts.Add(new ComparisonRow(key, key.Translate(), referenceCount, comparedCount));
            }
        }
    }

    private static List<ComparisonRow> CompareMods(
        StartupImpactSessionFigures reference,
        StartupImpactSessionFigures compared
    )
    {
        List<ComparisonRow> rows = [];
        foreach (var key in reference.ModOrder.Concat(compared.ModOrder).Distinct())
        {
            var inReference = reference.Mods.TryGetValue(key, out var referenceMod);
            var inCompared = compared.Mods.TryGetValue(key, out var comparedMod);
            var referenceMetrics = referenceMod?.Metrics ?? new Dictionary<string, float>();
            var comparedMetrics = comparedMod?.Metrics ?? new Dictionary<string, float>();

            var details = ByLargestChange(
                Pair(
                    referenceMetrics.Keys.Concat(comparedMetrics.Keys).Distinct(),
                    referenceMetrics,
                    comparedMetrics,
                    StartupImpactProfilerUtil.TranslateCategory,
                    category => IsNoisyStep(StartupImpactPhaseViewData.PhaseKey(category))
                )
            );

            var referenceTotal = referenceMod?.Total ?? 0f;
            var comparedTotal = comparedMod?.Total ?? 0f;
            if (referenceTotal < MinShownMs && comparedTotal < MinShownMs)
            {
                continue;
            }

            rows.Add(
                new ComparisonRow(
                    key,
                    (comparedMod ?? referenceMod)!.Name,
                    referenceTotal,
                    comparedTotal,
                    inReference && inCompared && MostlyNoisy(details)
                )
                {
                    InReference = inReference,
                    InCompared = inCompared,
                    Details = details,
                }
            );
        }
        return rows;
    }

    /// <summary>
    /// Whether at least half of how much a mod's categories changed is in categories known to
    /// vary between runs.
    /// </summary>
    internal static bool MostlyNoisy(IEnumerable<ComparisonRow> details)
    {
        var noisy = 0f;
        var steady = 0f;
        foreach (var detail in details)
        {
            if (detail.Noisy)
            {
                noisy += Math.Abs(detail.Delta);
            }
            else
            {
                steady += Math.Abs(detail.Delta);
            }
        }
        return noisy >= MinShownMs && noisy >= steady;
    }

    private static List<ComparisonRow> Pair(
        IEnumerable<string> keys,
        IReadOnlyDictionary<string, float> reference,
        IReadOnlyDictionary<string, float> compared,
        Func<string, string> label,
        Func<string, bool> noisy
    )
    {
        List<ComparisonRow> rows = [];
        foreach (var key in keys)
        {
            var referenceValue = reference.TryGetValue(key, out var r) ? r : 0f;
            var comparedValue = compared.TryGetValue(key, out var c) ? c : 0f;
            if (referenceValue < MinShownMs && comparedValue < MinShownMs)
            {
                continue;
            }
            rows.Add(new ComparisonRow(key, label(key), referenceValue, comparedValue, noisy(key)));
        }
        return rows;
    }

    private static List<ComparisonRow> ByLargestChange(List<ComparisonRow> rows)
    {
        rows.SortByDescending(row => Math.Abs(row.Delta));
        return rows;
    }

    private static List<ComparisonIssue> IssuesOf(
        StartupImpactSessionFigures reference,
        StartupImpactSessionFigures compared,
        bool modsAddedOrRemoved,
        bool modOrderChanged
    )
    {
        List<ComparisonIssue> issues = [];
        if (reference.ModListHash != compared.ModListHash || modsAddedOrRemoved)
        {
            issues.Add(ComparisonIssue.DifferentModList);
        }
        if (modOrderChanged)
        {
            issues.Add(ComparisonIssue.ModOrderChanged);
        }
        if (reference.HarmonyHooksTimed is not { } referenceHooks)
        {
            issues.Add(ComparisonIssue.HarmonyHookTimingUnknown);
        }
        else if (compared.HarmonyHooksTimed is not { } comparedHooks)
        {
            issues.Add(ComparisonIssue.HarmonyHookTimingUnknown);
        }
        else if (referenceHooks != comparedHooks)
        {
            issues.Add(ComparisonIssue.HarmonyHookTimingDiffers);
        }
        if ((reference.TimeToMenu > 0f) != (compared.TimeToMenu > 0f))
        {
            issues.Add(ComparisonIssue.NotBothTimedToMenu);
        }
        return issues;
    }
}
