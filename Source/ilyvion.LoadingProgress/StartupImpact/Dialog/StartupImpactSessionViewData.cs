using System.Text;

namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

internal sealed class StartupImpactSessionViewData
{
    public static readonly string[] CategoriesTotal =
    [
        "LoadingProgress.StartupImpact.Total.Mods",
        "LoadingProgress.StartupImpact.Total.ModsHidden",
        "LoadingProgress.StartupImpact.Total.BaseGame",
        "LoadingProgress.StartupImpact.Total.Others",
    ];

    /// <summary>
    /// One part of the remaining time: the loading stage it fell in (or the time after loading
    /// finished, keyed <see cref="AfterLoadingKey"/>), its text as shown, and how much.
    /// </summary>
    internal sealed record RemainingEntry(string Key, string Label, float Ms);

    internal const string AfterLoadingKey = "LoadingProgress.StartupImpact.Remaining.AfterLoading";

    // The second delayed-initialization pass borrows the first one's text, and the remaining
    // time lists both, so it is told apart there.
    private const string SecondPassKey = "LoadingProgress.StartupImpact.Remaining.SecondPass";

    private const int RemainingDetailLines = 10;

    private readonly StartupImpactSessionData sessionData;
    private readonly List<StartupImpactSessionModViewData> modViewData;
    private float hiddenModsLoadingTime;

    private readonly List<string> categories = [];
    private readonly List<string> categoriesNonMods = [];
    private readonly List<float> metricsNonMods = [];
    private readonly List<float> metricsOffThreadNonMods = [];
    private readonly List<float> metricsTotal = [];
    private readonly Dictionary<string, Color> categoryColorsNonMods = [];
    private readonly List<string> categoriesMods = [];
    private readonly List<float> metricsMods = [];
    private readonly Dictionary<string, Color> categoryColorsMods = [];
    private readonly List<RemainingEntry> remainingByStage = [];

    internal IReadOnlyList<StartupImpactSessionModViewData> ModViewData => modViewData.AsReadOnly();

    public float BasegameLoadingTime { get; private set; }
    public float OffThreadBasegameLoadingTime { get; private set; }
    public float ModsLoadingTime { get; private set; }
    public float MaxImpact { get; private set; }

    public IReadOnlyList<string> Categories => categories.AsReadOnly();
    public IReadOnlyList<string> CategoriesNonMods => categoriesNonMods.AsReadOnly();
    public IReadOnlyList<float> MetricsNonMods => metricsNonMods.AsReadOnly();
    public IReadOnlyList<float> MetricsOffThreadNonMods => metricsOffThreadNonMods.AsReadOnly();
    public IReadOnlyList<float> MetricsTotal => metricsTotal.AsReadOnly();
    public IReadOnlyDictionary<string, Color> CategoryColorsNonMods =>
        categoryColorsNonMods.AsReadOnly();
    public IReadOnlyList<string> CategoriesMods => categoriesMods.AsReadOnly();
    public IReadOnlyList<float> MetricsMods => metricsMods.AsReadOnly();
    public IReadOnlyDictionary<string, Color> CategoryColorsMods => categoryColorsMods.AsReadOnly();

    /// <summary>
    /// The span the totals bar covers, which the window's title gives as the startup time: see
    /// <see cref="Span(float, float, float, float)"/>.
    /// </summary>
    public float TotalWindow { get; private set; }

    /// <summary>
    /// The remaining part of the startup time, the part no category timed: see
    /// <see cref="RemainingTotal"/>. The totals bar's last segment.
    /// </summary>
    public float RemainingLoadingTime =>
        metricsTotal.Count == CategoriesTotal.Length ? metricsTotal[^1] : 0f;

    /// <summary>
    /// The remaining time: what its entries add up to, for a session that kept its stages;
    /// for one saved before stages were kept, what is left of <paramref name="window"/> once
    /// the mods, hidden or not, and the base game have had their
    /// <paramref name="timedTotal"/>.
    /// </summary>
    /// <remarks>
    /// The two differ by the time categories on two timers both cover, as a mod's
    /// <c>TryRegister</c> does inside the base game's <c>ParseAndProcessXML</c>. The mod's
    /// total and the base game's each count it, while the stage ledger credits it once, so
    /// the entries hold all the time no category timed and the window's leftover falls short
    /// of it by the shared time. The totals bar's segments then come to that much more than
    /// the window.
    /// </remarks>
    internal static float RemainingTotal(
        bool stagesKept,
        IEnumerable<float> entries,
        float window,
        float timedTotal
    ) => stagesKept ? entries.Sum() : Math.Max(0f, window - timedTotal);

