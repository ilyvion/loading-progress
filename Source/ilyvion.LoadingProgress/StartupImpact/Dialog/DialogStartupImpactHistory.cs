namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

/// <summary>
/// Lists the stored startup impact sessions so two runs can be compared.
/// </summary>
/// <remarks>
/// Reads the index rather than the sessions themselves: a session is around
/// 220 KB on a large mod list, almost all of it the per-mod block this list
/// never shows, so opening the picker must not mean parsing the lot.
/// </remarks>
[HotSwappable]
internal sealed class DialogStartupImpactHistory : Window
{
    private const float RowHeight = 32f;
    private const float HeaderHeight = 22f;
    private const float TitleHeight = 34f;
    private const float FooterHeight = 36f;
    private const float ButtonHeight = 24f;
    private const float OuterSpacing = 8f;
    private const float StatusTextDisplayTimeSeconds = 5f;

    private static readonly Color PinnedAccent = new(0.851f, 0.643f, 0.255f);
    private static readonly Color BaselineAccent = new(0.910f, 0.710f, 0.333f);
    private static readonly Color UnfinishedColor = new(0.878f, 0.541f, 0.502f);
    private static readonly Color DifferentListColor = new(0.878f, 0.627f, 0.353f);
    private static readonly Color MutedColor = new(0.659f, 0.643f, 0.612f);

    /// <summary>
    /// Which section of the list a session is shown in.
    /// </summary>
    internal enum SessionGroup
    {
        Pinned,
        Recent,
        Baseline,
    }

    private enum RowKind
    {
        GroupHeader,
        Session,
        EmptyGroup,
    }

    private sealed class Row
    {
        public RowKind Kind { get; init; }
        public SessionGroup Group { get; init; }
        public StartupImpactSessionIndexEntry? Entry { get; init; }
    }

    private static readonly SessionGroup[] GroupOrder =
    [
        SessionGroup.Pinned,
        SessionGroup.Recent,
        SessionGroup.Baseline,
    ];

    private List<StartupImpactSessionIndexEntry> _entries;
    private List<Row> _rows = [];
    private readonly UiTable _table;
    private readonly int _currentModListHash;

    /// <summary>
    /// The footer line. Falls back to the summary once it has been on screen
    /// long enough, the same way the startup impact window's own status does,
    /// so the count of what is stored is not lost for the rest of the session
    /// behind a "Pinned." from ten minutes ago.
    /// </summary>
    private string StatusText
    {
        get;
        set
        {
            field = value;
            if (!string.IsNullOrEmpty(value))
            {
                _statusTextSetTime = Time.realtimeSinceStartup;
            }
        }
    } = "";

    private float _statusTextSetTime;

    /// <param name="modal">
    /// Whether to block the window underneath. The settings screen opens this
    /// modally, because changing how many sessions to keep while a now-stale list
    /// is on screen has no good outcome. The impact window opens it non-modally,
    /// so the two can sit side by side and Load swaps what the impact window
    /// shows.
    /// </param>
    public DialogStartupImpactHistory(bool modal = false)
    {
        absorbInputAroundWindow = modal;
        draggable = true;

        _currentModListHash = StartupImpactSessionData.CurrentModListHash();
        _entries = StartupImpactSessionStorage.LoadIndex();
        // The two action columns are sized for the longest label any language gives them:
        // Russian's "Закрепить" and "Открепить" are wider than an English "Unpin", and a
        // label wider than its button is clipped at both ends rather than spilling.
        _table = new UiTable(0, RowHeight, [-14f, -140f, -70f, -55f, -85f, 1f, -80f, -80f, -30f]);
        RebuildRows();
    }

    public override Vector2 InitialSize => new(940f, Math.Min(620f, UI.screenHeight * 0.85f));

