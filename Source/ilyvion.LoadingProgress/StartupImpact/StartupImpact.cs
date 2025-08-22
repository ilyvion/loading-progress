namespace ilyvion.LoadingProgress.StartupImpact;

internal class StartupImpact
{
    private int _activeThreadId;
    private readonly ProfilerStopwatch _loadingProfiler;

    public ModInfoList Modlist { get; } = new();
    /// <summary>
    /// The total loading time, set only after FinishLoading() is called.
    /// </summary>
    public float TotalLoadingTime { get; private set; } = 0;
    public Profiler BaseGameProfiler { get; }

    public StartupImpact()
    {
        _activeThreadId = Environment.CurrentManagedThreadId;

        BaseGameProfiler = new Profiler("base game");
        _loadingProfiler = new ProfilerStopwatch("loading");

        if (LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            _loadingProfiler.Start("loading");
        }
    }

    private bool _loadingTimeMeasured = false;
    public void FinishLoading()
    {
        if (!_loadingTimeMeasured)
        {
            _loadingTimeMeasured = true;
            _ = _loadingProfiler.Stop("loading");
            TotalLoadingTime = _loadingProfiler.Total;

            LoadingProgressMod.instance.harmony.UnpatchCategory(Assembly.GetExecutingAssembly(), "StartupImpact");
        }
    }

    public void UpdateActiveThreadId()
    {
        _activeThreadId = Environment.CurrentManagedThreadId;
    }

    public bool IsActiveThread()
    {
        return Environment.CurrentManagedThreadId == _activeThreadId;
    }
}