    /// <summary>
    /// How the remaining time splits by loading stage, largest first, with what came after
    /// loading finished as an entry of its own. Empty for sessions saved before stages were
    /// kept.
    /// </summary>
    public IReadOnlyList<RemainingEntry> RemainingByStage => remainingByStage.AsReadOnly();

    /// <summary>
    /// The remaining split as the tooltip of the totals bar's remaining segment, or null when
    /// there is nothing to say.
    /// </summary>
    public IReadOnlyDictionary<string, string>? RemainingTooltipDetails { get; private set; }

    public StartupImpactSessionViewData(StartupImpactSessionData sessionData)
    {
        this.sessionData = sessionData;
        modViewData = [.. sessionData.Mods.Select(mod => new StartupImpactSessionModViewData(mod))];

        // The remaining entries come before the totals, which take the remaining time from them.
        CalculateBaseGameStats();
        CalculateRemainingByStage();
        CalculateModStats();

        foreach (var modView in modViewData)
        {
            modView.Initialize(this);
        }
    }

    public void CalculateModStats()
    {
        ModsLoadingTime = 0;
        hiddenModsLoadingTime = 0;
        MaxImpact = 0;

        HashSet<string> categorySet = [];
        foreach (var modView in modViewData)
        {
            if (modView.HideInUi)
            {
                hiddenModsLoadingTime += modView.ModData.TotalImpact;
            }
            else
            {
                if (MaxImpact < modView.ModData.TotalImpact)
                {
                    MaxImpact = modView.ModData.TotalImpact;
                }

                ModsLoadingTime += modView.ModData.TotalImpact;
            }

            foreach (var entry in modView.ModData.Metrics)
            {
                _ = categorySet.Add(entry.Key);
            }

            foreach (var entry in modView.ModData.OffThreadMetrics)
            {
                _ = categorySet.Add(entry.Key);
            }
        }

        categories.Clear();
        categories.AddRange(categorySet.OrderBy(category => category));

        // A session with no loading time takes its timed steps as one. Any other keeps the
        // stored loading time as the point the clock stopped, which is what the session file
        // and external tools read, even when the steps come to more: the bar widens instead.
        var totalLoadingTime = ModsLoadingTime + hiddenModsLoadingTime + BasegameLoadingTime;
        TotalWindow = Span(
            sessionData.LoadingTime,
            sessionData.TimeToMenu,
            PostLoadAttributedTime(sessionData),
            totalLoadingTime
        );
        if (sessionData.LoadingTime == 0)
        {
            sessionData.OverrideLoadingTime(totalLoadingTime);
        }

        metricsTotal.Clear();
        metricsTotal.AddRange([
            ModsLoadingTime,
            hiddenModsLoadingTime,
            BasegameLoadingTime,
            RemainingTotal(
                sessionData.StageTimings.Count > 0,
                remainingByStage.Select(entry => entry.Ms),
                TotalWindow,
                totalLoadingTime
            ),
        ]);

        categoriesMods.Clear();
        metricsMods.Clear();
        foreach (
            var modView in modViewData
                .Where(m => !m.HideInUi && m.ModData.TotalImpact > 0)
                .OrderByDescending(m => m.ModData.TotalImpact)
        )
        {
            var name = modView.ModData.ModName;
            categoryColorsMods[name] = StartupImpactProfilerUtil.HashColor(
                modView.ModData.ModPackageId
            );
            categoriesMods.Add(name);
            metricsMods.Add(modView.ModData.TotalImpact);
        }
    }

    public void CalculateBaseGameStats()
    {
        categoriesNonMods.Clear();
        metricsNonMods.Clear();
        metricsOffThreadNonMods.Clear();
        BasegameLoadingTime = 0;

        foreach (var entry in sessionData.Metrics)
        {
            var cat = entry.Key;

            categoryColorsNonMods[cat] = StartupImpactProfilerUtil.HashColor(cat);
            categoriesNonMods.Add(cat);
            metricsNonMods.Add(entry.Value);
            BasegameLoadingTime += entry.Value;
            metricsOffThreadNonMods.Add(
                sessionData.OffThreadMetrics.TryGetValue(cat, out var offValue) ? offValue : 0f
            );
        }

        foreach (var entry in sessionData.OffThreadMetrics)
        {
            if (categoriesNonMods.Contains(entry.Key))
            {
                continue;
            }

            categoryColorsNonMods[entry.Key] = StartupImpactProfilerUtil.HashColor(entry.Key);
            categoriesNonMods.Add(entry.Key);
            metricsNonMods.Add(0f);
            metricsOffThreadNonMods.Add(entry.Value);
        }

        OffThreadBasegameLoadingTime = sessionData.OffThreadTotalImpact;
    }

