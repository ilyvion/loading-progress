namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

/// <summary>
/// The header a stored session is listed by.
/// </summary>
/// <remarks>
/// Held apart from the session itself so the picker can list a whole history
/// without parsing every session file. On a 228-mod list a session is about
/// 220 KB, and 96% of that is the per-mod block the list never shows.
///
/// An entry for a boot that never finished has no session file behind it:
/// the process died before there was anything to write, so only the header
/// survives.
/// </remarks>
internal sealed class StartupImpactSessionIndexEntry : IExposable
{
    private string id = "";
    private long savedAtUtcTicks;
    private float loadingTime;
    private int modsLoaded;
    private int modListHash;
    private bool completed = true;
    private string lastStage = "";
    private bool pinned;
    private bool baseline;
    private bool measuredToMenu;

    public string Id => id;

    public DateTime SavedAtUtc =>
        savedAtUtcTicks == 0 ? DateTime.MinValue : new DateTime(savedAtUtcTicks, DateTimeKind.Utc);

    /// <summary>
    /// The time the session is listed by, in milliseconds: the startup time the
    /// startup impact window gives it (see <see cref="ListedTime"/>); 0 for a
    /// boot that never finished. Note this is not the unit Settings.LoadingTimes
    /// uses, which is seconds.
    /// </summary>
    public float LoadingTime => loadingTime;

    public int ModsLoaded => modsLoaded;

    public int ModListHash => modListHash;

    /// <summary>
    /// Whether <see cref="LoadingTime"/> runs to the main menu. False for a session saved
    /// before that was measured, and for a startup that went straight into a game; theirs
    /// runs to the end of loading only, so it reads shorter than a newer session's would.
    /// </summary>
    public bool MeasuredToMenu => measuredToMenu;

    /// <summary>
    /// Whether the boot reached the end of loading. False means the run was
    /// reconstructed from a marker left behind by a boot that hung or crashed.
    /// </summary>
    public bool Completed => completed;

    /// <summary>
    /// For an unfinished boot, the last loading stage it reached.
    /// </summary>
    public string LastStage => lastStage;

    /// <summary>
    /// Whether a session file holding the per-mod breakdown exists for this entry.
    /// </summary>
    public bool HasBody => completed;

    public bool Pinned
    {
        get => pinned;
        set => pinned = value;
    }

    public bool Baseline
    {
        get => baseline;
        set => baseline = value;
    }

    public StartupImpactSessionIndexEntry() { }

    /// <summary>
    /// The history's note on a boot that never finished, from the last stage it recorded:
    /// where it stopped, or that it stopped after loading, before the main menu was usable.
    /// </summary>
    internal static string UnfinishedNote(string? lastStage) =>
        string.IsNullOrEmpty(lastStage)
            ? "LoadingProgress.StartupImpact.History.Note.Unfinished".Translate().ToString()
        : lastStage == nameof(LoadingStage.Finished)
            ? "LoadingProgress.StartupImpact.History.Note.UnfinishedAfterLoading"
                .Translate()
                .ToString()
        : "LoadingProgress.StartupImpact.History.Note.UnfinishedAt"
            .Translate(TranslateStage(lastStage!))
            .ToString();

    /// <summary>
    /// The loading stage an unfinished boot reached, in the player's language.
    /// </summary>
    /// <remarks>
    /// The marker stores the LoadingStage member's name, which is also the
    /// suffix of the key the loading window already uses for that stage, so the
    /// translated name costs a lookup rather than a second set of strings.
    /// Eight of those strings end in a parenthesised italic placeholder for the
    /// mod being worked on, which has no meaning once the stage is over, and
    /// every one of them ends in an ellipsis that reads oddly inside the note
    /// this goes in; both come off.
    ///
    /// The text is resolved by hand rather than through Translate, for two
    /// reasons. CanTranslate only consults the active language, so a player on
    /// any language but English would have been shown the raw member name for
    /// every stage; and Translate pseudo-translates a fallback result in dev
    /// mode, which would garble the one case this exists to serve.
    /// </remarks>
    internal static string TranslateStage(string stageName)
    {
        if (string.IsNullOrEmpty(stageName))
        {
            return "";
        }

        if (!TryGetStageText("LoadingProgress.Stage." + StageKeySuffix(stageName), out var text))
        {
            return stageName;
        }

        var marker = text.IndexOf(" (<i>{0}</i>)", StringComparison.Ordinal);
        return (marker >= 0 ? text[..marker] : text).TrimEnd('.');
    }

