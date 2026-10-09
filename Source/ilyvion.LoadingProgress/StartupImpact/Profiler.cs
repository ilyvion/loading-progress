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

    // The timers with a category open on this thread, innermost last. Only the innermost one's
    // clock runs: a category started on another timer pauses the one below it, as a category
    // started on the same timer does, so a stretch is counted once, under the category that
    // ran it, whichever timer that is on. A mod's TryRegister inside the base game's
    // ParseAndProcessXML is then the mod's time and not the base game's as well.
    [ThreadStatic]
    private static List<Profiler>? _openOnThread;

    /// <summary>
    /// Starts timing <paramref name="category"/> on this thread. When this throws, the
    /// category was not opened, so a caller that stops only what it started has nothing to
    /// stop, and a category this start paused on another timer runs on.
    /// </summary>
    public void Start(string category)
    {
        if (
            !LoadingProgressMod.Settings.TrackStartupLoadingImpact || string.IsNullOrEmpty(category)
        )
        {
            return;
        }

        var open = _openOnThread ??= [];
        var below = open.Count > 0 && !ReferenceEquals(open[^1], this) ? open[^1] : null;
        try
        {
            // A category open on this thread pauses while this one runs, and what it ran until
            // now is its own: one on another timer, then one on this timer. Both are recorded
            // before this one is opened, which is the last step.
            below?.PauseOpen();
            var profiler = _threadLocalProfiler.Value;
            var ms = profiler.Interrupt(out var interrupted);
            if (interrupted != null)
            {
                Record(interrupted, ms);
            }
            profiler.Push(category);
        }
        catch
        {
            below?._threadLocalProfiler.Value.Resume();
            throw;
        }
        open.Add(this);
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
        try
        {
            Record(actualCategory, ms);
            Discount(actualCategory, discountMs);
        }
        finally
        {
            Release();
        }
        return ms;
    }

    // Records what the open category on this thread ran until now and stops its clock, for a
    // category on another timer starting inside it.
    private void PauseOpen()
    {
        var ms = _threadLocalProfiler.Value.Pause(out var paused);
        if (paused != null)
        {
            Record(paused, ms);
        }
    }

    // After a stop: this timer comes off the thread's list, and when the innermost open
    // category is now another timer's, that one's clock runs again, and this one's, restarted
    // for its own outer category, waits under it.
    private void Release()
    {
        if (_openOnThread is not { } open)
        {
            return;
        }

        var index = open.LastIndexOf(this);
        if (index >= 0)
        {
            open.RemoveAt(index);
        }
        if (open.Count > 0 && !ReferenceEquals(open[^1], this))
        {
            _ = _threadLocalProfiler.Value.Pause(out _);
            open[^1]._threadLocalProfiler.Value.Resume();
        }
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
        // A timer disposed with a category still open would otherwise stay on this thread's
        // list, and the next start on another timer would pause it.
        _ = _openOnThread?.RemoveAll(open => ReferenceEquals(open, this));
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
