namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

internal static class StartupImpactSessionStorage
{
    private const string SaveFileName = "StartupImpactData.xml";
    private const string SaveLabel = "sessionData";

    private const string HistoryFolderName = "StartupImpactSessions";
    private const string IndexFileName = "index.xml";
    private const string IndexLabel = "sessionIndex";
    private const string IndexEntriesLabel = "entries";

    internal static string SaveFilePath =>
        Path.Combine(GenFilePaths.SaveDataFolderPath, GenText.SanitizeFilename(SaveFileName));

    internal static string HistoryFolderPath =>
        Path.Combine(GenFilePaths.SaveDataFolderPath, HistoryFolderName);

    internal static string IndexFilePath => Path.Combine(HistoryFolderPath, IndexFileName);

    internal static string SessionFilePath(string id) =>
        Path.Combine(HistoryFolderPath, GenText.SanitizeFilename(id + ".xml"));

    /// <summary>
    /// Writes a session to the shared file, exactly as it always was.
    /// </summary>
    /// <remarks>
    /// The auto-save setting promises this file to external tools such as
    /// RimSort, so its content and timing are unchanged by history. Callers that
    /// want the session kept use <see cref="SaveAndRecord"/> instead.
    /// </remarks>
    internal static void Save(StartupImpactSessionData sessionData)
    {
        Scribe.saver.InitSaving(SaveFilePath, "StartupImpactSession");
        Scribe_Deep.Look(ref sessionData, SaveLabel);
        Scribe.saver.FinalizeSaving();
    }

    /// <summary>
    /// The auto-save path: writes the shared file, then keeps a copy of it in
    /// the history.
    /// </summary>
    internal static void SaveAndRecord(StartupImpactSessionData sessionData)
    {
        Save(sessionData);

        if (LoadingProgressMod.Settings.SessionsToKeep > 0)
        {
            AddToHistory(sessionData);
        }
    }

    /// <summary>
    /// The stored history, newest first. Never null; an unreadable or absent
    /// index reads as an empty history rather than throwing.
    /// </summary>
    internal static List<StartupImpactSessionIndexEntry> LoadIndex()
    {
        List<StartupImpactSessionIndexEntry>? entries = null;
        try
        {
            if (File.Exists(IndexFilePath))
            {
                Scribe.loader.InitLoading(IndexFilePath);
                Scribe_Collections.Look(ref entries, IndexEntriesLabel, LookMode.Deep);
                Scribe.loader.FinalizeLoading();
            }
        }
        catch (Exception e)
        {
            LoadingProgressMod.Error("Could not read the startup impact history index: " + e);
            Scribe.ForceStop();
            entries = null;
        }

        entries ??= [];
        _ = entries.RemoveAll(entry => entry is null || string.IsNullOrEmpty(entry.Id));
        entries.SortByDescending(entry => entry.SavedAtUtc.Ticks);
        return entries;
    }

    internal static void SaveIndex(List<StartupImpactSessionIndexEntry> entries)
    {
        try
        {
            _ = Directory.CreateDirectory(HistoryFolderPath);
            Scribe.saver.InitSaving(IndexFilePath, IndexLabel);
            Scribe_Collections.Look(ref entries, IndexEntriesLabel, LookMode.Deep);
            Scribe.saver.FinalizeSaving();
        }
        catch (Exception e)
        {
            LoadingProgressMod.Error("Could not write the startup impact history index: " + e);
            Scribe.ForceStop();
        }
    }

    /// <summary>
    /// Reads one stored session's full breakdown, or null when its file is gone
    /// or it never had one.
    /// </summary>
    internal static StartupImpactSessionData? LoadSession(string id)
    {
        var path = SessionFilePath(id);
        StartupImpactSessionData? sessionData = null;
        try
        {
            if (File.Exists(path))
            {
                Scribe.loader.InitLoading(path);
                Scribe_Deep.Look(ref sessionData, SaveLabel);
                Scribe.loader.FinalizeLoading();
            }
        }
        catch (Exception e)
        {
            LoadingProgressMod.Error($"Could not read stored session '{id}': " + e);
            Scribe.ForceStop();
            return null;
        }
        return sessionData;
    }

    /// <summary>
    /// Removes a stored session and its file. Returns the remaining history.
    /// </summary>
    internal static List<StartupImpactSessionIndexEntry> Delete(string id)
    {
        var entries = LoadIndex();
        _ = entries.RemoveAll(entry => entry.Id == id);
        DeleteSessionFile(id);
        SaveIndex(entries);
        return entries;
    }

