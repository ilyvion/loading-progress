namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

internal sealed class StartupImpactSessionModViewData(StartupImpactSessionModData modData)
{
    private readonly List<float> metrics = [];
    private readonly List<float> offThreadMetrics = [];

    public StartupImpactSessionModData ModData { get; } = modData;

    public bool HideInUi
    {
        get; set;
    }

    public IReadOnlyList<float> Metrics => metrics.AsReadOnly();
    public IReadOnlyList<float> OffThreadMetrics => offThreadMetrics.AsReadOnly();

    internal void Initialize(StartupImpactSessionViewData sessionViewData)
    {
        metrics.Clear();
        foreach (var k in sessionViewData.Categories)
        {
            metrics.Add(ModData.Metrics.TryGetValue(k, out var v) ? v : default!);
        }

        offThreadMetrics.Clear();
        foreach (var k in sessionViewData.Categories)
        {
            offThreadMetrics.Add(ModData.OffThreadMetrics.TryGetValue(k, out var v) ? v : default!);
        }
    }
}
