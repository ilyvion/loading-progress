namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// Where the time no category accounts for went, stage by stage.
/// </summary>
/// <remarks>
/// Every loading stage runs from its start to the next stage's start on the tracking clock,
/// and every category stopped on the active thread credits the stretch it timed to the stages
/// that stretch overlapped. What is left of a stage is the time it spent in nothing Startup
/// Impact names, which is where a regression that shows in no breakdown can hide.
/// </remarks>
internal sealed class StageLedger
{
    private readonly List<StageLedgerEntry> _entries = [];

    private readonly object _lock = new();
    private bool _closed;

    public StageLedger(string initialStage)
    {
        _entries.Add(new StageLedgerEntry(initialStage, 0f));
    }

    public IReadOnlyList<StageLedgerEntry> Entries
    {
        get
        {
            lock (_lock)
            {
                return [.. _entries];
            }
        }
    }

    /// <summary>
    /// Starts a stage at <paramref name="nowMs"/>, ending the one before it there. Beginning
    /// the stage that is already running changes nothing.
    /// </summary>
    public void Begin(string stage, float nowMs)
    {
        lock (_lock)
        {
            if (_closed || string.Equals(_entries[^1].Stage, stage, StringComparison.Ordinal))
            {
                return;
            }

            EndCurrent(nowMs);
            _entries.Add(new StageLedgerEntry(stage, nowMs));
        }
    }

    /// <summary>
    /// Runs <paramref name="work"/> as <paramref name="stage"/>, reading the time from
    /// <paramref name="nowMs"/>, then begins again the stage that was running before it.
    /// </summary>
    public void RunAsStage(string stage, Func<float> nowMs, Action work)
    {
        string resumed;
        lock (_lock)
        {
            resumed = _entries[^1].Stage;
        }

        Begin(stage, nowMs());
        try
        {
            work();
        }
        finally
        {
            Begin(resumed, nowMs());
        }
    }

    /// <summary>
    /// Credits <paramref name="ms"/> timed milliseconds that ended at <paramref name="stopMs"/>
    /// to the stages they ran in, each the part that overlapped it.
    /// </summary>
    /// <remarks>
    /// A category that starts in one stage and stops in the next would otherwise credit all of
    /// its time to the later stage: the earlier one would look unaccounted for, and the later
    /// one's excess would be lost to the floor at zero. No two stretches overlap, since only the
    /// innermost open category on a thread runs and only the active thread is attributed.
    /// </remarks>
    public void Attribute(float ms, float stopMs)
    {
        lock (_lock)
        {
            if (_closed || ms <= 0f)
            {
                return;
            }

            var startMs = stopMs - ms;
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                var entryEndMs = entry.EndedMs < 0f ? stopMs : Math.Min(entry.EndedMs, stopMs);
                var overlap = entryEndMs - Math.Max(entry.StartedMs, startMs);
                if (overlap > 0f)
                {
                    entry.AttributedMs += overlap;
                }
                if (entry.StartedMs <= startMs)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Ends the last stage at <paramref name="nowMs"/>. Nothing is recorded after this.
    /// </summary>
    public void Close(float nowMs)
    {
        lock (_lock)
        {
            if (_closed)
            {
                return;
            }

            EndCurrent(nowMs);
            _closed = true;
        }
    }

    private void EndCurrent(float nowMs)
    {
        var current = _entries[^1];
        if (current.EndedMs < 0f)
        {
            current.EndedMs = Math.Max(nowMs, current.StartedMs);
        }
    }
}

internal sealed class StageLedgerEntry(string stage, float startedMs)
{
    public string Stage { get; } = stage;
    public float StartedMs { get; } = startedMs;
    public float EndedMs { get; set; } = -1f;
    public float AttributedMs { get; set; }

    public float WallMs => EndedMs < 0f ? 0f : EndedMs - StartedMs;
    public float RemainingMs => Math.Max(0f, WallMs - AttributedMs);
}
