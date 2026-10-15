using System.Collections.Concurrent;

namespace ilyvion.LoadingProgress.StartupImpact;

internal sealed class Profiler(string measurementTarget) : IDisposable
{
    private readonly ThreadLocal<SingleThreadedProfiler> _threadLocalProfiler = new(() =>
        new ProfilerStopwatch(measurementTarget)
    );

    private SingleThreadedProfiler ThreadProfiler =>
        _threadLocalProfiler.Value
        ?? throw new InvalidOperationException("The thread's profiler was not created.");

    public ConcurrentDictionary<string, float> Metrics { get; } = [];
    public float TotalImpact { get; private set; }

    public ConcurrentDictionary<string, float> OffThreadMetrics { get; } = [];

    private float _offThreadTotalImpact;
    public float OffThreadTotalImpact => _offThreadTotalImpact;

    // The categories open on this thread, with their timers, innermost last. Only the innermost one's
    // clock runs: a category started on another timer pauses the one below it, as a category
    // started on the same timer does, so a stretch is counted once, under the category that
    // ran it, whichever timer that is on. A mod's TryRegister inside the base game's
    // ParseAndProcessXML is then the mod's time and not the base game's as well.
    [ThreadStatic]
    private static List<OpenCategory>? _openOnThread;

    // How many of the categories in _openOnThread are unguarded.
    [ThreadStatic]
    private static int _unguardedOnThread;

    [ThreadStatic]
    private static int _busy;

    // A category open on this thread, on the timer it was started on. An unguarded one has no
    // finally block to stop it, so code that threw past its stop leaves it open.
    private readonly record struct OpenCategory(Profiler Profiler, string Category, bool Unguarded);

    /// <summary>
    /// Whether a timer is starting, stopping or discounting a category on this thread. A
    /// mod's hook on a method that work calls, such as <see cref="object.Equals(object)"/>,
    /// runs inside it, and must not be timed there.
    /// </summary>
    internal static bool BusyOnThread => _busy > 0;

    /// <summary>
    /// Starts timing <paramref name="category"/> on this thread. When this throws, the
    /// category was not opened, so a caller that stops only what it started has nothing to
    /// stop, and a category this start paused on another timer runs on.
    /// </summary>
    public void Start(string category) => Start(category, unguarded: false);

    /// <summary>
    /// Starts timing <paramref name="category"/> on this thread for code with no finally block
    /// to stop it. When that code throws past its <see cref="StopUnguarded"/>, the next stop of
    /// a category open below it on this thread stops it first, so its time runs on until then.
    /// Throws as <see cref="Start(string)"/> does.
    /// </summary>
    public void StartUnguarded(string category) => Start(category, unguarded: true);

    private void Start(string category, bool unguarded)
    {
        if (
            !LoadingProgressMod.Settings.TrackStartupLoadingImpact || string.IsNullOrEmpty(category)
        )
        {
            return;
        }

        _busy++;
        try
        {
            StartBusy(category, unguarded);
        }
        finally
        {
            _busy--;
        }
    }

    private void StartBusy(string category, bool unguarded)
    {
        var open = _openOnThread ??= [];
        var below =
            open.Count > 0 && !ReferenceEquals(open[^1].Profiler, this) ? open[^1].Profiler : null;
        try
        {
            // A category open on this thread pauses while this one runs, and what it ran until
            // now is its own: one on another timer, then one on this timer. Both are recorded
            // before this one is opened, which is the last step.
            below?.PauseOpen();
            var profiler = ThreadProfiler;
            var ms = profiler.Interrupt(out var interrupted);
            if (interrupted != null)
            {
                Record(interrupted, ms);
            }
            profiler.Push(category);
        }
        catch
        {
            below?.ThreadProfiler.Resume();
            throw;
        }
        open.Add(new OpenCategory(this, category, unguarded));
        if (unguarded)
        {
            _unguardedOnThread++;
        }
    }

    public float Stop(string category) => Stop(category, 0f);

