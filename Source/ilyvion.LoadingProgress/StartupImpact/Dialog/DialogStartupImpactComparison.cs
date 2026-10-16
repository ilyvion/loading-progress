namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

/// <summary>
/// Shows how one stored startup differs from another: the overall times and counts, every
/// loading stage, every mod and every step.
/// </summary>
[HotSwappable]
internal sealed class DialogStartupImpactComparison : Window
{
    private const float TitleHeight = 34f;
    private const float PickerHeight = 30f;
    private const float PickerButtonWidth = 190f;
    private const float SwapButtonWidth = 80f;
    private const float HeadlineHeight = 32f;
    private const float RowHeight = 28f;
    private const float HeaderHeight = 22f;
    private const float FooterHeight = 40f;
    private const float CloseButtonWidth = 100f;
    private const float ButtonHeight = 28f;
    private const float OuterSpacing = 8f;
    private const float InnerSpacing = 4f;
    private const int DetailLines = 10;

    private static readonly Color SlowerColor = new(0.878f, 0.541f, 0.502f);
    private static readonly Color FasterColor = new(0.533f, 0.784f, 0.502f);
    private static readonly Color IssueColor = new(0.878f, 0.627f, 0.353f);
    private static readonly Color MutedColor = new(0.659f, 0.643f, 0.612f);

    private enum Tab
    {
        Overview,
        Stages,
        Mods,
        Steps,
    }

    private static readonly Tab[] TabOrder = [Tab.Overview, Tab.Stages, Tab.Mods, Tab.Steps];

    /// <summary>
    /// One line of a tab's table, as drawn.
    /// </summary>
    private sealed record DisplayRow(
        string Label,
        string Reference,
        string Compared,
        string Change,
        Color ChangeColor,
        bool Noisy,
        string? Tooltip
    );

    private StartupImpactSessionIndexEntry _reference;
    private StartupImpactSessionIndexEntry _compared;
    private StartupImpactSessionComparison _comparison;
    private Tab _tab = Tab.Overview;
    private readonly Dictionary<Tab, List<DisplayRow>> _rows = [];
    private readonly Dictionary<Tab, UiTable> _tables = [];
    private readonly List<TabRecord> _tabRecords;
    private List<string> _issueLines = [];
    private string _headline = "";
    private string _statusText = "";

    // Translated once per window, since every visible row's change column may need them.
    private readonly string _noisyLabel = "LoadingProgress.StartupImpact.Compare.Noisy".Translate();
    private readonly string _noisyTip =
        "LoadingProgress.StartupImpact.Compare.Noisy.Tip".Translate();
    private readonly string _noisyModTip =
        "LoadingProgress.StartupImpact.Compare.Noisy.ModTip".Translate();

    private DialogStartupImpactComparison(
        StartupImpactSessionIndexEntry reference,
        StartupImpactSessionIndexEntry compared,
        StartupImpactSessionComparison comparison
    )
    {
        draggable = true;
        doCloseX = true;

        _reference = reference;
        _compared = compared;
        _comparison = comparison;

        foreach (var tab in TabOrder)
        {
            _tables[tab] = new UiTable(0, RowHeight, [1f, -105f, -105f, -105f, -60f]);
        }
        _tabRecords =
        [
            .. TabOrder.Select(tab => new TabRecord(
                TabLabel(tab).Translate(),
                () => _tab = tab,
                () => _tab == tab
            )),
        ];
        Rebuild();
    }

    public override Vector2 InitialSize => new(860f, Math.Min(760f, UI.screenHeight * 0.85f));

    /// <summary>
    /// Opens a comparison of two stored sessions, or returns false when either one's data
    /// cannot be read.
    /// </summary>
    internal static bool TryOpen(
        StartupImpactSessionIndexEntry reference,
        StartupImpactSessionIndexEntry compared
    )
    {
        if (Load(reference, compared) is not { } comparison)
        {
            return false;
        }
        Find.WindowStack.Add(new DialogStartupImpactComparison(reference, compared, comparison));
        return true;
    }

