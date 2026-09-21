namespace ilyvion.LoadingProgress.StartupImpact;

internal sealed class StartupImpact
{
    private int _activeThreadId;
    private readonly ProfilerStopwatch _loadingProfiler;

    public ModInfoList Modlist { get; } = new();

    /// <summary>
    /// The total loading time, set only after FinishLoading() is called.
    /// </summary>
    public float TotalLoadingTime { get; private set; }
    public Profiler BaseGameProfiler { get; }

    /// <summary>
    /// Whether TrackStartupLoadingImpact was on when the mod was constructed, i.e. for the
    /// whole duration of this startup. Unlike LoadingProgressMod.Settings.TrackStartupLoadingImpact,
    /// this doesn't change if the user flips the setting later in the same session.
    /// </summary>
    public bool WasTrackingEnabledAtStartup { get; }

    /// <summary>
    /// A boot that was still in progress when the game last stopped, recovered
    /// from the marker it left behind. Written into the history at the end of
    /// this load, when Scribe is safe to use.
    /// </summary>
    internal StartupImpactCrashMarker.UnfinishedBoot? PreviousUnfinishedBoot { get; }

    private DateTime? _sessionCapturedAtUtc;

    /// <summary>
    /// When this startup's session was captured, fixed for the life of the
    /// process so every session taken from this boot carries one timestamp.
    /// </summary>
    /// <remarks>
    /// Set when loading finishes, the point the figures stop changing. It falls
    /// back to first use because a session can still be captured when tracking
    /// was off and FinishLoading never ran, and handing out a fresh time on
    /// every read would make one run look like several: the history identifies
    /// a session by when it was captured, and the picker lists that as when the
    /// run happened.
    /// </remarks>
    internal DateTime SessionCapturedAtUtc => _sessionCapturedAtUtc ??= DateTime.UtcNow;

    public StartupImpact()
    {
        _activeThreadId = Environment.CurrentManagedThreadId;

        WasTrackingEnabledAtStartup = LoadingProgressMod.Settings.TrackStartupLoadingImpact;

        BaseGameProfiler = new Profiler("base game");
        _loadingProfiler = new ProfilerStopwatch("loading");

        if (WasTrackingEnabledAtStartup)
        {
            _loadingProfiler.Start("loading");
        }

        // Plain file IO, which is safe this early; the marker has to be read before Begin
        // overwrites it. Nothing touches Scribe here, because mod constructors run while the
        // loader is still using it. Not tied to auto-save: a boot that never finishes cannot
        // be saved by hand after the fact, so the checkbox that asks for these is the only
        // thing that can gate it.
        if (WasTrackingEnabledAtStartup && LoadingProgressMod.Settings.KeepUnfinishedBoots)
        {
            PreviousUnfinishedBoot = StartupImpactCrashMarker.TakePrevious();
            StartupImpactCrashMarker.Begin(Dialog.StartupImpactSessionData.CurrentModListHash());
        }
    }

    private bool _loadingTimeMeasured;

    public void FinishLoading()
    {
        if (!_loadingTimeMeasured)
        {
            _loadingTimeMeasured = true;
            _ = _loadingProfiler.Stop("loading");
            TotalLoadingTime = _loadingProfiler.Total;
            _sessionCapturedAtUtc = DateTime.UtcNow;

            // This boot finished, so the marker no longer describes anything.
            StartupImpactCrashMarker.Clear();

            LoadingProgressMod.instance.harmony.UnpatchCategory(
                Assembly.GetExecutingAssembly(),
                "StartupImpact"
            );

            // FinishLoading can run off the main thread, and Scribe.saver is global state also
            // used from the main thread; defer both of these.
            if (PreviousUnfinishedBoot is not null)
            {
                LongEventHandler.ExecuteWhenFinished(static () =>
                {
                    try
                    {
                        if (
                            LoadingProgressMod.instance.StartupImpact.PreviousUnfinishedBoot is
                            { } unfinished
                        )
                        {
                            Dialog.StartupImpactSessionStorage.RecordUnfinishedBoot(unfinished);
                        }
                    }
                    catch (Exception e)
                    {
                        LoadingProgressMod.Error("Failed to record an unfinished boot: " + e);
                    }
                });
            }

            if (
                WasTrackingEnabledAtStartup
                && LoadingProgressMod.Settings.AutoSaveStartupImpactReport
            )
            {
                LongEventHandler.ExecuteWhenFinished(static () =>
                {
                    try
                    {
                        Dialog.StartupImpactSessionStorage.SaveAndRecord(
                            Dialog.StartupImpactSessionData.FromCurrentSession()
                        );
                    }
                    catch (Exception e)
                    {
                        LoadingProgressMod.Error("Failed to auto-save startup impact report: " + e);
                    }
                });
            }
        }
    }

    public void UpdateActiveThreadId() => _activeThreadId = Environment.CurrentManagedThreadId;

    public bool IsActiveThread() => Environment.CurrentManagedThreadId == _activeThreadId;
}
