using System.Collections.Concurrent;
using System.Threading;

namespace ilyvion.LoadingProgress.StartupImpact;

public class Profiler(string measurementTarget)
{
    private readonly ThreadLocal<SingleThreadedProfiler> _threadLocalProfiler = new(() => new ProfilerStopwatch(measurementTarget));

    public ConcurrentDictionary<string, float> Metrics { get; } = [];
    public float TotalImpact { get; private set; } = 0;

    public ConcurrentDictionary<string, float> OffThreadMetrics { get; } = [];
    public float OffThreadTotalImpact { get; private set; } = 0;

    public void Start(string category)
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            return;
        }

        _threadLocalProfiler.Value.Start(category);
    }

    public float Stop(string category)
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            return 0f;
        }

        float ms = _threadLocalProfiler.Value.Stop(category, out string actualCategory);

        if (LoadingProgressMod.instance.StartupImpact.IsActiveThread())
        {
            TotalImpact += ms;

            _ = Metrics.TryGetValue(actualCategory, out float total);
            total += ms;
            Metrics[actualCategory] = total;
        }
        else
        {
            OffThreadTotalImpact += ms;

            _ = OffThreadMetrics.TryGetValue(actualCategory, out float total);
            total += ms;
            OffThreadMetrics[actualCategory] = total;
        }

        return ms;
    }
}