    private void CalculateRemainingByStage()
    {
        remainingByStage.Clear();
        RemainingTooltipDetails = null;

        remainingByStage.AddRange(
            RemainingEntries(
                sessionData.StageTimings,
                sessionData.LoadingTime,
                sessionData.TimeToMenu,
                PostLoadAttributedTime(sessionData)
            )
        );
        if (remainingByStage.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder(
            "LoadingProgress.StartupImpact.Remaining.ByStage".Translate().ToString()
        );
        foreach (var entry in remainingByStage.Take(RemainingDetailLines))
        {
            _ = sb.Append('\n')
                .Append(entry.Label)
                .Append(": ")
                .Append(ProfilerBar.TimeText(entry.Ms));
        }
        RemainingTooltipDetails = new Dictionary<string, string>
        {
            ["LoadingProgress.StartupImpact.Total.Others"] = sb.ToString(),
        };
    }

    /// <summary>
    /// The remaining entries a session's stages and its time to the menu give, largest first:
    /// each stage's wall time less what categories accounted for in it, and what came after
    /// loading finished less what was timed there, the long events and the deferred actions
    /// they queued. Anything under a millisecond is left out.
    /// </summary>
    internal static IReadOnlyList<RemainingEntry> RemainingEntries(
        IEnumerable<StartupImpactStageData> stages,
        float loadingTime,
        float timeToMenu,
        float postLoadAttributedMs
    )
    {
        List<RemainingEntry> entries = [];
        foreach (var stage in stages)
        {
            if (stage.RemainingMs >= 1f)
            {
                var label = StartupImpactSessionIndexEntry.TranslateStage(stage.Stage);
                if (stage.Stage == nameof(LoadingStage.ExecuteToExecuteWhenFinished2))
                {
                    label = SecondPassKey.Translate(label);
                }
                entries.Add(new RemainingEntry(stage.Stage, label, stage.RemainingMs));
            }
        }

        if (timeToMenu > loadingTime)
        {
            var afterLoading = timeToMenu - loadingTime - postLoadAttributedMs;
            if (afterLoading >= 1f)
            {
                entries.Add(
                    new RemainingEntry(AfterLoadingKey, AfterLoadingKey.Translate(), afterLoading)
                );
            }
        }
        entries.Sort((a, b) => b.Ms.CompareTo(a.Ms));
        return entries;
    }

    /// <summary>
    /// The startup time a session is shown and listed by: its time to the main menu when it
    /// recorded one. Without one, as when the startup went into a game or the menu never
    /// settled, it is the loading time plus what was timed after loading, all of which ran
    /// after the clock stopped. It is never less than the timed steps' total, so the totals bar
    /// holds them all.
    /// </summary>
    internal static float Span(
        float loadingTime,
        float timeToMenu,
        float postLoadAttributedMs,
        float timedTotal
    ) => Math.Max(Math.Max(timeToMenu, loadingTime + postLoadAttributedMs), timedTotal);

    /// <summary>
    /// <see cref="Span(float, float, float, float)"/> for a stored session.
    /// </summary>
    internal static float Span(StartupImpactSessionData sessionData) =>
        Span(
            sessionData.LoadingTime,
            sessionData.TimeToMenu,
            PostLoadAttributedTime(sessionData),
            sessionData.Metrics.Sum(entry => entry.Value)
                + sessionData.Mods.Sum(mod => mod.TotalImpact)
        );

    /// <summary>
    /// Time between the end of loading and the main menu that some category did account for:
    /// everything timed under <see cref="PostLoadTracker.Category"/>, whoever ran it.
    /// </summary>
    /// <remarks>
    /// The stage ledger closes when loading ends, so it holds none of this time, and a saved
    /// session has only its metrics to go by. These keep the time the game sat paused in the
    /// background out, as the time to the menu does.
    /// </remarks>
    private static float PostLoadAttributedTime(StartupImpactSessionData sessionData)
    {
        var total = 0f;
        foreach (var entry in sessionData.Metrics)
        {
            if (entry.Key.StartsWith(PostLoadTracker.Category, StringComparison.Ordinal))
            {
                total += entry.Value;
            }
        }
        foreach (var mod in sessionData.Mods)
        {
            foreach (var entry in mod.Metrics)
            {
                if (entry.Key.StartsWith(PostLoadTracker.Category, StringComparison.Ordinal))
                {
                    total += entry.Value;
                }
            }
        }
        return total;
    }
}