    /// <summary>
    /// Sets one session's pinned and baseline flags. Returns the remaining
    /// history, with the change applied.
    /// </summary>
    /// <remarks>
    /// Read, change, write, exactly as <see cref="Delete"/> does, rather than
    /// writing back the list the caller is holding. The picker loads the index
    /// when it opens and stays open beside the impact window, so a session saved
    /// there in the meantime is already in the file; writing the picker's own
    /// list over it would drop that session and orphan its file.
    /// </remarks>
    internal static List<StartupImpactSessionIndexEntry> SetFlags(
        string id,
        bool pinned,
        bool baseline
    )
    {
        var entries = LoadIndex();
        ApplyFlags(entries, id, pinned, baseline);
        SaveIndex(entries);
        return entries;
    }

    /// <summary>
    /// Applies one session's flags to a history, taking the baseline away from
    /// whichever session held it when this one is claiming the role.
    /// </summary>
    internal static void ApplyFlags(
        List<StartupImpactSessionIndexEntry> entries,
        string id,
        bool pinned,
        bool baseline
    )
    {
        foreach (var entry in entries)
        {
            if (entry.Id == id)
            {
                entry.Pinned = pinned;
                entry.Baseline = baseline;
            }
            else if (baseline)
            {
                entry.Baseline = false;
            }
        }
    }

    /// <summary>
    /// Records a boot that never finished, from the marker it left behind.
    /// </summary>
    internal static void RecordUnfinishedBoot(StartupImpactCrashMarker.UnfinishedBoot boot)
    {
        var entries = LoadIndex();
        var id = NewId(boot.StartedAtUtc);

        entries.Insert(
            0,
            StartupImpactSessionIndexEntry.ForUnfinishedBoot(
                id,
                boot.StartedAtUtc,
                boot.ModListHash,
                boot.ModsLoaded,
                boot.LastStage
            )
        );

        Evict(entries);
        SaveIndex(entries);
    }

    private static void AddToHistory(StartupImpactSessionData sessionData)
    {
        try
        {
            _ = Directory.CreateDirectory(HistoryFolderPath);

            var entries = LoadIndex();
            var savedAt =
                sessionData.SavedAtUtc == DateTime.MinValue
                    ? DateTime.UtcNow
                    : sessionData.SavedAtUtc;
            // Pressing Save twice records the same session twice otherwise, and two rows
            // carrying one run's figures read as two runs. A session is identified by when it
            // was captured, which FromCurrentSession stamps once.
            var already = entries.FindIndex(entry => entry.SavedAtUtc == savedAt);
            if (already >= 0)
            {
                File.Copy(SaveFilePath, SessionFilePath(entries[already].Id), true);
                return;
            }

            var id = NewId(savedAt);

            // Copy the file Save just wrote rather than serialising the same session a second
            // time. On a 229-mod list that is 219 KB through Scribe, and it lands after
            // FinishLoading has stopped the clock -- unmeasured, but still time the player
            // waits at a frozen screen.
            File.Copy(SaveFilePath, SessionFilePath(id), true);

            entries.Insert(0, StartupImpactSessionIndexEntry.ForCompletedSession(id, sessionData));

            Evict(entries);
            SaveIndex(entries);
        }
        catch (Exception e)
        {
            LoadingProgressMod.Error(
                "Could not add this session to the startup impact history: " + e
            );
            Scribe.ForceStop();
        }
    }

    private static void Evict(List<StartupImpactSessionIndexEntry> entries)
    {
        var settings = LoadingProgressMod.Settings;
        var evicted = SessionRetention.SelectForEviction(
            entries,
            settings.SessionsToKeep,
            settings.KeepPinnedSessions,
            entry => entry.Pinned,
            entry => entry.Baseline
        );

        foreach (var entry in evicted)
        {
            DeleteSessionFile(entry.Id);
            _ = entries.Remove(entry);
        }
    }

    private static void DeleteSessionFile(string id)
    {
        try
        {
            var path = SessionFilePath(id);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception e)
        {
            LoadingProgressMod.Warning($"Could not delete stored session '{id}': " + e.Message);
        }
    }

    /// <summary>
    /// A timestamp-shaped id, nudged forward if a file already claims it, so two
    /// saves inside the same second cannot overwrite one another.
    /// </summary>
    private static string NewId(DateTime savedAtUtc)
    {
        var baseId = savedAtUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var id = baseId;
        var suffix = 1;
        while (File.Exists(SessionFilePath(id)))
        {
            id = $"{baseId}-{suffix.ToString(CultureInfo.InvariantCulture)}";
            suffix++;
        }
        return id;
    }
}
