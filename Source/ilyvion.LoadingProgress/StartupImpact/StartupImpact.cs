using System.Diagnostics;

namespace ilyvion.LoadingProgress.StartupImpact;

internal sealed class StartupImpact
{
    private int _activeThreadId;
    private readonly ProfilerStopwatch _loadingProfiler;
    private readonly Stopwatch _clock = new();

    public ModInfoList Modlist { get; } = new();

    /// <summary>
    /// The total loading time, set only after FinishLoading() is called.
    /// </summary>
    public float TotalLoadingTime { get; private set; }
    public Profiler BaseGameProfiler { get; }

    /// <summary>
    /// Milliseconds since tracking began, on the clock the stage ledger and the time to the
    /// main menu are read from.
    /// </summary>
    internal float ElapsedMs => (float)_clock.Elapsed.TotalMilliseconds;

    /// <summary>
    /// Per loading stage, how long it ran against how much of it a category accounted for.
    /// </summary>
    public StageLedger StageLedger { get; } = new(LoadingStage.Initializing.ToString());

    /// <summary>
    /// Whether FinishLoading has run, i.e. the loading time has been taken.
    /// </summary>
    public bool LoadingTimeMeasured { get; private set; }

    /// <summary>
    /// Milliseconds from the start of tracking to the frame the main menu was usable, or 0
    /// until that frame comes. Later than <see cref="TotalLoadingTime"/> by however long the
    /// interface's initialization and other mods' post-load events took, less any time the
    /// game sat paused in the background in between.
    /// </summary>
    public float TimeToMenu { get; private set; }

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
    private bool _previousUnfinishedBootRecorded;
    private readonly EventThreadWait _endOfStartupSave = new();

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

        // The two clocks start together, so a time read off one can be set against the other:
        // the time to the menu against the loading time, the stages against both.
        _clock.Start();
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

    /// <summary>
    /// Notes a stage change in the ledger.
    /// </summary>
    internal void NotifyStage(LoadingStage stage)
    {
        if (WasTrackingEnabledAtStartup)
        {
            StageLedger.Begin(stage.ToString(), ElapsedMs);
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> as a stage of its own in the ledger, named
    /// <paramref name="stage"/>, between two parts of the stage that was running.
    /// </summary>
    internal void RunAsStage(string stage, Action work)
    {
        if (WasTrackingEnabledAtStartup)
        {
            StageLedger.RunAsStage(stage, () => ElapsedMs, work);
        }
        else
        {
            work();
        }
    }

    public void FinishLoading()
    {
        if (!LoadingTimeMeasured)
        {
            LoadingTimeMeasured = true;
            _ = _loadingProfiler.Stop("loading");
            TotalLoadingTime = _loadingProfiler.Total;
            StageLedger.Close(ElapsedMs);
            _sessionCapturedAtUtc = DateTime.UtcNow;

            LoadingProgressMod.instance.harmony.UnpatchCategory(
                Assembly.GetExecutingAssembly(),
                "StartupImpact"
            );

            // FinishLoading runs inside the interface's own long event, so the previous
            // startup's record waits until that event has finished, when Scribe is free. It
            // does not wait for this startup to end: one that stopped on the way would lose it.
            // The engine runs that queue only after an event that returns normally, so the
            // end of the startup makes the record too, if this has not run by then.
            if (PreviousUnfinishedBoot is not null)
            {
                LongEventHandler.ExecuteWhenFinished(RecordPreviousUnfinishedBoot);
            }
        }
    }

    // Records the previous unfinished startup once, from whichever asks first.
    private void RecordPreviousUnfinishedBoot()
    {
        if (_previousUnfinishedBootRecorded || PreviousUnfinishedBoot is not { } unfinished)
        {
            return;
        }
        _previousUnfinishedBootRecorded = true;

        try
        {
            Dialog.StartupImpactSessionStorage.RecordUnfinishedBoot(unfinished);
        }
        catch (Exception e)
        {
            LoadingProgressMod.Error("Failed to record an unfinished boot: " + e);
        }
    }

    /// <summary>
    /// Takes the time to the main menu: <paramref name="loadingMs"/>, the clock less the time
    /// the game sat paused in the background on the way, the same reading the loading window
    /// records, never less than the loading time.
    /// </summary>
    internal void MarkMenuReached(float loadingMs)
    {
        if (LoadingTimeMeasured)
        {
            TimeToMenu = Math.Max(TotalLoadingTime, loadingMs);
        }
    }

    /// <summary>
    /// Ends the startup, after loading has: saves its session, once, when saving it
    /// automatically is on, then removes the marker.
    /// </summary>
    /// <remarks>
    /// Saving uses Scribe. An asynchronous long event whose thread is still running may be
    /// using it, as loading a save does, so the saving then waits until that thread has
    /// stopped, checked each frame by <see cref="UpdateEndOfStartup"/>. The marker stays until
    /// then, so a game that stops first leaves it, and the next startup records this one as a
    /// startup that never finished.
    /// </remarks>
    internal void FinishStartup()
    {
        if (!LoadingTimeMeasured)
        {
            return;
        }

        _endOfStartupSave.RunWhenStopped(SaveAtStartupEnd, LongEventHandler.eventThread);
    }

    /// <summary>
    /// Runs the end of the startup's saving if it is waiting for a long event's thread and
    /// that thread has stopped. Called on the main thread every frame.
    /// </summary>
    internal void UpdateEndOfStartup() => _endOfStartupSave.Update(LongEventHandler.eventThread);

    // This startup's session, when saving it automatically is on; then the marker, since the
    // boot it describes is over and recorded. The previous unfinished startup is recorded
    // first, if the interface's event threw and never ran the action FinishLoading queued: its
    // marker is already gone, so nothing could record it later.
    private void SaveAtStartupEnd()
    {
        RecordPreviousUnfinishedBoot();

        if (WasTrackingEnabledAtStartup && LoadingProgressMod.Settings.AutoSaveStartupImpactReport)
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
        }

        StartupImpactCrashMarker.Clear();
    }

    public void UpdateActiveThreadId() => _activeThreadId = Environment.CurrentManagedThreadId;

    public bool IsActiveThread() => Environment.CurrentManagedThreadId == _activeThreadId;
}