    private void RebuildRows()
    {
        _entries.SortByDescending(entry => entry.SavedAtUtc.Ticks);

        List<Row> rows = [];
        foreach (var group in GroupOrder)
        {
            rows.Add(new Row { Kind = RowKind.GroupHeader, Group = group });

            var any = false;
            foreach (var entry in _entries)
            {
                if (GroupOf(entry) != group)
                {
                    continue;
                }
                rows.Add(
                    new Row
                    {
                        Kind = RowKind.Session,
                        Group = group,
                        Entry = entry,
                    }
                );
                any = true;
            }

            if (!any)
            {
                rows.Add(new Row { Kind = RowKind.EmptyGroup, Group = group });
            }
        }

        _rows = rows;
        _table.RowCount = rows.Count;
    }

    /// <summary>
    /// Which section a session belongs to. The baseline wins over a pin,
    /// because a session can hold both: taking the baseline no longer clears a
    /// pin, so that the pin is still there when the role moves on.
    /// </summary>
    internal static SessionGroup GroupOf(StartupImpactSessionIndexEntry entry) =>
        entry.Baseline ? SessionGroup.Baseline
        : entry.Pinned ? SessionGroup.Pinned
        : SessionGroup.Recent;

    public override void DoWindowContents(Rect inRect)
    {
        var y = inRect.y;

        Text.Font = GameFont.Medium;
        Widgets.Label(
            new Rect(inRect.x, y, inRect.width, TitleHeight),
            "LoadingProgress.StartupImpact.History.Title".Translate()
        );
        Text.Font = GameFont.Small;
        y += TitleHeight;

        _table.Header(inRect.x, y, inRect.width, HeaderHeight, DrawHeaderColumn);
        y += HeaderHeight;

        var tableHeight = inRect.height - (y - inRect.y) - FooterHeight - OuterSpacing;
        _table.StartTable(inRect.x, y, inRect.width, tableHeight);
        for (var row = 0; row < _rows.Count; row++)
        {
            if (_table.IsRowVisible(row))
            {
                DrawRow(row);
            }
        }
        _table.EndTable();

        DrawFooter(new Rect(inRect.x, inRect.yMax - FooterHeight, inRect.width, FooterHeight));
    }

