namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// A small file written while a boot is in progress and removed once it
/// finishes. One still on disk at the next startup describes a boot that never
/// completed, and becomes the only record that run leaves behind.
/// </summary>
/// <remarks>
/// Plain text rather than Scribe, deliberately. This is written during loading,
/// and Scribe.saver is global state the loader is already using -- the same
/// reason the session auto-save defers itself to ExecuteWhenFinished. It is
/// also rewritten on every stage change, so it stays a few short lines.
///
/// Every operation swallows its own IO errors. A boot must not fail because
/// the diagnostic watching it could not write a file.
/// </remarks>
internal static class StartupImpactCrashMarker
{
    private const string MarkerFileName = "StartupImpactInProgress.txt";

    internal static string MarkerFilePath =>
        Path.Combine(GenFilePaths.SaveDataFolderPath, GenText.SanitizeFilename(MarkerFileName));

    /// <summary>
    /// Whether this boot is being watched. Checked by callers before building a
    /// stage name, so a disabled marker costs a bool read per stage change.
    /// </summary>
    /// <remarks>
    /// Volatile because Clear runs from FinishLoading, which can be off the main
    /// thread, while stage changes read this from the loading thread. A stale read
    /// would rewrite the marker after the boot had finished, and the next startup
    /// would then report a boot that never failed.
    /// </remarks>
    internal static bool IsActive
    {
        get => _isActive;
        private set => _isActive = value;
    }

    private static volatile bool _isActive;
    private static long _startedAtUtcTicks;
    private static int _modListHash;
    private static string _stage = "";

    /// <summary>
    /// A boot that was still in progress when the game last stopped.
    /// </summary>
    internal readonly struct UnfinishedBoot(
        DateTime startedAtUtc,
        int modListHash,
        int modsLoaded,
        string lastStage
    )
    {
        public DateTime StartedAtUtc { get; } = startedAtUtc;
        public int ModListHash { get; } = modListHash;
        public int ModsLoaded { get; } = modsLoaded;
        public string LastStage { get; } = lastStage;
    }

    /// <summary>
    /// Reads and removes a marker left by a previous boot, if there is one.
    /// Call before <see cref="Begin"/>, which overwrites it.
    /// </summary>
    internal static UnfinishedBoot? TakePrevious()
    {
        try
        {
            if (!File.Exists(MarkerFilePath))
            {
                return null;
            }

            var text = File.ReadAllText(MarkerFilePath);
            File.Delete(MarkerFilePath);
            return Parse(text);
        }
        catch (Exception e)
        {
            LoadingProgressMod.Warning("Could not read the previous startup marker: " + e.Message);
            return null;
        }
    }

    /// <summary>
    /// Reads a marker's contents, or null when they describe no usable boot.
    /// </summary>
    /// <remarks>
    /// Separate from the file so the format can be round-tripped against
    /// <see cref="Format"/> in the test suite. A marker is written repeatedly
    /// while the game loads and may be cut short by whatever stopped that boot,
    /// so a truncated or partly written file has to read as no marker at all
    /// rather than throwing.
    ///
    /// Completeness is what makes that safe. <see cref="Format"/> writes the
    /// stage line last and ends it with a newline, so a marker missing either
    /// was cut short; checking only the fields it happens to carry lets a cut
    /// inside the tick digits through as a boot that started in the year 1.
    /// </remarks>
    internal static UnfinishedBoot? Parse(string text)
    {
        if (text.Length == 0 || text[^1] != '\n')
        {
            return null;
        }

        var fields = ParseFields(text);

        return
            !fields.TryGetValue("stage", out var stage)
            || !fields.TryGetValue("startedAtUtcTicks", out var ticksText)
            || !long.TryParse(
                ticksText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var ticks
            )
            || ticks <= 0
            || ticks > DateTime.MaxValue.Ticks
            ? null
            : new UnfinishedBoot(
                new DateTime(ticks, DateTimeKind.Utc),
                ReadInt(fields, "modListHash"),
                ReadInt(fields, "modsLoaded"),
                stage
            );
    }

    /// <summary>
    /// The contents written for a boot in progress.
    /// </summary>
    internal static string Format(
        long startedAtUtcTicks,
        int modListHash,
        int modsLoaded,
        string stage
    ) =>
        "version=1\n"
        + "startedAtUtcTicks="
        + startedAtUtcTicks.ToString(CultureInfo.InvariantCulture)
        + "\nmodListHash="
        + modListHash.ToString(CultureInfo.InvariantCulture)
        + "\nmodsLoaded="
        + modsLoaded.ToString(CultureInfo.InvariantCulture)
        + "\nstage="
        + (stage ?? "")
        + "\n";

    /// <summary>
    /// Starts watching this boot.
    /// </summary>
    internal static void Begin(int modListHash)
    {
        IsActive = true;
        _startedAtUtcTicks = DateTime.UtcNow.Ticks;
        _modListHash = modListHash;
        _stage = "";
        Write();
    }

    /// <summary>
    /// Records the stage this boot has reached, so a marker left behind says
    /// where it stopped rather than only that it did.
    /// </summary>
    internal static void RecordStage(string stage)
    {
        if (!IsActive)
        {
            return;
        }
        _stage = stage ?? "";
        Write();
    }

    /// <summary>
    /// Marks this boot as finished, removing the marker.
    /// </summary>
    internal static void Clear()
    {
        IsActive = false;
        try
        {
            if (File.Exists(MarkerFilePath))
            {
                File.Delete(MarkerFilePath);
            }
        }
        catch (Exception e)
        {
            LoadingProgressMod.Warning("Could not remove the startup marker: " + e.Message);
        }
    }

    private static void Write()
    {
        try
        {
            File.WriteAllText(
                MarkerFilePath,
                Format(
                    _startedAtUtcTicks,
                    _modListHash,
                    LoadingSessionStats.ModsLoaded ?? 0,
                    _stage
                )
            );
        }
        catch (Exception e)
        {
            // Never let the watcher break the thing it is watching.
            LoadingProgressMod.Warning("Could not write the startup marker: " + e.Message);
            IsActive = false;
        }
    }

    private static Dictionary<string, string> ParseFields(string text)
    {
        Dictionary<string, string> fields = [];
        foreach (var line in text.Split('\n'))
        {
            var field = line.TrimEnd('\r');
            var separator = field.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }
            fields[field[..separator]] = field[(separator + 1)..];
        }
        return fields;
    }

    private static int ReadInt(Dictionary<string, string> fields, string key) =>
        fields.TryGetValue(key, out var text)
        && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
}
