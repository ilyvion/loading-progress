using System.Collections.Concurrent;

namespace ilyvion.LoadingProgress.StartupImpact;

internal sealed class Profiler(string measurementTarget) : IDisposable
{
    private readonly ThreadLocal<SingleThreadedProfiler> _threadLocalProfiler = new(
        () => new ProfilerStopwatch(measurementTarget));

    public ConcurrentDictionary<string, float> Metrics { get; } = [];
    public float TotalImpact
    {
        get; private set;
    }

    public ConcurrentDictionary<string, float> OffThreadMetrics { get; } = [];
    public float OffThreadTotalImpact
    {
        get; private set;
    }

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

        var ms = _threadLocalProfiler.Value.Stop(category, out var actualCategory);

        if (LoadingProgressMod.instance.StartupImpact.IsActiveThread())
        {
            TotalImpact += ms;

            _ = Metrics.TryGetValue(actualCategory, out var total);
            total += ms;
            Metrics[actualCategory] = total;
        }
        else
        {
            OffThreadTotalImpact += ms;

            _ = OffThreadMetrics.TryGetValue(actualCategory, out var total);
            total += ms;
            OffThreadMetrics[actualCategory] = total;
        }

        return ms;
    }

    public void Dispose()
    {
        _threadLocalProfiler.Dispose();
        GC.SuppressFinalize(this);
    }
}
