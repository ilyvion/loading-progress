namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// Where the time no category accounts for went, stage by stage.
/// </summary>
/// <remarks>
/// Every loading stage runs from its start to the next stage's start on the tracking clock,
/// and every category stopped on the active thread credits the stretch it timed to the stages
/// that stretch overlapped. A stretch two categories both cover is credited once. What is
/// left of a stage is the time it spent in nothing Startup Impact names, which is where a
/// regression that shows in no breakdown can hide.
/// </remarks>
internal sealed class StageLedger
{
    private readonly List<StageLedgerEntry> _entries = [];

    // The stretches already credited, in order, none overlapping. Every stretch ends at the
    // clock's reading when its category stopped, so the ones a new stretch overlaps are the
    // last ones here.
    private readonly List<(float StartMs, float EndMs)> _credited = [];

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
    /// to the stages they ran in, each the part that overlapped it. A part an earlier stretch
    /// already credited is not credited again.
    /// </summary>
    /// <remarks>
    /// A category that starts in one stage and stops in the next would otherwise credit all of
    /// its time to the later stage: the earlier one would look unaccounted for, and the later
    /// one's excess would be lost to the floor at zero. Categories on different timers can
    /// cover the same stretch, as a mod's <c>TryRegister</c> does inside the base game's
    /// <c>ParseAndProcessXML</c>, and crediting it twice would hide a stage's untimed time in
    /// the same way.
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
            var mergedStartMs = startMs;
            var uncoveredEndMs = stopMs;
            while (_credited.Count > 0 && _credited[^1].EndMs > startMs)
            {
                var (creditedStart, creditedEnd) = _credited[^1];
                _credited.RemoveAt(_credited.Count - 1);
                if (creditedEnd < uncoveredEndMs)
                {
                    Credit(creditedEnd, uncoveredEndMs);
                }
                uncoveredEndMs = Math.Max(creditedStart, startMs);
                mergedStartMs = Math.Min(mergedStartMs, creditedStart);
            }
            if (startMs < uncoveredEndMs)
            {
                Credit(startMs, uncoveredEndMs);
            }
            _credited.Add((mergedStartMs, stopMs));
        }
    }

    // Credits the stretch from startMs to endMs to the stages it overlaps.
    private void Credit(float startMs, float endMs)
    {
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            var entryEndMs = entry.EndedMs < 0f ? endMs : Math.Min(entry.EndedMs, endMs);
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
            _credited.Clear();
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
