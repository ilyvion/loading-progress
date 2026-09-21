namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

internal sealed class StartupImpactSessionData : IExposable
{
    private float loadingTime;
    private Dictionary<string, float> metrics = [];
    private float totalImpact;
    private Dictionary<string, float> offThreadMetrics = [];
    private float offThreadTotalImpact;

    private List<StartupImpactSessionModData> mods = [];

    private int? modsLoaded;
    private int? defsParsed;
    private int? patchOperationsApplied;

    private long savedAtUtcTicks;
    private int modListHash;

    public float LoadingTime => loadingTime;
    public IReadOnlyDictionary<string, float> Metrics => metrics.AsReadOnly();
    public float TotalImpact => totalImpact;
    public IReadOnlyDictionary<string, float> OffThreadMetrics => offThreadMetrics.AsReadOnly();
    public float OffThreadTotalImpact => offThreadTotalImpact;

    public IReadOnlyList<StartupImpactSessionModData> Mods => mods.AsReadOnly();

    public int? ModsLoaded => modsLoaded;
    public int? DefsParsed => defsParsed;
    public int? PatchOperationsApplied => patchOperationsApplied;

    /// <summary>
    /// When this session was captured. Sessions saved before history existed
    /// have no timestamp and report <see cref="DateTime.MinValue"/>.
    /// </summary>
    public DateTime SavedAtUtc =>
        savedAtUtcTicks == 0 ? DateTime.MinValue : new DateTime(savedAtUtcTicks, DateTimeKind.Utc);

    /// <summary>
    /// Hash of the mod list this session ran under, from the same
    /// <see cref="StableListHasher"/> the loading window compares against, so two
    /// sessions can be told apart as comparable or not.
    /// </summary>
    public int ModListHash => modListHash;

    internal static StartupImpactSessionData FromCurrentSession()
    {
        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        StartupImpactSessionData startupImpactSessionData = new()
        {
            loadingTime = startupImpact.TotalLoadingTime,
            metrics = startupImpact.BaseGameProfiler.Metrics.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
            ),
            totalImpact = startupImpact.BaseGameProfiler.TotalImpact,
            offThreadMetrics = startupImpact.BaseGameProfiler.OffThreadMetrics.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value
            ),
            offThreadTotalImpact = startupImpact.BaseGameProfiler.OffThreadTotalImpact,
            mods =
            [
                .. startupImpact.Modlist.ModsInImpactOrder.Select(
                    StartupImpactSessionModData.FromModInfo
                ),
            ],
            modsLoaded = LoadingSessionStats.ModsLoaded,
            defsParsed = LoadingSessionStats.DefsParsed,
            patchOperationsApplied = LoadingSessionStats.PatchOperationsApplied,
            savedAtUtcTicks = startupImpact.SessionCapturedAtUtc.Ticks,
            modListHash = CurrentModListHash(),
        };

        return startupImpactSessionData;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref loadingTime, "loadingTime");
        Scribe_Collections.Look(
            ref metrics,
            "metrics",
            LookMode.Value,
            LookMode.Value,
            ref metricsKeysWorkingList,
            ref metricsValuesWorkingList
        );
        Scribe_Values.Look(ref totalImpact, "totalImpact");
        Scribe_Collections.Look(
            ref offThreadMetrics,
            "offThreadMetrics",
            LookMode.Value,
            LookMode.Value,
            ref offThreadMetricsKeysWorkingList,
            ref offThreadMetricsValuesWorkingList
        );
        Scribe_Values.Look(ref offThreadTotalImpact, "offThreadTotalImpact");

        Scribe_Collections.Look(ref mods, "mods", LookMode.Deep);

        Scribe_Values.Look(ref modsLoaded, "modsLoaded");
        Scribe_Values.Look(ref defsParsed, "defsParsed");
        Scribe_Values.Look(ref patchOperationsApplied, "patchOperationsApplied");
        Scribe_Values.Look(ref savedAtUtcTicks, "savedAtUtcTicks");
        Scribe_Values.Look(ref modListHash, "modListHash");
    }

    /// <summary>
    /// The same hash the loading window computes for the running mod list.
    /// </summary>
    internal static int CurrentModListHash() =>
        StableListHasher.ComputeListHash(
            LoadedModManager.RunningModsListForReading.Select(mod => mod.PackageId)
        );

    // These are used by Scribe_Collections.Look
    private List<string>? metricsKeysWorkingList;
    private List<float>? metricsValuesWorkingList;
    private List<string>? offThreadMetricsKeysWorkingList;
    private List<float>? offThreadMetricsValuesWorkingList;

    internal void OverrideLoadingTime(float loadingTime)
    {
        LoadingProgressMod.Warning(
            $"Overriding loading time for session data; was {this.loadingTime}, now {loadingTime}"
        );
        this.loadingTime = loadingTime;
    }
}