    /// <summary>
    /// Stops <paramref name="category"/> and takes <paramref name="discountMs"/> back off the
    /// category the stop recorded into: see <see cref="Discount"/>. That is the one on top of
    /// the stack, which a stop naming another, logged as a mismatch, records into instead.
    /// Unguarded categories still open above it on this thread are stopped first.
    /// </summary>
    public float Stop(string category, float discountMs)
    {
        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            return 0f;
        }

        _busy++;
        try
        {
            StopAbandoned();
            return StopOpen(category, discountMs);
        }
        finally
        {
            _busy--;
        }
    }

    /// <summary>
    /// Stops <paramref name="category"/>, started with <see cref="StartUnguarded"/> on this
    /// thread, after the unguarded categories still open above it. With a
    /// <paramref name="replacer"/>, it was code that ran in place of the code it was called
    /// from, and what it ran is recorded under the category open below it on this thread, on
    /// whichever timer that is, marked as replaced by <paramref name="replacer"/>; under
    /// <paramref name="category"/> itself when nothing is open below it. Does nothing when
    /// <paramref name="category"/> is not open unguarded on this thread.
    /// </summary>
    public void StopUnguarded(string category, string? replacer)
    {
        if (_unguardedOnThread == 0 || _openOnThread is not { } open)
        {
            return;
        }

        var index = open.Count - 1;
        while (
            index >= 0
            && open[index].Unguarded
            && !(ReferenceEquals(open[index].Profiler, this) && open[index].Category == category)
        )
        {
            index--;
        }
        if (index < 0 || !open[index].Unguarded)
        {
            return;
        }

        _busy++;
        try
        {
            while (open.Count - 1 > index)
            {
                var top = open[^1];
                _ = top.Profiler.StopOpen(top.Category, 0f);
            }

            if (replacer == null)
            {
                _ = StopOpen(category, 0f);
                return;
            }

            var ms = ThreadProfiler.Stop(category, out var actualCategory);
            Release();
            if (open.Count > 0 && open[^1].Profiler.ThreadProfiler.Open is { } below)
            {
                open[^1].Profiler.Record(StartupImpactProfilerUtil.ReplacedBy(below, replacer), ms);
            }
            else
            {
                Record(actualCategory, ms);
            }
        }
        finally
        {
            _busy--;
        }
    }

    // Stops the unguarded categories on top of this thread's open ones, which code that threw
    // past their stops left open.
    private static void StopAbandoned()
    {
        while (_unguardedOnThread > 0 && _openOnThread is [.., { Unguarded: true } top])
        {
            _ = top.Profiler.StopOpen(top.Category, 0f);
        }
    }

    private float StopOpen(string category, float discountMs)
    {
        var ms = ThreadProfiler.Stop(category, out var actualCategory);
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
        var ms = ThreadProfiler.Pause(out var paused);
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

        for (var index = open.Count - 1; index >= 0; index--)
        {
            if (ReferenceEquals(open[index].Profiler, this))
            {
                if (open[index].Unguarded)
                {
                    _unguardedOnThread--;
                }
                open.RemoveAt(index);
                break;
            }
        }
        if (open.Count > 0 && !ReferenceEquals(open[^1].Profiler, this))
        {
            _ = ThreadProfiler.Pause(out _);
            open[^1].Profiler.ThreadProfiler.Resume();
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
        _busy++;
        try
        {
            if (ms <= 0f || !Metrics.TryGetValue(category, out var total))
            {
                return;
            }

            var taken = Math.Min(ms, total);
            Metrics[category] = total - taken;
            TotalImpact -= taken;
        }
        finally
        {
            _busy--;
        }
    }

    public void Dispose()
    {
        // A timer disposed with a category still open would otherwise stay on this thread's
        // list, and the next start on another timer would pause it.
        if (_openOnThread is { } open)
        {
            _unguardedOnThread -= open.Count(o => ReferenceEquals(o.Profiler, this) && o.Unguarded);
            _ = open.RemoveAll(o => ReferenceEquals(o.Profiler, this));
        }
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
