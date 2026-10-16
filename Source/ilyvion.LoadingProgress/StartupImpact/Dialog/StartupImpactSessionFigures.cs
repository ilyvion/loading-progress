namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

/// <summary>
/// The figures of one stored session that a comparison lines up against other sessions', each
/// keyed so the same figure can be looked up in any of them.
/// </summary>
internal sealed class StartupImpactSessionFigures
{
    /// <summary>
    /// One mod's figures: its name as shown, its time on the loading thread and that time by
    /// category, with each category's per-call parameter kept.
    /// </summary>
    internal sealed record ModFigures(
        string Name,
        float Total,
        IReadOnlyDictionary<string, float> Metrics
    );

    private StartupImpactSessionFigures() { }

    public DateTime SavedAtUtc { get; private init; }

    /// <summary>
    /// The startup time the startup impact window gives the session: see
    /// <see cref="StartupImpactSessionViewData.Span(StartupImpactSessionData)"/>.
    /// </summary>
    public float StartupTime { get; private init; }

    public float LoadingTime { get; private init; }

    /// <summary>
    /// Milliseconds to the main menu being usable, or 0 for a session that did not record it.
    /// </summary>
    public float TimeToMenu { get; private init; }

    public float EarlyLaunchTime { get; private init; }
    public int? ModsLoaded { get; private init; }
    public int? DefsParsed { get; private init; }
    public int? PatchOperationsApplied { get; private init; }
    public int ModListHash { get; private init; }
    public bool? HarmonyHooksTimed { get; private init; }

    /// <summary>
    /// Every loading stage's wall time, a stage begun more than once summed into one.
    /// </summary>
    public IReadOnlyDictionary<string, float> Stages { get; private init; } =
        new Dictionary<string, float>();

    /// <summary>
    /// The stages in <see cref="Stages"/>, in the order the session first entered them.
    /// </summary>
    public IReadOnlyList<string> StageOrder { get; private init; } = [];

    /// <summary>
    /// Time on the loading thread per step, the base game's and every mod's summed, keyed by
    /// <see cref="StartupImpactPhaseViewData.PhaseKey"/>.
    /// </summary>
    public IReadOnlyDictionary<string, float> Steps { get; private init; } =
        new Dictionary<string, float>();

    /// <summary>
    /// Every mod, keyed by <see cref="ModKey"/>.
    /// </summary>
    public IReadOnlyDictionary<string, ModFigures> Mods { get; private init; } =
        new Dictionary<string, ModFigures>();

    /// <summary>
    /// The keys of <see cref="Mods"/>, in the order the session lists them.
    /// </summary>
    public IReadOnlyList<string> ModOrder { get; private init; } = [];

    /// <summary>
    /// What a mod is looked up by in another session: its package id, or its name for an entry
    /// that has none.
    /// </summary>
    internal static string ModKey(StartupImpactSessionModData mod) =>
        string.IsNullOrEmpty(mod.ModPackageId) ? "name:" + mod.ModName : mod.ModPackageId;

    internal static StartupImpactSessionFigures From(StartupImpactSessionData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        Dictionary<string, float> stages = [];
        List<string> stageOrder = [];
        foreach (var stage in data.StageTimings)
        {
            if (stages.TryGetValue(stage.Stage, out var wallMs))
            {
                stages[stage.Stage] = wallMs + stage.WallMs;
            }
            else
            {
                stages[stage.Stage] = stage.WallMs;
                stageOrder.Add(stage.Stage);
            }
        }

        Dictionary<string, float> steps = [];
        foreach (var entry in data.Metrics)
        {
            AddTo(steps, StartupImpactPhaseViewData.PhaseKey(entry.Key), entry.Value);
        }

        Dictionary<string, ModFigures> mods = [];
        List<string> modOrder = [];
        foreach (var mod in data.Mods)
        {
            foreach (var entry in mod.Metrics)
            {
                AddTo(steps, StartupImpactPhaseViewData.PhaseKey(entry.Key), entry.Value);
            }

            var key = ModKey(mod);
            if (mods.TryGetValue(key, out var existing))
            {
                Dictionary<string, float> merged = new(existing.Metrics);
                foreach (var entry in mod.Metrics)
                {
                    AddTo(merged, entry.Key, entry.Value);
                }
                mods[key] = existing with
                {
                    Total = existing.Total + mod.TotalImpact,
                    Metrics = merged,
                };
            }
            else
            {
                mods[key] = new ModFigures(
                    mod.ModName,
                    mod.TotalImpact,
                    new Dictionary<string, float>(mod.Metrics)
                );
                modOrder.Add(key);
            }
        }

        return new StartupImpactSessionFigures
        {
            SavedAtUtc = data.SavedAtUtc,
            StartupTime = StartupImpactSessionViewData.Span(data),
            LoadingTime = data.LoadingTime,
            TimeToMenu = data.TimeToMenu,
            EarlyLaunchTime = data.EarlyLaunchTime,
            ModsLoaded = data.ModsLoaded,
            DefsParsed = data.DefsParsed,
            PatchOperationsApplied = data.PatchOperationsApplied,
            ModListHash = data.ModListHash,
            HarmonyHooksTimed = data.HarmonyHooksTimed,
            Stages = stages,
            StageOrder = stageOrder,
            Steps = steps,
            Mods = mods,
            ModOrder = modOrder,
        };

        static void AddTo(Dictionary<string, float> sums, string key, float value)
        {
            sums[key] = (sums.TryGetValue(key, out var sum) ? sum : 0f) + value;
        }
    }
}