    private static StartupImpactSessionComparison? Load(
        StartupImpactSessionIndexEntry reference,
        StartupImpactSessionIndexEntry compared
    ) =>
        StartupImpactSessionStorage.LoadSession(reference.Id) is { } referenceData
        && StartupImpactSessionStorage.LoadSession(compared.Id) is { } comparedData
            ? StartupImpactSessionComparison.Compare(
                StartupImpactSessionFigures.From(referenceData),
                StartupImpactSessionFigures.From(comparedData)
            )
            : null;

    private void Show(
        StartupImpactSessionIndexEntry reference,
        StartupImpactSessionIndexEntry compared
    )
    {
        if (Load(reference, compared) is not { } comparison)
        {
            _statusText = "LoadingProgress.StartupImpact.History.Status.LoadFailed".Translate();
            return;
        }
        _reference = reference;
        _compared = compared;
        _comparison = comparison;
        _statusText = "";
        Rebuild();
    }

    private void Rebuild()
    {
        var loading = _comparison.Totals[0];
        _headline = "LoadingProgress.StartupImpact.Compare.Headline".Translate(
            TimeText(loading.Reference),
            TimeText(loading.Compared),
            TimeChangeText(loading.Delta).Colorize(TimeChangeColor(loading.Delta))
        );
        _issueLines = [.. _comparison.Issues.Select(IssueText)];

        _rows[Tab.Overview] = OverviewRows();
        _rows[Tab.Stages] = [.. _comparison.Stages.Select(TimeRow)];
        _rows[Tab.Mods] = [.. _comparison.Mods.Select(ModRow)];
        _rows[Tab.Steps] = [.. _comparison.Steps.Select(TimeRow)];
        foreach (var tab in TabOrder)
        {
            _tables[tab].RowCount = _rows[tab].Count;
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        var y = inRect.y;

        Text.Font = GameFont.Medium;
        Widgets.Label(
            new Rect(inRect.x, y, inRect.width, TitleHeight),
            "LoadingProgress.StartupImpact.Compare.Title".Translate()
        );
        y += TitleHeight;

        Text.Font = GameFont.Small;
        DrawPickers(new Rect(inRect.x, y, inRect.width, PickerHeight));
        y += PickerHeight + OuterSpacing;

        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(inRect.x, y, inRect.width, HeadlineHeight), _headline);
        y += HeadlineHeight;
        Text.Font = GameFont.Small;

        y = DrawIssues(inRect.x, y, inRect.width);

        y += TabDrawer.TabHeight;
        var tabRect = new Rect(
            inRect.x,
            y,
            inRect.width,
            inRect.yMax - y - FooterHeight - OuterSpacing
        );
        Widgets.DrawMenuSection(tabRect);
        _ = TabDrawer.DrawTabs(tabRect, _tabRecords);
        DrawTab(tabRect.ContractedBy(OuterSpacing));

        DrawFooter(new Rect(inRect.x, inRect.yMax - FooterHeight, inRect.width, FooterHeight));
    }

    private void DrawPickers(Rect rect)
    {
        var x = rect.x;
        x = DrawPicker(
            x,
            rect,
            "LoadingProgress.StartupImpact.Compare.Reference".Translate(),
            _reference,
            picked => Show(picked, _compared)
        );

        var swapRect = new Rect(x, rect.y, SwapButtonWidth, rect.height);
        if (Widgets.ButtonText(swapRect, "LoadingProgress.StartupImpact.Compare.Swap".Translate()))
        {
            Show(_compared, _reference);
        }
        TooltipHandler.TipRegion(
            swapRect,
            "LoadingProgress.StartupImpact.Compare.Swap.Tip".Translate()
        );
        x = swapRect.xMax + (OuterSpacing * 3);

        _ = DrawPicker(
            x,
            rect,
            "LoadingProgress.StartupImpact.Compare.Compared".Translate(),
            _compared,
            picked => Show(_reference, picked)
        );
    }

    /// <summary>
    /// A label and a button showing which session it is set to, which opens a list of the stored
    /// sessions to set it to instead. Returns the x after it.
    /// </summary>
    private static float DrawPicker(
        float x,
        Rect row,
        string label,
        StartupImpactSessionIndexEntry current,
        Action<StartupImpactSessionIndexEntry> pick
    )
    {
        var labelWidth = Text.CalcSize(label).x + InnerSpacing;
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(x, row.y, labelWidth, row.height), label);
        Text.Anchor = TextAnchor.UpperLeft;
        x += labelWidth + InnerSpacing;

