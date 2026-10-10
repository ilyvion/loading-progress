namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

/// <summary>
/// The time a set of mods spent in one loading phase, summed across all of them, along with
/// each mod's share of it.
/// </summary>
internal sealed class StartupImpactPhaseViewData
{
    private readonly List<StartupImpactSessionModData> mods = [];
    private readonly List<string> modNames = [];
    private readonly List<float> metrics = [];
    private readonly List<float> offThreadMetrics = [];

    private StartupImpactPhaseViewData(string key)
    {
        Key = key;
    }

    /// <summary>
    /// The phase's profiler category, with any per-call parameter after '|' removed.
    /// </summary>
    public string Key { get; }

    public string Label => field ??= LabelFor(Key);

    public float TotalImpact { get; private set; }
    public float OffThreadTotalImpact { get; private set; }

    /// <summary>
    /// The mods that spent time in this phase, largest on-thread share first.
    /// </summary>
    public IReadOnlyList<StartupImpactSessionModData> Mods => mods.AsReadOnly();

    /// <summary>
    /// The names of <see cref="Mods"/>, in the same order; the categories for this phase's bar.
    /// </summary>
    public IReadOnlyList<string> ModNames => modNames.AsReadOnly();

    public IReadOnlyList<float> Metrics => metrics.AsReadOnly();
    public IReadOnlyList<float> OffThreadMetrics => offThreadMetrics.AsReadOnly();

    /// <summary>
    /// Sums the given mods' metrics per phase, leaving out phases no mod spent time in.
    /// </summary>
    internal static List<StartupImpactPhaseViewData> FromMods(
        IEnumerable<StartupImpactSessionModData> mods
    )
    {
        Dictionary<
            string,
            Dictionary<StartupImpactSessionModData, (float On, float Off)>
        > byPhase = [];
        foreach (var mod in mods)
        {
            foreach (var entry in mod.Metrics)
            {
                Add(byPhase, mod, entry.Key, entry.Value, offThread: false);
            }
            foreach (var entry in mod.OffThreadMetrics)
            {
                Add(byPhase, mod, entry.Key, entry.Value, offThread: true);
            }
        }

        List<StartupImpactPhaseViewData> phases = [];
        foreach (var (key, byMod) in byPhase)
        {
            var phase = new StartupImpactPhaseViewData(key);
            foreach (
                var (mod, (on, off)) in byMod
                    .OrderByDescending(e => e.Value.On)
                    .ThenByDescending(e => e.Value.Off)
            )
            {
                phase.mods.Add(mod);
                phase.modNames.Add(mod.ModName);
                phase.metrics.Add(on);
                phase.offThreadMetrics.Add(off);
                phase.TotalImpact += on;
                phase.OffThreadTotalImpact += off;
            }
            if (phase.TotalImpact > 0 || phase.OffThreadTotalImpact > 0)
            {
                phases.Add(phase);
            }
        }
        return phases;

        static void Add(
            Dictionary<
                string,
                Dictionary<StartupImpactSessionModData, (float On, float Off)>
            > byPhase,
            StartupImpactSessionModData mod,
            string category,
            float value,
            bool offThread
        )
        {
            var key = PhaseKey(category);
            if (!byPhase.TryGetValue(key, out var byMod))
            {
                byPhase[key] = byMod = [];
            }
            var (on, off) = byMod.TryGetValue(mod, out var sums) ? sums : default;
            byMod[mod] = offThread ? (on, off + value) : (on + value, off);
        }
    }

    /// <summary>
    /// The category without its per-call parameter. Time another mod's code ran in a
    /// category's place is a phase of its own per mod that replaced it.
    /// </summary>
    internal static string PhaseKey(string category)
    {
        if (
            StartupImpactProfilerUtil.TrySplitReplaced(category, out var replaced, out var replacer)
        )
        {
            return StartupImpactProfilerUtil.ReplacedBy(PhaseKey(replaced), replacer);
        }

        var pipeIdx = category.IndexOf('|', StringComparison.Ordinal);
        return pipeIdx < 0 ? category : category[..pipeIdx];
    }

    /// <summary>
    /// A category that takes a per-call parameter is labelled by its <c>.Grouped</c> key, since
    /// its own label needs that parameter.
    /// </summary>
    private static string LabelFor(string key) =>
        StartupImpactProfilerUtil.TrySplitReplaced(key, out var replaced, out var replacer)
            ? StartupImpactProfilerUtil.ReplacedByKey.Translate(LabelFor(replaced), replacer)
        : $"{key}.Grouped" is var groupedKey && groupedKey.CanTranslate() ? groupedKey.Translate()
        : StartupImpactProfilerUtil.TranslateCategory(key);
}
