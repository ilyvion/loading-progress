namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

internal class StartupImpactSessionModData : IExposable
{
#pragma warning disable IDE0032 // Use auto property
    private string modName = "";
#pragma warning restore IDE0032 // Use auto property
    private Dictionary<string, float> metrics = [];
    private float totalImpact;
    private Dictionary<string, float> offThreadMetrics = [];
    private float offThreadTotalImpact;

    public string ModName => modName;
    public IReadOnlyDictionary<string, float> Metrics => metrics.AsReadOnly();
    public float TotalImpact => totalImpact;
    public IReadOnlyDictionary<string, float> OffThreadMetrics => offThreadMetrics;
    public float OffThreadTotalImpact => offThreadTotalImpact;

    public static StartupImpactSessionModData FromModInfo(ModInfo info)
    {
        return new StartupImpactSessionModData
        {
            modName = info.Mod.Name ?? string.Empty,
            metrics = new Dictionary<string, float>(info.Profiler.Metrics),
            totalImpact = info.Profiler.TotalImpact,
            offThreadMetrics = new Dictionary<string, float>(info.Profiler.OffThreadMetrics),
            offThreadTotalImpact = info.Profiler.OffThreadTotalImpact,
        };
    }
    public void ExposeData()
    {
        Scribe_Values.Look(ref modName!, "modName");
        Scribe_Collections.Look(ref metrics, "metrics", LookMode.Value, LookMode.Value, ref metricsKeysWorkingList, ref metricsValuesWorkingList);
        Scribe_Values.Look(ref totalImpact, "totalImpact");
        Scribe_Collections.Look(ref offThreadMetrics, "offThreadMetrics", LookMode.Value, LookMode.Value, ref offThreadKeysWorkingList, ref offThreadValuesWorkingList);
        Scribe_Values.Look(ref offThreadTotalImpact, "offThreadTotalImpact");
    }

    // These are used by Scribe_Collections.Look
    private List<string>? metricsKeysWorkingList;
    private List<float>? metricsValuesWorkingList;
    private List<string>? offThreadKeysWorkingList;
    private List<float>? offThreadValuesWorkingList;
}