    private static void DrawHeaderColumn(int column, Rect rect)
    {
        var key = column switch
        {
            1 => "LoadingProgress.StartupImpact.History.Column.When",
            2 => "LoadingProgress.StartupImpact.History.Column.Load",
            3 => "LoadingProgress.StartupImpact.History.Column.Mods",
            4 => "LoadingProgress.StartupImpact.History.Column.List",
            5 => "LoadingProgress.StartupImpact.History.Column.Notes",
            _ => null,
        };
        if (key is null)
        {
            return;
        }

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = MutedColor;
        Widgets.Label(rect, key.Translate());
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    private void DrawRow(int row)
    {
        var entry = _rows[row];
        switch (entry.Kind)
        {
            case RowKind.GroupHeader:
                DrawGroupHeader(row, entry.Group);
                break;
            case RowKind.EmptyGroup:
                DrawEmptyGroup(row, entry.Group);
                break;
            case RowKind.Session:
                DrawSessionRow(row, entry.Entry!);
                break;
            default:
                break;
        }
    }

    private void DrawGroupHeader(int row, SessionGroup group)
    {
        var rect = FullRowRect(row);
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = AccentFor(group);
        Widgets.Label(rect, GroupCaption(group).Translate());
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    private void DrawEmptyGroup(int row, SessionGroup group)
    {
        var rect = FullRowRect(row);

        if (group == SessionGroup.Baseline)
        {
            var button = new Rect(
                rect.x,
                rect.y + ((rect.height - ButtonHeight) / 2f),
                170f,
                ButtonHeight
            );
            if (
                Widgets.ButtonText(
                    button,
                    "LoadingProgress.StartupImpact.History.ChooseBaseline".Translate()
                )
            )
            {
                OpenBaselineMenu();
            }
            rect.xMin = button.xMax + OuterSpacing;
        }

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = MutedColor;
        Widgets.Label(rect, EmptyGroupHint(group).Translate());
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    private void DrawSessionRow(int row, StartupImpactSessionIndexEntry entry)
    {
        var group = GroupOf(entry);
        var rowRect = FullRowRect(row);

        if (group != SessionGroup.Recent)
        {
            Widgets.DrawBoxSolid(
                new Rect(rowRect.x, rowRect.y, 3f, rowRect.height),
                AccentFor(group)
            );
        }
        else if (row % 2 == 1)
        {
            Widgets.DrawAltRect(rowRect);
        }
        Widgets.DrawHighlightIfMouseover(rowRect);

        Text.Anchor = TextAnchor.MiddleLeft;

        Label(1, row, StartupImpactSessionIndexEntry.FormatTimestamp(entry.SavedAtUtc));
        Label(
            2,
            row,
            entry.Completed
                ? StartupImpactSessionIndexEntry.FormatLoadingTime(entry.LoadingTime)
                : "LoadingProgress.StartupImpact.History.NoLoadTime".Translate(),
            entry.Completed ? Color.white : UnfinishedColor
        );
        Label(3, row, entry.ModsLoaded.ToString(CultureInfo.CurrentCulture), MutedColor);

        var comparable = entry.ModListHash == _currentModListHash;
        Label(
            4,
            row,
            entry.ModListHash.ToString("X8", CultureInfo.InvariantCulture),
            comparable ? MutedColor : DifferentListColor
        );
        Label(5, row, NoteFor(entry, comparable), NoteColorFor(entry, comparable));

        Text.Anchor = TextAnchor.UpperLeft;

        DrawRowButtons(row, entry, group);

        if (
            Event.current.type == EventType.MouseDown
            && Event.current.button == 1
            && Mouse.IsOver(rowRect)
        )
        {
            OpenRowMenu(entry);
            Event.current.Use();
        }
    }

    private void DrawRowButtons(int row, StartupImpactSessionIndexEntry entry, SessionGroup group)
    {
        var pinKey = group switch
        {
            SessionGroup.Pinned => "LoadingProgress.StartupImpact.History.Unpin",
            SessionGroup.Baseline => "LoadingProgress.StartupImpact.History.ClearBaseline",
            SessionGroup.Recent => "LoadingProgress.StartupImpact.History.Pin",
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
        };

        if (Widgets.ButtonText(ButtonRect(6, row), pinKey.Translate()))
        {
            TogglePinOrBaseline(entry, group);
        }

        var loadRect = ButtonRect(7, row);
        if (entry.HasBody)
        {
            if (
                Widgets.ButtonText(
                    loadRect,
                    "LoadingProgress.StartupImpact.History.Load".Translate()
                )
            )
            {
                OpenSession(entry);
            }
        }
        else
        {
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            _ = Widgets.ButtonText(
                loadRect,
                "LoadingProgress.StartupImpact.History.Load".Translate(),
                true,
                false,
                false
            );
            GUI.color = previous;
            TooltipHandler.TipRegion(
                loadRect,
                "LoadingProgress.StartupImpact.History.NoBreakdown".Translate()
            );
        }

        var deleteCell = _table.Cell(8, row);
        var icon = new Rect(0f, 0f, 18f, 18f) { center = deleteCell.center };
        if (Widgets.ButtonImage(icon, TexButton.Delete, MutedColor, Color.white))
        {
            ConfirmDelete(entry);
        }
    }

    private void DrawFooter(Rect rect)
    {
        if (
            !string.IsNullOrEmpty(StatusText)
            && Time.realtimeSinceStartup - _statusTextSetTime > StatusTextDisplayTimeSeconds
        )
        {
            StatusText = "";
        }

        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = string.IsNullOrEmpty(StatusText) ? MutedColor : BaselineAccent;
        Widgets.Label(
            new Rect(rect.x, rect.y, rect.width - 110f, rect.height),
            string.IsNullOrEmpty(StatusText) ? Summary() : StatusText
        );
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        if (
            Widgets.ButtonText(
                new Rect(rect.xMax - 100f, rect.y + 4f, 100f, ButtonHeight),
                "Close".Translate()
            )
        )
        {
            Close();
        }
    }

    private string Summary() =>
        "LoadingProgress.StartupImpact.History.Summary".Translate(
            _entries.Count,
            LoadingProgressMod.Settings.SessionsToKeep
        );

    private Rect FullRowRect(int row)
    {
        var first = _table.Cell(0, row);
        var last = _table.Cell(8, row);
        return new Rect(first.x, first.y, last.xMax - first.x, first.height);
    }

    private Rect ButtonRect(int column, int row)
    {
        var cell = _table.Cell(column, row);
        return new Rect(
            cell.x + 3f,
            cell.y + ((cell.height - ButtonHeight) / 2f),
            cell.width - 6f,
            ButtonHeight
        );
    }

    private void Label(int column, int row, string text, Color? color = null)
    {
        var cell = _table.Cell(column, row);
        cell.xMin += 4f;
        cell.xMax -= 4f;
        GUI.color = color ?? Color.white;
        Widgets.Label(cell, text.Truncate(cell.width));
        GUI.color = Color.white;
    }

    private static Color AccentFor(SessionGroup group) =>
        group switch
        {
            SessionGroup.Pinned => PinnedAccent,
            SessionGroup.Baseline => BaselineAccent,
            SessionGroup.Recent => MutedColor,
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
        };

    private static string GroupCaption(SessionGroup group) =>
        group switch
        {
            SessionGroup.Pinned => "LoadingProgress.StartupImpact.History.Group.Pinned",
            SessionGroup.Baseline => "LoadingProgress.StartupImpact.History.Group.Baseline",
            SessionGroup.Recent => "LoadingProgress.StartupImpact.History.Group.Recent",
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
        };

    private static string EmptyGroupHint(SessionGroup group) =>
        group switch
        {
            SessionGroup.Pinned => "LoadingProgress.StartupImpact.History.Empty.Pinned",
            SessionGroup.Baseline => "LoadingProgress.StartupImpact.History.Empty.Baseline",
            SessionGroup.Recent => "LoadingProgress.StartupImpact.History.Empty.Recent",
            _ => throw new ArgumentOutOfRangeException(nameof(group), group, null),
        };

    private static string NoteFor(StartupImpactSessionIndexEntry entry, bool comparable) =>
        entry.Completed
            ? comparable
                ? string.Empty
                : "LoadingProgress.StartupImpact.History.Note.DifferentList".Translate().ToString()
            : string.IsNullOrEmpty(entry.LastStage)
                ? "LoadingProgress.StartupImpact.History.Note.Unfinished".Translate().ToString()
                : "LoadingProgress.StartupImpact.History.Note.UnfinishedAt"
                    .Translate(StartupImpactSessionIndexEntry.TranslateStage(entry.LastStage))
                    .ToString();

    private static Color NoteColorFor(StartupImpactSessionIndexEntry entry, bool comparable) =>
        !entry.Completed ? UnfinishedColor
        : !comparable ? DifferentListColor
        : MutedColor;

    private void TogglePinOrBaseline(StartupImpactSessionIndexEntry entry, SessionGroup group)
    {
        switch (group)
        {
            case SessionGroup.Recent:
                Persist(entry, pinned: true, baseline: entry.Baseline);
                StatusText = "LoadingProgress.StartupImpact.History.Status.Pinned".Translate();
                break;
            case SessionGroup.Pinned:
                Persist(entry, pinned: false, baseline: entry.Baseline);
                StatusText = "LoadingProgress.StartupImpact.History.Status.Unpinned".Translate();
                break;
            case SessionGroup.Baseline:
                Persist(entry, pinned: entry.Pinned, baseline: false);
                StatusText =
                    "LoadingProgress.StartupImpact.History.Status.BaselineCleared".Translate();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(group), group, null);
        }
    }

    private void SetBaseline(StartupImpactSessionIndexEntry entry)
    {
        // The pin is left alone. A session that was pinned before it became the baseline is
        // still pinned when the role moves on, so clearing the baseline cannot quietly drop
        // a session the player had protected into the eviction pool.
        Persist(entry, pinned: entry.Pinned, baseline: true);
        StatusText = "LoadingProgress.StartupImpact.History.Status.BaselineSet".Translate();
    }

    private void OpenRowMenu(StartupImpactSessionIndexEntry entry)
    {
        List<FloatMenuOption> options = [];
        if (!entry.Baseline)
        {
            options.Add(
                new FloatMenuOption(
                    "LoadingProgress.StartupImpact.History.SetBaseline".Translate(),
                    () => SetBaseline(entry)
                )
            );
        }
        options.Add(
            new FloatMenuOption(
                "LoadingProgress.StartupImpact.History.Delete".Translate(),
                () => ConfirmDelete(entry)
            )
        );
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void OpenBaselineMenu()
    {
        List<FloatMenuOption> options = [];
        foreach (var entry in _entries)
        {
            var target = entry;
            options.Add(
                new FloatMenuOption(
                    StartupImpactSessionIndexEntry.FormatTimestamp(target.SavedAtUtc),
                    () => SetBaseline(target)
                )
            );
        }

        if (options.Count == 0)
        {
            StatusText = "LoadingProgress.StartupImpact.History.Empty.Recent".Translate();
            return;
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void OpenSession(StartupImpactSessionIndexEntry entry)
    {
        var data = StartupImpactSessionStorage.LoadSession(entry.Id);
        if (data is null)
        {
            StatusText = "LoadingProgress.StartupImpact.History.Status.LoadFailed".Translate();
            return;
        }
        // Reuse the window the player is already looking at, so the view settings they chose
        // survive the swap.
        var open = Find.WindowStack.WindowOfType<DialogStartupImpact>();
        if (open is not null)
        {
            open.ShowSession(data);
        }
        else
        {
            Find.WindowStack.Add(new DialogStartupImpact(data));
        }

        // The impact window may be behind this one, or off to one side, so say here that the
        // click did something rather than leaving the player to spot it.
        StatusText = "LoadingProgress.StartupImpact.History.Status.Loaded".Translate(
            StartupImpactSessionIndexEntry.FormatTimestamp(entry.SavedAtUtc)
        );
    }

    private void ConfirmDelete(StartupImpactSessionIndexEntry entry) =>
        Find.WindowStack.Add(
            Dialog_MessageBox.CreateConfirmation(
                DeleteConfirmationText(entry),
                () =>
                {
                    _entries = StartupImpactSessionStorage.Delete(entry.Id);
                    StatusText = "LoadingProgress.StartupImpact.History.Status.Deleted".Translate();
                    RebuildRows();
                },
                true
            )
        );

    private static string DeleteConfirmationText(StartupImpactSessionIndexEntry entry)
    {
        var text = "LoadingProgress.StartupImpact.History.Confirm.Delete"
            .Translate(StartupImpactSessionIndexEntry.FormatTimestamp(entry.SavedAtUtc))
            .ToString();

        // Nothing is promoted when the baseline goes. There is no implicit baseline: a session
        // holds the role because the player gave it, and until another is chosen no session
        // has it.
        return entry.Baseline
            ? text + "\n\n" + "LoadingProgress.StartupImpact.History.Confirm.Baseline".Translate()
            : text;
    }

    /// <summary>
    /// Writes one session's flags through storage and adopts the history it
    /// reads back, so a session saved beside this window survives the change and
    /// appears in the list.
    /// </summary>
    private void Persist(StartupImpactSessionIndexEntry entry, bool pinned, bool baseline)
    {
        _entries = StartupImpactSessionStorage.SetFlags(entry.Id, pinned, baseline);
        RebuildRows();
    }
}
