using System.Diagnostics;

namespace ilyvion.LoadingProgress.StartupImpact;

internal sealed class ProfilerStopwatch(string what) : SingleThreadedProfiler(what)
{
    private readonly Stopwatch _stopwatch = new();

    public override void Start() => _stopwatch.Start();

    public override float Stop()
    {
        _stopwatch.Stop();
        var ms = (float)_stopwatch.Elapsed.TotalMilliseconds;
        _stopwatch.Reset();
        return ms;
    }
}