    /// <summary>
    /// The stage key a recorded stage name resolves through.
    /// </summary>
    /// <remarks>
    /// The second delayed-initialization pass has no string of its own: it is
    /// the same work as the first, and the loading window labels it with the
    /// first one's key too.
    /// </remarks>
    private static string StageKeySuffix(string stageName) =>
        stageName == nameof(LoadingStage.ExecuteToExecuteWhenFinished2)
            ? nameof(LoadingStage.ExecuteToExecuteWhenFinished)
            : stageName;

    private static bool TryGetStageText(string key, out string text)
    {
        if (
            LanguageDatabase.activeLanguage is { } active
            && active.TryGetTextFromKey(key, out var activeText)
        )
        {
            text = activeText.ToString();
            return true;
        }

        if (
            LanguageDatabase.defaultLanguage is { } fallback
            && fallback.TryGetTextFromKey(key, out var fallbackText)
        )
        {
            text = fallbackText.ToString();
            return true;
        }

        text = "";
        return false;
    }

    /// <summary>
    /// Formats a stored load time for display.
    /// </summary>
    /// <remarks>
    /// Its own method so the unit is asserted in one place. LoadingTime comes
    /// from ProfilerStopwatch, which returns milliseconds, while
    /// Settings.LoadingTimes holds seconds; reading this one as seconds renders
    /// a ten second load as nearly three hours. Rounded as the startup impact window rounds the
    /// same time: see <see cref="ProfilerBar.WholeSecondsText"/>.
    /// </remarks>
    internal static string FormatLoadingTime(float loadingTimeMilliseconds) =>
        ProfilerBar.WholeSecondsText(loadingTimeMilliseconds);

    /// <summary>
    /// How a stored session's time is written wherever it is shown.
    /// </summary>
    /// <remarks>
    /// Local time, because the player is comparing it against when they
    /// remember starting the game. Sortable, unambiguous in any language, and
    /// the same shape the HTML export already writes: RimWorld pins
    /// CurrentCulture to en-US, so a culture-sensitive format would show US
    /// ordering to every player regardless.
    /// </remarks>
    internal static string FormatTimestamp(DateTime savedAtUtc) =>
        savedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    internal static StartupImpactSessionIndexEntry ForCompletedSession(
        string id,
        StartupImpactSessionData data
    ) =>
        data is null
            ? throw new ArgumentNullException(nameof(data))
            : new StartupImpactSessionIndexEntry
            {
                id = id,
                savedAtUtcTicks =
                    data.SavedAtUtc == DateTime.MinValue
                        ? DateTime.UtcNow.Ticks
                        : data.SavedAtUtc.Ticks,
                loadingTime = ListedTime(data),
                modsLoaded = data.ModsLoaded ?? data.Mods.Count,
                modListHash = data.ModListHash,
                completed = true,
                lastStage = "",
                measuredToMenu = data.TimeToMenu > 0f,
            };

    /// <summary>
    /// Takes the figures of a session saved again under this entry, as when the player saves
    /// a session already in the history by hand.
    /// </summary>
    internal void UpdateFrom(StartupImpactSessionData data)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        loadingTime = ListedTime(data);
        modsLoaded = data.ModsLoaded ?? data.Mods.Count;
        measuredToMenu = data.TimeToMenu > 0f;
    }

    /// <summary>
    /// The time a session is listed by, the startup time the startup impact window puts in its
    /// title: see <see cref="StartupImpactSessionViewData.Span(StartupImpactSessionData)"/>.
    /// </summary>
    internal static float ListedTime(StartupImpactSessionData data) =>
        StartupImpactSessionViewData.Span(data);

    internal static StartupImpactSessionIndexEntry ForUnfinishedBoot(
        string id,
        DateTime startedAtUtc,
        int modListHash,
        int modsLoaded,
        string lastStage
    ) =>
        new()
        {
            id = id,
            savedAtUtcTicks = startedAtUtc.Ticks,
            loadingTime = 0f,
            modsLoaded = modsLoaded,
            modListHash = modListHash,
            completed = false,
            lastStage = lastStage ?? "",
        };

    public void ExposeData()
    {
        Scribe_Values.Look(ref id, "id", "");
        Scribe_Values.Look(ref savedAtUtcTicks, "savedAtUtcTicks");
        Scribe_Values.Look(ref loadingTime, "loadingTime");
        Scribe_Values.Look(ref modsLoaded, "modsLoaded");
        Scribe_Values.Look(ref modListHash, "modListHash");
        Scribe_Values.Look(ref completed, "completed", true);
        Scribe_Values.Look(ref lastStage, "lastStage", "");
        Scribe_Values.Look(ref pinned, "pinned");
        Scribe_Values.Look(ref baseline, "baseline");
        Scribe_Values.Look(ref measuredToMenu, "measuredToMenu");

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            id ??= "";
            lastStage ??= "";
        }
    }
}
