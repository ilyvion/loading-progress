using System.Diagnostics.CodeAnalysis;
using ilyvion.LoadingProgress.StartupImpact.Dialog.Export;

namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

[HotSwappable]
internal sealed class DialogStartupImpact : Window
{
    private const float ButtonWidth = 80f;
    private const float ExportButtonWidth = 110f;
    private const float ButtonHeight = 32f;
    private const float OuterSpacing = 8f;
    private const float InnerSpacing = 4f;
    private const float CheckboxHeight = 24f;
    private const float TitleHeight = 30f;
    private const float BarHeight = 46f;
    private const float ScaleDetailSliderWidth = 160f;
    private const float ScaleDetailMinTau = 100f;
    private const float ScaleDetailMaxTau = 5000f;
    private const float ScaleDetailRoundTo = 50f;

    private static readonly Dictionary<string, Color> CategoryColors = new()
    {
        {
            "LoadingProgress.StartupImpact.ModConstructor",
            new Color(156f / 255, 147f / 255, 67f / 255)
        },
        { "LoadingProgress.StartupImpact.LoadDefs", new Color(67f / 255, 84f / 255, 156f / 255) },
        {
            "LoadingProgress.StartupImpact.CombineXml",
            new Color(130f / 255, 130f / 255, 130f / 255)
        },
        {
            "LoadingProgress.StartupImpact.TKeySystemParse",
            new Color(84f / 255, 207f / 255, 154f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ErrorCheckPatches",
            new Color(72f / 255, 121f / 255, 175f / 255)
        },
        {
            "LoadingProgress.StartupImpact.LoadPatches",
            new Color(136f / 255, 156f / 255, 67f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ApplyPatches",
            new Color(156f / 255, 67f / 255, 121f / 255)
        },
        {
            "LoadingProgress.StartupImpact.RegisterXmlInheritance",
            new Color(176f / 255, 223f / 255, 224f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ResolveXmlInheritance",
            new Color(82f / 255, 26f / 255, 106f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ClearCachedPatches",
            new Color(63f / 255, 109f / 255, 125f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ClearCachedXmlInheritance",
            new Color(118f / 255, 136f / 255, 92f / 255)
        },
        {
            "LoadingProgress.StartupImpact.LanguageDatabaseInitAllMetadata",
            new Color(158f / 255, 92f / 255, 93f / 255)
        },
        {
            "LoadingProgress.StartupImpact.DefDatabaseAddAllInMods",
            new Color(168f / 255, 99f / 255, 64f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.NonImplied",
            new Color(94f / 255, 122f / 255, 151f / 255)
        },
        {
            "LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Early",
            new Color(137f / 255, 121f / 255, 161f / 255)
        },
        {
            "LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Final",
            new Color(28f / 255, 76f / 255, 84f / 255)
        },
        {
            "LoadingProgress.StartupImpact.TKeySystemBuildMappings",
            new Color(182f / 255, 168f / 255, 119f / 255)
        },
        {
            "LoadingProgress.StartupImpact.BackStoryTranslationUtilityLoadAndInjectBackstoryData",
            new Color(60f / 255, 49f / 255, 109f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AudioClips",
            new Color(147f / 255, 170f / 255, 143f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.Textures",
            new Color(157f / 255, 140f / 255, 104f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.Strings",
            new Color(92f / 255, 86f / 255, 82f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AssetBundles",
            new Color(163f / 255, 155f / 255, 110f / 255)
        },
        {
            "LoadingProgress.StartupImpact.LoadedLanguageInjectIntoDataBeforeImpliedDefs",
            new Color(122f / 255, 71f / 255, 122f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ColoredTextResetStaticData",
            new Color(169f / 255, 142f / 255, 172f / 255)
        },
        {
            "LoadingProgress.StartupImpact.DefGeneratorGenerateImpliedDefsPreResolve",
            new Color(148f / 255, 87f / 255, 58f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.Implied",
            new Color(145f / 255, 106f / 255, 75f / 255)
        },
        {
            "LoadingProgress.StartupImpact.PlayDataLoaderResetStaticDataPre",
            new Color(189f / 255, 171f / 255, 133f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ResolveReferences",
            new Color(96f / 255, 126f / 255, 110f / 255)
        },
        {
            "LoadingProgress.StartupImpact.DefGeneratorGenerateImpliedDefsPostResolve",
            new Color(76f / 255, 104f / 255, 132f / 255)
        },
        {
            "LoadingProgress.StartupImpact.PlayDataLoaderResetStaticDataPost",
            new Color(164f / 255, 189f / 255, 208f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ErrorCheckAllDefs",
            new Color(157f / 255, 140f / 255, 104f / 255)
        },
        {
            "LoadingProgress.StartupImpact.KeyPrefsInit",
            new Color(133f / 255, 105f / 255, 128f / 255)
        },
        {
            "LoadingProgress.StartupImpact.ShortHashGiverGiveAllShortHashes",
            new Color(176f / 255, 157f / 255, 147f / 255)
        },
        {
            "LoadingProgress.StartupImpact.SolidBioDatabaseLoadAllBios",
            new Color(86f / 255, 98f / 255, 136f / 255)
        },
        {
            "LoadingProgress.StartupImpact.LoadedLanguageInjectIntoDataAfterImpliedDefs",
            new Color(142f / 255, 153f / 255, 170f / 255)
        },
        {
            "LoadingProgress.StartupImpact.StaticConstructorOnStartupUtilityCallAll",
            new Color(171f / 255, 114f / 255, 131f / 255)
        },
        {
            "LoadingProgress.StartupImpact.FloatMenuMakerMapInit",
            new Color(120f / 255, 108f / 255, 86f / 255)
        },
        {
            "LoadingProgress.StartupImpact.GlobalTextureAtlasManagerBakeStaticAtlases",
            new Color(131f / 255, 88f / 255, 96f / 255)
        },
        {
            "LoadingProgress.StartupImpact.AbstractFilesystemClearAllCache",
            new Color(114f / 255, 99f / 255, 143f / 255)
        },
        // { "extra-30", new Color(92f/255, 86f/255, 82f/255) },

        {
            "LoadingProgress.StartupImpact.Total.Mods",
            new Color(175f / 255, 126f / 255, 72f / 255)
        },
        {
            "LoadingProgress.StartupImpact.Total.ModsHidden",
            new Color(103f / 255, 83f / 255, 61f / 255)
        },
        {
            "LoadingProgress.StartupImpact.Total.BaseGame",
            new Color(72f / 255, 121f / 255, 175f / 255)
        },
        {
            "LoadingProgress.StartupImpact.Total.Others",
            new Color(35f / 255, 50f / 255, 84f / 255)
        },
    };
    private static readonly Color DefaultColor = new(128f / 255f, 128f / 255f, 128f / 255f);

    private const float HeaderHeight = 20f;

    private enum SortColumn
    {
        Impact,
        Name,
    }

    private bool _useLogScale;
    private float _scaleDetailTau = 1000f;
    private UiTable _table;
    private readonly StartupImpactSessionData _currentSessionData;
    private StartupImpactSessionData _sessionData;
    private StartupImpactSessionViewData _sessionViewData;
    private string _modFilter = "";
    private List<StartupImpactSessionModViewData> _filteredModViewData = [];
    private SortColumn _sortColumn = SortColumn.Impact;
    private bool _sortAscending;

    // Set window width to 800 and height to the lesser of 800 or 75% of the screen height
    public override Vector2 InitialSize => new(800f, Math.Min(800f, UI.screenHeight * 0.75f));

    private double? _statusTextSetTime;
    private const float StatusTextDisplayTimeSeconds = 5f;
    private string? _exportedPath;
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

    private readonly bool _wasTrackingEnabledAtStartup = LoadingProgressMod
        .instance
        .StartupImpact
        .WasTrackingEnabledAtStartup;

    /// <summary>
    /// Whether anything has ever been kept in the history.
    /// </summary>
    /// <remarks>
    /// The screen shown when tracking was off only offers History when there is
    /// one to open, so a player who has never turned tracking on sees the same
    /// screen they saw before this existed rather than a button leading to three
    /// empty groups. Decided once when the window opens: DoWindowContents is at
    /// its class coupling limit, and this would otherwise be a file check per
    /// frame.
    /// </remarks>
    private readonly bool _hasSavedHistory = File.Exists(StartupImpactSessionStorage.IndexFilePath);

    public DialogStartupImpact()
        : this(null) { }

    /// <summary>
    /// Opens showing a stored session, or this launch's own when given none.
    /// </summary>
    /// <remarks>
    /// The current session is captured either way, so PreClose restores it and
    /// reopening the window shows this launch again, exactly as the Load button
    /// behaved before.
    /// </remarks>
    internal DialogStartupImpact(StartupImpactSessionData? sessionData)
    {
        // Movable, so it can be placed beside the saved session list. The two are different
        // window types, so both stay open, and Load in the list swaps what this one shows.
        draggable = true;

        _currentSessionData = StartupImpactSessionData.FromCurrentSession();
        _sessionData = sessionData ?? _currentSessionData;
        Initialize();
    }

    /// <summary>
    /// Shows a stored session in the window that is already open.
    /// </summary>
    /// <remarks>
    /// Opening a second window would discard the log scale, scale detail and
    /// sort the player had set, and WindowStack.Add removes any window of the
    /// same type anyway, so the list would be swapping one window for another
    /// on every Load.
    /// </remarks>
    internal void ShowSession(StartupImpactSessionData sessionData)
    {
        _sessionData = sessionData;
        Initialize();

        // This window may already have been on screen, showing a different run, so say what
        // just replaced it rather than silently swapping every figure.
        StatusText = "LoadingProgress.StartupImpact.History.Status.Loaded".Translate(
            StartupImpactSessionIndexEntry.FormatTimestamp(sessionData.SavedAtUtc)
        );
    }

    /// <summary>
    /// Opens the saved session picker.
    /// </summary>
    /// <remarks>
    /// Its own method so DoWindowContents does not gain a reference to another
    /// type: that method already sits on its class coupling limit.
    /// </remarks>
    private static void OpenHistory() => Find.WindowStack.Add(new DialogStartupImpactHistory());

    public override void PreClose()
    {
        base.PreClose();

        // Loading a saved session (via the "Load" button) replaces the displayed data with
        // historical data. Don't let that outlive the dialog: if it's reopened later some other
        // way, it should reflect the current session again, not whatever was last loaded.
        _sessionData = _currentSessionData;
    }

    [MemberNotNull([nameof(_sessionViewData), nameof(_table)])]
    private void Initialize()
    {
        _sessionViewData = new StartupImpactSessionViewData(_sessionData);
        _table = new UiTable(_sessionData.Mods.Count, 40, [-40, 30, -80, 38]);
        _modFilter = "";
        ApplyModFilter();
    }

    private void ApplyModFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(_modFilter)
            ? _sessionViewData.ModViewData
            : _sessionViewData.ModViewData.Where(info =>
                info.ModData.ModName.Contains(_modFilter, StringComparison.OrdinalIgnoreCase)
                || info.ModData.ModPackageId.Contains(
                    _modFilter,
                    StringComparison.OrdinalIgnoreCase
                )
            );

        filtered = _sortColumn switch
        {
            SortColumn.Name => _sortAscending
                ? filtered.OrderBy(info => info.ModData.ModName, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderByDescending(
                    info => info.ModData.ModName,
                    StringComparer.OrdinalIgnoreCase
                ),
            SortColumn.Impact => _sortAscending
                ? filtered.OrderBy(info => info.ModData.TotalImpact)
                : filtered.OrderByDescending(info => info.ModData.TotalImpact),
            _ => throw new ArgumentOutOfRangeException(nameof(_sortColumn)),
        };

        _filteredModViewData = [.. filtered];
        _table.RowCount = _filteredModViewData.Count;
    }

    private void SetSort(SortColumn column)
    {
        if (_sortColumn == column)
        {
            _sortAscending = !_sortAscending;
        }
        else
        {
            _sortColumn = column;
            // Impact starts high-to-low (matches the previous fixed ordering);
            // Name starts A-to-Z.
            _sortAscending = column == SortColumn.Name;
        }
        ApplyModFilter();
    }

    private void DrawTableHeaderColumn(int column, Rect rect)
    {
        var sortColumn = column switch
        {
            1 => SortColumn.Name,
            2 => SortColumn.Impact,
            _ => (SortColumn?)null,
        };
        if (sortColumn is not { } resolvedColumn)
        {
            return;
        }

        var label =
            resolvedColumn == SortColumn.Name
                ? "LoadingProgress.StartupImpact.ColumnName".Translate()
                : "LoadingProgress.StartupImpact.ColumnImpact".Translate();
        if (_sortColumn == resolvedColumn)
        {
            label = $"{label} {(_sortAscending ? "▲" : "▼")}";
        }

        if (Widgets.ButtonText(rect, label, drawBackground: false))
        {
            SetSort(resolvedColumn);
        }
    }

    public override void DoWindowContents(Rect area)
    {
        if (HasNothingToShow())
        {
            DoDisabledContents(area);
            return;
        }

        float y = 0;

        // Log scale checkbox (top right), with an optional detail slider to its left
        Text.Anchor = TextAnchor.MiddleRight;
        var label = "LoadingProgress.StartupImpact.LogarithmicScale".Translate();
        var checkboxWidth = Text.CalcSize(label).x + 24f + OuterSpacing;
        var checkboxRect = new Rect(area.width - checkboxWidth, y, checkboxWidth, CheckboxHeight);

        if (_useLogScale)
        {
            var sliderRect = new Rect(
                checkboxRect.x - OuterSpacing - ScaleDetailSliderWidth,
                y + 2,
                ScaleDetailSliderWidth,
                CheckboxHeight
            );
            _scaleDetailTau = Widgets.HorizontalSlider(
                sliderRect,
                _scaleDetailTau,
                ScaleDetailMinTau,
                ScaleDetailMaxTau,
                label: "LoadingProgress.StartupImpact.ScaleDetail".Translate(
                    ProfilerBar.TimeText(_scaleDetailTau)
                ),
                roundTo: ScaleDetailRoundTo
            );
            TooltipHandler.TipRegion(
                sliderRect,
                "LoadingProgress.StartupImpact.ScaleDetail.Tip".Translate()
            );
        }

        Widgets.CheckboxLabeled(checkboxRect, label, ref _useLogScale);
        TooltipHandler.TipRegion(
            checkboxRect,
            "LoadingProgress.StartupImpact.LogarithmicScale.Tip".Translate()
        );

        Text.Anchor = TextAnchor.MiddleLeft;
        Text.Font = GameFont.Medium;

        var profilerBar = new ProfilerBar()
        {
            UseLogScale = _useLogScale,
            ProgressBarPadding = 2f,
            DefaultColor = DefaultColor,
            Tau = _scaleDetailTau,
        };

        Rect titleRect = new(0, y, area.width, TitleHeight);
        Widgets.Label(
            titleRect,
            "LoadingProgress.StartupImpact.StartupTime".Translate(
                ProfilerBar.TimeText(_sessionData.LoadingTime)
            )
        );
        DrawStoredSessionCaption(titleRect);
        y += titleRect.height;

        Rect profileRect = new(0, y, area.width, BarHeight);
        profilerBar.Draw(
            profileRect,
            _sessionViewData.MetricsTotal,
            StartupImpactSessionViewData.CategoriesTotal,
            _sessionData.LoadingTime,
            CategoryColors
        );
        y += profileRect.height + InnerSpacing;

        if (
            _sessionData.ModsLoaded is int modsLoaded
            && _sessionData.DefsParsed is int defsParsed
            && _sessionData.PatchOperationsApplied is int patchOperationsApplied
        )
        {
            Text.Font = GameFont.Small;
            Rect sessionStatsRect = new(0, y, area.width, Text.LineHeight);
            Widgets.Label(
                sessionStatsRect,
                "LoadingProgress.StartupImpact.SessionStats".Translate(
                    defsParsed.ToString("N0", CultureInfo.CurrentCulture),
                    patchOperationsApplied.ToString("N0", CultureInfo.CurrentCulture),
                    modsLoaded.ToString("N0", CultureInfo.CurrentCulture)
                )
            );
            y += sessionStatsRect.height + InnerSpacing;
            Text.Font = GameFont.Medium;
        }

        Rect nonmodsTitleRect = new(0, y, area.width, TitleHeight);
        Widgets.Label(
            nonmodsTitleRect,
            "LoadingProgress.StartupImpact.StartupNonmods".Translate(
                ProfilerBar.TimeText(_sessionViewData.BasegameLoadingTime)
            )
        );
        y += nonmodsTitleRect.height;

        Rect nonmodsProfileRect = new(0, y, area.width, BarHeight);
        var nonmodsOffThreadRect = nonmodsProfileRect;
        if (
            LoadingProgressMod.Settings.ShowBaseGameOffThreadImpact
            && _sessionViewData.OffThreadBasegameLoadingTime > 1f
        )
        {
            nonmodsOffThreadRect.yMin += nonmodsProfileRect.height / 2;
            nonmodsProfileRect.yMax -= nonmodsProfileRect.height / 2;
            profilerBar.Draw(
                nonmodsOffThreadRect,
                _sessionViewData.MetricsOffThreadNonMods,
                _sessionViewData.CategoriesNonMods,
                Math.Max(
                    _sessionViewData.BasegameLoadingTime,
                    _sessionViewData.OffThreadBasegameLoadingTime
                ),
                _sessionViewData.CategoryColorsNonMods
            );
        }
        profilerBar.Draw(
            nonmodsProfileRect,
            _sessionViewData.MetricsNonMods,
            _sessionViewData.CategoriesNonMods,
            _sessionViewData.BasegameLoadingTime,
            _sessionViewData.CategoryColorsNonMods
        );
        y += BarHeight + OuterSpacing;

        Rect modsTitleRect = new(0, y, area.width, TitleHeight);
        Widgets.Label(
            modsTitleRect,
            "LoadingProgress.StartupImpact.StartupMods".Translate(
                ProfilerBar.TimeText(_sessionViewData.ModsLoadingTime)
            )
        );
        y += modsTitleRect.height;

        Rect modsProfileRect = new(0, y, area.width, BarHeight);
        profilerBar.Draw(
            modsProfileRect,
            _sessionViewData.MetricsMods,
            _sessionViewData.CategoriesMods,
            _sessionViewData.ModsLoadingTime,
            _sessionViewData.CategoryColorsMods,
            translateCategories: false
        );
        y += modsProfileRect.height + InnerSpacing;
        Text.Font = GameFont.Small;

        var filterLabel = "LoadingProgress.StartupImpact.FilterMods".Translate();
        var filterLabelWidth = Text.CalcSize(filterLabel).x + InnerSpacing;
        Widgets.Label(new Rect(0, y, filterLabelWidth, CheckboxHeight), filterLabel);
        var newModFilter = Widgets.TextField(
            new Rect(filterLabelWidth, y, area.width - filterLabelWidth, CheckboxHeight),
            _modFilter
        );
        if (newModFilter != _modFilter)
        {
            _modFilter = newModFilter;
            ApplyModFilter();
        }
        y += CheckboxHeight + OuterSpacing;

        _table.Header(0, y, area.width, HeaderHeight, DrawTableHeaderColumn);
        y += HeaderHeight + InnerSpacing;

        var bottomOffset = ButtonHeight + OuterSpacing + InnerSpacing; // Button height + spacing + padding
        _table.StartTable(0, y, area.width, area.height - y - bottomOffset);

        var row = 0;
        foreach (var info in _filteredModViewData)
        {
            if (_table.IsRowVisible(row))
            {
                if (
                    Widgets.ButtonImage(
                        _table.Cell(0, row),
                        Textures.Eye,
                        info.HideInUi ? Color.white : Color.grey,
                        tooltip: "LoadingProgress.StartupImpact.ToggleModVisibility.Tip".Translate()
                    )
                )
                {
                    info.HideInUi = !info.HideInUi;
                    _sessionViewData.CalculateBaseGameStats();
                    _sessionViewData.CalculateModStats();
                }

                GUI.color = info.HideInUi ? Color.grey : Color.white;

                _table.TruncatedLabel(1, row, info.ModData.ModName);

                _table.TruncatedLabel(2, row, ProfilerBar.TimeText(info.ModData.TotalImpact));

                var rect = _table.Cell(3, row);
                var rect2 = rect;
                if (info.ModData.OffThreadTotalImpact > 1f)
                {
                    rect2.yMin += rect.height / 2;
                    rect.yMax -= rect.height / 2;
                    profilerBar.Draw(
                        rect2,
                        info.OffThreadMetrics,
                        _sessionViewData.Categories,
                        Math.Max(_sessionViewData.MaxImpact, info.ModData.OffThreadTotalImpact),
                        CategoryColors
                    );
                }
                profilerBar.Draw(
                    rect,
                    info.Metrics,
                    _sessionViewData.Categories,
                    Math.Max(_sessionViewData.MaxImpact, info.ModData.TotalImpact),
                    CategoryColors
                );
            }
            row++;
        }

        _table.EndTable();

        GUI.color = Color.white;
        var showSave = HasSomethingToSave();
        var trailingButtons = showSave ? 3 : 2;
        var buttonsStartX =
            area.width
            - ((ButtonWidth + OuterSpacing) * trailingButtons)
            - (ExportButtonWidth + OuterSpacing);
        var x = buttonsStartX;
        var yBtn = area.height - ButtonHeight - 3f;

        HandleStatus(yBtn, buttonsStartX);
        if (
            Widgets.ButtonText(
                new Rect(x, yBtn, ExportButtonWidth, ButtonHeight),
                "LoadingProgress.StartupImpact.ExportHtml".Translate()
            )
        )
        {
            try
            {
                var html = StartupImpactHtmlExporter.BuildReport(
                    _sessionData,
                    _sessionViewData,
                    CategoryColors,
                    DefaultColor,
                    LoadingProgressMod.Settings.ShowBaseGameOffThreadImpact
                );
                var exportPath = Path.Combine(
                    GenFilePaths.SaveDataFolderPath,
                    GenText.SanitizeFilename("StartupImpactReport.html")
                );
                File.WriteAllText(exportPath, html);
                StatusText = "LoadingProgress.StartupImpact.Exported".Translate();
                _exportedPath = exportPath;
            }
            catch (Exception ex)
            {
                StatusText = "LoadingProgress.StartupImpact.ExportFailed".Translate(ex.Message);
                _exportedPath = null;
            }
        }
        TooltipHandler.TipRegion(
            new Rect(x, yBtn, ExportButtonWidth, ButtonHeight),
            "LoadingProgress.StartupImpact.ExportHtml.Tip".Translate()
        );
        x += ExportButtonWidth + OuterSpacing;
        if (showSave)
        {
            if (
                Widgets.ButtonText(new Rect(x, yBtn, ButtonWidth, ButtonHeight), "Save".Translate())
            )
            {
                _exportedPath = null;
                try
                {
                    // The current session, never whichever one is on screen. External tools
                    // such as RimSort read this file, so writing a stored session over it
                    // would hand them an old run labelled as the latest.
                    //
                    // It records into the history too. This button is only shown when
                    // automatic saving is off, which is the one case where nothing else writes
                    // a session, so without this the History button beside it would have
                    // nothing to open.
                    StartupImpactSessionStorage.SaveAndRecord(_currentSessionData);
                    StatusText = "LoadingProgress.StartupImpact.Saved".Translate();
                }
                catch (Exception ex)
                {
                    StatusText = "LoadingProgress.StartupImpact.SaveFailed".Translate(ex.Message);
                }
            }
            x += ButtonWidth + OuterSpacing;
        }
        if (
            Widgets.ButtonText(
                new Rect(x, yBtn, ButtonWidth, ButtonHeight),
                "LoadingProgress.StartupImpact.History".Translate()
            )
        )
        {
            _exportedPath = null;
            OpenHistory();
        }
        x += ButtonWidth + OuterSpacing;
        if (
            Widgets.ButtonText(
                new Rect(x, yBtn, ButtonWidth, ButtonHeight),
                "Close".Translate(),
                true,
                false,
                true
            )
        )
        {
            Close();
        }
        Text.Anchor = TextAnchor.UpperLeft;

        void HandleStatus(float yBtn, float buttonsStartX)
        {
            // Show SaveStatus if set, and clear after timeout
            if (
                _statusTextSetTime.HasValue
                && Time.realtimeSinceStartup - _statusTextSetTime.Value
                    > StatusTextDisplayTimeSeconds
            )
            {
                StatusText = "";
            }
            else if (!string.IsNullOrEmpty(StatusText))
            {
                var statusRect = new Rect(0, yBtn, buttonsStartX - OuterSpacing, ButtonHeight);
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(statusRect, (TaggedString)StatusText);
                if (_exportedPath != null)
                {
                    TooltipHandler.TipRegion(
                        statusRect,
                        "LoadingProgress.StartupImpact.Exported.Tip".Translate(_exportedPath)
                    );
                }
            }
        }
    }

    /// <summary>
    /// Whether there is nothing to draw: tracking was off for this startup, and
    /// no stored session has been opened in its place.
    /// </summary>
    /// <remarks>
    /// Only the current session is missing when tracking was off. A stored one
    /// carries its own figures and is worth showing whatever this boot did.
    /// </remarks>
    private bool HasNothingToShow() =>
        !_wasTrackingEnabledAtStartup && ReferenceEquals(_sessionData, _currentSessionData);

    /// <summary>
    /// Whether the Save button belongs on the button row: this startup measured
    /// something, and nothing else is writing it to disk.
    /// </summary>
    /// <remarks>
    /// With auto-save on, the report file is rewritten every startup and the
    /// history keeps the sessions, so the button does nothing the mod is not
    /// doing already.
    ///
    /// The tracking half matters because opening a stored session draws this
    /// layout even when tracking was off for the whole load. The current session
    /// is empty in that case, and Save writes the current session, so the button
    /// would hand external tools a report with no timings in it and add a 00:00
    /// row to the history.
    ///
    /// Its own method for the same reason as <see cref="HasNothingToShow"/>:
    /// DoWindowContents is already at its complexity limit.
    /// </remarks>
    /// <summary>
    /// Says which stored run is on screen, at the right-hand end of the title
    /// row.
    /// </summary>
    /// <remarks>
    /// The picker stays open beside this window and its Load button swaps every
    /// figure here, so without this the only thing telling one run from another
    /// is the number the player remembers seeing a moment ago. Nothing is drawn
    /// for this startup's own session, which is what the window has always
    /// shown.
    ///
    /// Its own method to keep DoWindowContents off its class coupling limit.
    /// </remarks>
    private void DrawStoredSessionCaption(Rect titleRect)
    {
        if (
            ReferenceEquals(_sessionData, _currentSessionData)
            || _sessionData.SavedAtUtc == DateTime.MinValue
        )
        {
            return;
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = DefaultColor;
        Widgets.Label(
            titleRect,
            "LoadingProgress.StartupImpact.ShowingSaved".Translate(
                StartupImpactSessionIndexEntry.FormatTimestamp(_sessionData.SavedAtUtc)
            )
        );
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.MiddleLeft;
        Text.Font = GameFont.Medium;
    }

    private bool HasSomethingToSave() =>
        _wasTrackingEnabledAtStartup && !LoadingProgressMod.Settings.AutoSaveStartupImpactReport;

    private void DoDisabledContents(Rect area)
    {
        var isTrackingEnabledNow = LoadingProgressMod.Settings.TrackStartupLoadingImpact;

        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.MiddleCenter;
        var textRect = new Rect(0, 0, area.width, area.height - ButtonHeight - (OuterSpacing * 2));
        var secondLine = (
            isTrackingEnabledNow
                ? "LoadingProgress.StartupImpact.WillTrackNextStartup"
                : "LoadingProgress.StartupImpact.EnableTrackingHint"
        ).Translate();
        Widgets.Label(
            textRect,
            $"{"LoadingProgress.StartupImpact.DisabledAtStartup".Translate()}\n\n{secondLine}"
        );
        Text.Anchor = TextAnchor.UpperLeft;
        Text.Font = GameFont.Small;

        var yBtn = area.height - ButtonHeight;
        var closeLabel = "Close".Translate();
        var historyLabel = "LoadingProgress.StartupImpact.History".Translate();

        // Sessions kept from earlier runs are still readable even though this one recorded
        // nothing, so the way to them belongs on this screen too. Only when there are some:
        // with no history this screen is exactly the one the mod showed before, which is what
        // a player who has never turned tracking on should still see.
        var trailingButtons = _hasSavedHistory ? 2 : 1;
        if (isTrackingEnabledNow)
        {
            var width = (ButtonWidth * trailingButtons) + (OuterSpacing * (trailingButtons - 1));
            var buttonX = (area.width - width) / 2f;
            if (_hasSavedHistory)
            {
                if (
                    Widgets.ButtonText(
                        new Rect(buttonX, yBtn, ButtonWidth, ButtonHeight),
                        historyLabel
                    )
                )
                {
                    OpenHistory();
                }
                buttonX += ButtonWidth + OuterSpacing;
            }
            if (
                Widgets.ButtonText(
                    new Rect(buttonX, yBtn, ButtonWidth, ButtonHeight),
                    closeLabel,
                    true,
                    false,
                    true
                )
            )
            {
                Close();
            }
            return;
        }

        var enableLabel = "LoadingProgress.StartupImpact.EnableTracking".Translate();
        var enableButtonWidth = Text.CalcSize(enableLabel).x + (OuterSpacing * 2);
        var rowWidth =
            enableButtonWidth + (OuterSpacing * trailingButtons) + (ButtonWidth * trailingButtons);
        var enableButtonRect = new Rect(
            (area.width - rowWidth) / 2f,
            yBtn,
            enableButtonWidth,
            ButtonHeight
        );
        if (Widgets.ButtonText(enableButtonRect, enableLabel))
        {
            LoadingProgressMod.Settings.TrackStartupLoadingImpact = true;
            LoadingProgressMod.instance.WriteSettings();
            Close();
        }
        var nextX = enableButtonRect.xMax + OuterSpacing;
        if (_hasSavedHistory)
        {
            if (Widgets.ButtonText(new Rect(nextX, yBtn, ButtonWidth, ButtonHeight), historyLabel))
            {
                OpenHistory();
            }
            nextX += ButtonWidth + OuterSpacing;
        }
        var closeButtonRect = new Rect(nextX, yBtn, ButtonWidth, ButtonHeight);
        if (Widgets.ButtonText(closeButtonRect, closeLabel, true, false, true))
        {
            Close();
        }
    }

    [StaticConstructorOnStartup]
    private static class Textures
    {
        public static readonly Texture2D Eye = ContentFinder<Texture2D>.Get("LP_Eye");
    }
}
