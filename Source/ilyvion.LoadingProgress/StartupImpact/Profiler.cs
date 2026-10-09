using System.Collections.Concurrent;

namespace ilyvion.LoadingProgress.StartupImpact;

internal sealed class Profiler(string measurementTarget) : IDisposable
{
    private readonly ThreadLocal<SingleThreadedProfiler> _threadLocalProfiler = new(() =>
        new ProfilerStopwatch(measurementTarget)
    );

    public ConcurrentDictionary<string, float> Metrics { get; } = [];
    public float TotalImpact { get; private set; }

    public ConcurrentDictionary<string, float> OffThreadMetrics { get; } = [];

    private float _offThreadTotalImpact;
    public float OffThreadTotalImpact => _offThreadTotalImpact;

    /// <summary>
    /// Starts timing <paramref name="category"/> on this thread. When this throws, the
    /// category was not opened, so a caller that stops only what it started has nothing to
    /// stop.
    /// </summary>
    public void Start(string category)
    {
        if (
            !LoadingProgressMod.Settings.TrackStartupLoadingImpact || string.IsNullOrEmpty(category)
        )
        {
            return;
        }

        // A category open on this thread pauses while this one runs, and what it ran until now
        // is its own. It is recorded before this one is opened, which is the last step.
        var profiler = _threadLocalProfiler.Value;
        var ms = profiler.Interrupt(out var interrupted);
        if (interrupted != null)
        {
            Record(interrupted, ms);
        }
        profiler.Push(category);
    }

    public float Stop(string category) => Stop(category, 0f);

    /// <summary>
    /// Stops <paramref name="category"/> and takes <paramref name="discountMs"/> back off the
    /// category the stop recorded into: see <see cref="Discount"/>. That is the one on top of
    /// the stack, which a stop naming another, logged as a mismatch, records into instead.
    /// </summary>
    public float Stop(string category, float discountMs)
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            return 0f;
        }

        var ms = _threadLocalProfiler.Value.Stop(category, out var actualCategory);
        Record(actualCategory, ms);
        Discount(actualCategory, discountMs);
        return ms;
    }

    private void Record(string category, float ms)
    {
        if (LoadingProgressMod.instance.StartupImpact.IsActiveThread())
        {
            TotalImpact += ms;

            _ = Metrics.TryGetValue(category, out var total);
            total += ms;
            Metrics[category] = total;

            var startupImpact = LoadingProgressMod.instance.StartupImpact;
            startupImpact.StageLedger.Attribute(ms, startupImpact.ElapsedMs);
        }
        else
        {
            InterlockedAdd(ref _offThreadTotalImpact, ms);

            _ = OffThreadMetrics.AddOrUpdate(category, ms, (_, total) => total + ms);
        }
    }

    /// <summary>
    /// Takes <paramref name="ms"/> back off a category timed on the active thread, for time it
    /// was open that was not the startup's: the game sitting paused in the background.
    /// </summary>
    public void Discount(string category, float ms)
    {
        if (ms <= 0f || !Metrics.TryGetValue(category, out var total))
        {
            return;
        }

        var taken = Math.Min(ms, total);
        Metrics[category] = total - taken;
        TotalImpact -= taken;
    }

    public void Dispose()
    {
        _threadLocalProfiler.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void InterlockedAdd(ref float location, float value)
    {
        float initial;
        float computed;
        do
        {
            initial = location;
            computed = initial + value;
        } while (initial != Interlocked.CompareExchange(ref location, computed, initial));
    }
}