        var buttonRect = new Rect(x, row.y, PickerButtonWidth, row.height);
        if (Widgets.ButtonText(buttonRect, SessionLabel(current)))
        {
            List<FloatMenuOption> options =
            [
                .. StartupImpactSessionStorage
                    .LoadIndex()
                    .Where(entry => entry.HasBody)
                    .Select(entry => new FloatMenuOption(SessionLabel(entry), () => pick(entry))),
            ];
            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
        return buttonRect.xMax + OuterSpacing;
    }

    /// <summary>
    /// How a stored session is named wherever one is picked: when it ran, and whether it is
    /// the baseline.
    /// </summary>
    internal static string SessionLabel(StartupImpactSessionIndexEntry entry)
    {
        var timestamp = StartupImpactSessionIndexEntry.FormatTimestamp(entry.SavedAtUtc);
        return entry.Baseline
            ? "LoadingProgress.StartupImpact.Compare.BaselineSession".Translate(timestamp)
            : timestamp;
    }

    private float DrawIssues(float x, float y, float width)
    {
        foreach (var line in _issueLines)
        {
            var height = Text.CalcHeight(line, width);
            GUI.color = IssueColor;
            Widgets.Label(new Rect(x, y, width, height), line);
            GUI.color = Color.white;
            y += height;
        }
        return _issueLines.Count > 0 ? y + OuterSpacing : y;
    }

    private void DrawTab(Rect rect)
    {
        var y = rect.y;
        if (TabSummary() is { } summary)
        {
            GUI.color = MutedColor;
            var height = Text.CalcHeight(summary, rect.width);
            Widgets.Label(new Rect(rect.x, y, rect.width, height), summary);
            GUI.color = Color.white;
            y += height + InnerSpacing;
        }

        var table = _tables[_tab];
        var rows = _rows[_tab];
        table.Header(rect.x, y, rect.width, HeaderHeight, DrawHeaderColumn);
        y += HeaderHeight;

        table.StartTable(rect.x, y, rect.width, rect.yMax - y);
        for (var row = 0; row < rows.Count; row++)
        {
            if (table.IsRowVisible(row))
            {
                DrawRow(table, row, rows[row]);
            }
        }
        table.EndTable();
    }

    private string? TabSummary() =>
        _tab switch
        {
            Tab.Stages => "LoadingProgress.StartupImpact.Compare.Summary.Stages".Translate(
                TimeChangeText(_comparison.StageDeltaSum)
            ),
            Tab.Mods => "LoadingProgress.StartupImpact.Compare.Summary.Mods".Translate(),
            Tab.Steps => "LoadingProgress.StartupImpact.Compare.Summary.Steps".Translate(),
            Tab.Overview => null,
            _ => throw new ArgumentOutOfRangeException(nameof(_tab), _tab, null),
        };

    private static void DrawHeaderColumn(int column, Rect rect)
    {
        var key = column switch
        {
            1 => "LoadingProgress.StartupImpact.Compare.Reference",
            2 => "LoadingProgress.StartupImpact.Compare.Compared",
            3 => "LoadingProgress.StartupImpact.Compare.Change",
            _ => null,
        };
        if (key is null)
        {
            return;
        }

        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = MutedColor;
        Widgets.Label(
            new Rect(rect.x, rect.y, rect.width - InnerSpacing, rect.height),
            key.Translate()
        );
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;
    }

    private void DrawRow(UiTable table, int row, DisplayRow display)
    {
        var rowRect = table.Cell(0, row);
        rowRect.xMax = table.Cell(4, row).xMax;
        if (row % 2 == 1)
        {
            Widgets.DrawAltRect(rowRect);
        }
        Widgets.DrawHighlightIfMouseover(rowRect);

        Text.Anchor = TextAnchor.MiddleLeft;
        Cell(table, 0, row, display.Label, Color.white);
        Text.Anchor = TextAnchor.MiddleRight;
        Cell(table, 1, row, display.Reference, MutedColor);
        Cell(table, 2, row, display.Compared, Color.white);
        Cell(table, 3, row, display.Change, display.ChangeColor);

        if (display.Noisy)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Cell(table, 4, row, _noisyLabel, MutedColor);
            Text.Font = GameFont.Small;
        }
        Text.Anchor = TextAnchor.UpperLeft;

        if (display.Tooltip is { } tooltip)
        {
            TooltipHandler.TipRegion(rowRect, tooltip);
        }
    }

    private static void Cell(UiTable table, int column, int row, string text, Color color)
    {
        var cell = table.Cell(column, row);
        cell.xMin += InnerSpacing;
        cell.xMax -= InnerSpacing;
        GUI.color = color;
        Widgets.Label(cell, text.Truncate(cell.width));
        GUI.color = Color.white;
    }

    private void DrawFooter(Rect rect)
    {
        var textRect = new Rect(
            rect.x,
            rect.y,
            rect.width - CloseButtonWidth - OuterSpacing,
            rect.height
        );
        Text.Font = GameFont.Tiny;
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = string.IsNullOrEmpty(_statusText) ? MutedColor : IssueColor;
        Widgets.Label(
            textRect,
            string.IsNullOrEmpty(_statusText)
                ? "LoadingProgress.StartupImpact.Compare.NoiseHint".Translate().ToString()
                : _statusText
        );
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        if (
            Widgets.ButtonText(
                new Rect(
                    rect.xMax - CloseButtonWidth,
                    rect.y + ((rect.height - ButtonHeight) / 2f),
                    CloseButtonWidth,
                    ButtonHeight
                ),
                "Close".Translate()
            )
        )
        {
            Close();
        }
    }

    private List<DisplayRow> OverviewRows()
    {
        var reference = _comparison.Reference;
        var compared = _comparison.Compared;

        List<DisplayRow> rows = [.. _comparison.Totals.Select(TimeRow)];
        rows.AddRange(_comparison.Counts.Select(CountRow));

        var sameList = reference.ModListHash == compared.ModListHash;
        rows.Add(
            TextRow(
                "LoadingProgress.StartupImpact.Compare.ModList",
                reference.ModListHash.ToString("X8", CultureInfo.InvariantCulture),
                compared.ModListHash.ToString("X8", CultureInfo.InvariantCulture),
                sameList,
                "LoadingProgress.StartupImpact.Compare.Different"
            )
        );
        rows.Add(
            ModListRow("LoadingProgress.StartupImpact.Compare.AddedMods", _comparison.AddedMods)
        );
        rows.Add(
            ModListRow("LoadingProgress.StartupImpact.Compare.RemovedMods", _comparison.RemovedMods)
        );
        rows.Add(
            TextRow(
                "LoadingProgress.StartupImpact.Compare.ModOrder",
                "",
                "",
                !_comparison.ModOrderChanged,
                "LoadingProgress.StartupImpact.Compare.Changed"
            )
        );
        rows.Add(
            TextRow(
                "LoadingProgress.StartupImpact.Compare.HarmonyHooks",
                OnOffText(reference.HarmonyHooksTimed),
                OnOffText(compared.HarmonyHooksTimed),
                reference.HarmonyHooksTimed == compared.HarmonyHooksTimed,
                "LoadingProgress.StartupImpact.Compare.Different"
            )
        );
        return rows;
    }

    private DisplayRow TimeRow(ComparisonRow row) =>
        new(
            row.Label,
            TimeText(row.Reference),
            TimeText(row.Compared),
            TimeChangeText(row.Delta),
            TimeChangeColor(row.Delta),
            row.Noisy,
            row.Noisy ? _noisyTip : null
        );

    private static DisplayRow CountRow(ComparisonRow row)
    {
        var delta = (int)Math.Round(row.Delta);
        return new(
            row.Label,
            ((int)row.Reference).ToString("N0", CultureInfo.CurrentCulture),
            ((int)row.Compared).ToString("N0", CultureInfo.CurrentCulture),
            delta == 0
                ? "0"
                : (delta > 0 ? "+" : "−")
                    + Math.Abs(delta).ToString("N0", CultureInfo.CurrentCulture),
            delta == 0 ? MutedColor : Color.white,
            false,
            null
        );
    }

    private static DisplayRow TextRow(
        string labelKey,
        string reference,
        string compared,
        bool same,
        string differentKey
    ) =>
        new(
            labelKey.Translate(),
            reference,
            compared,
            (same ? "LoadingProgress.StartupImpact.Compare.Same" : differentKey).Translate(),
            same ? MutedColor : IssueColor,
            false,
            null
        );

    private static DisplayRow ModListRow(string labelKey, IReadOnlyList<string> names) =>
        new(
            labelKey.Translate(),
            "",
            "",
            names.Count.ToString(CultureInfo.CurrentCulture),
            names.Count == 0 ? MutedColor : IssueColor,
            false,
            names.Count == 0 ? null : string.Join("\n", names)
        );

    private DisplayRow ModRow(ComparisonRow row)
    {
        var notLoaded = "LoadingProgress.StartupImpact.Compare.NotLoaded".Translate();
        return new(
            row.Label,
            row.InReference ? TimeText(row.Reference) : notLoaded,
            row.InCompared ? TimeText(row.Compared) : notLoaded,
            TimeChangeText(row.Delta),
            TimeChangeColor(row.Delta),
            row.Noisy,
            ModTooltip(row)
        );
    }

    private string? ModTooltip(ComparisonRow row)
    {
        var lines = row
            .Details.Where(detail => Math.Abs(detail.Delta) >= 1f)
            .Take(DetailLines)
            .Select(detail =>
                $"{detail.Label}: {TimeText(detail.Reference)} → {TimeText(detail.Compared)} ({TimeChangeText(detail.Delta)})"
            )
            .ToList();
        if (lines.Count == 0)
        {
            return row.Noisy ? _noisyModTip : null;
        }

        var tip =
            "LoadingProgress.StartupImpact.Compare.ModDetails".Translate()
            + "\n"
            + string.Join("\n", lines);
        return row.Noisy ? tip + "\n\n" + _noisyModTip : tip;
    }

    private static string IssueText(ComparisonIssue issue) =>
        (
            issue switch
            {
                ComparisonIssue.DifferentModList =>
                    "LoadingProgress.StartupImpact.Compare.Issue.DifferentModList",
                ComparisonIssue.ModOrderChanged =>
                    "LoadingProgress.StartupImpact.Compare.Issue.ModOrderChanged",
                ComparisonIssue.HarmonyHookTimingDiffers =>
                    "LoadingProgress.StartupImpact.Compare.Issue.HarmonyHookTimingDiffers",
                ComparisonIssue.HarmonyHookTimingUnknown =>
                    "LoadingProgress.StartupImpact.Compare.Issue.HarmonyHookTimingUnknown",
                ComparisonIssue.NotBothTimedToMenu =>
                    "LoadingProgress.StartupImpact.Compare.Issue.NotBothTimedToMenu",
                _ => throw new ArgumentOutOfRangeException(nameof(issue), issue, null),
            }
        ).Translate();

    private static string OnOffText(bool? value) =>
        (
            value switch
            {
                true => "LoadingProgress.StartupImpact.Compare.On",
                false => "LoadingProgress.StartupImpact.Compare.Off",
                null => "LoadingProgress.StartupImpact.Compare.Unknown",
            }
        ).Translate();

    private static string TabLabel(Tab tab) =>
        tab switch
        {
            Tab.Overview => "LoadingProgress.StartupImpact.Compare.Tab.Overview",
            Tab.Stages => "LoadingProgress.StartupImpact.Compare.Tab.Stages",
            Tab.Mods => "LoadingProgress.StartupImpact.Compare.Tab.Mods",
            Tab.Steps => "LoadingProgress.StartupImpact.Compare.Tab.Steps",
            _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, null),
        };

    private static string TimeText(float ms) => ProfilerBar.TimeText(Mathf.Round(ms));

    /// <summary>
    /// A change in time with its sign, rounded as the times beside it are.
    /// </summary>
    internal static string TimeChangeText(float deltaMs)
    {
        var rounded = Mathf.Round(deltaMs);
        var text = ProfilerBar.TimeText(Math.Abs(rounded));
        return rounded > 0 ? "+" + text
            : rounded < 0 ? "−" + text
            : text;
    }

    private static Color TimeChangeColor(float deltaMs) =>
        Mathf.Round(deltaMs) > 0 ? SlowerColor
        : Mathf.Round(deltaMs) < 0 ? FasterColor
        : MutedColor;
}
