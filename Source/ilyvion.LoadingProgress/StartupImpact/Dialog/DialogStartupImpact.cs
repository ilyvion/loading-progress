
using System.Diagnostics.CodeAnalysis;

namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

[HotSwappable]
internal sealed class DialogStartupImpact : Window
{
    private const float ButtonWidth = 80f;
    private const float ButtonHeight = 32f;
    private const float OuterSpacing = 8f;
    private const float InnerSpacing = 4f;
    private const float CheckboxHeight = 24f;
    private const float TitleHeight = 30f;
    private const float BarHeight = 46f;

    private static readonly Dictionary<string, Color> CategoryColors = new()
      {
        { "LoadingProgress.StartupImpact.ModConstructor", new Color(156f/255, 147f/255, 67f/255) },
        { "LoadingProgress.StartupImpact.LoadDefs", new Color(67f/255, 84f/255, 156f/255)},
        { "LoadingProgress.StartupImpact.CombineXml", new Color(130f/255, 130f/255, 130f/255)},
        { "LoadingProgress.StartupImpact.TKeySystemParse", new Color(84f/255, 207f/255, 154f/255)},
        { "LoadingProgress.StartupImpact.ErrorCheckPatches", new Color(72f/255, 121f/255, 175f/255)},
        { "LoadingProgress.StartupImpact.LoadPatches", new Color(136f/255, 156f/255, 67f/255)},
        { "LoadingProgress.StartupImpact.ApplyPatches", new Color(156f/255, 67f/255, 121f/255)},
        { "LoadingProgress.StartupImpact.RegisterXmlInheritance", new Color(176f/255, 223f/255, 224f/255)},
        { "LoadingProgress.StartupImpact.ResolveXmlInheritance", new Color(82f/255, 26f/255, 106f/255)},
        { "LoadingProgress.StartupImpact.ClearCachedPatches", new Color(63f/255, 109f/255, 125f/255) },
        { "LoadingProgress.StartupImpact.ClearCachedXmlInheritance", new Color(118f/255, 136f/255, 92f/255) },
        { "LoadingProgress.StartupImpact.LanguageDatabaseInitAllMetadata", new Color(158f/255, 92f/255, 93f/255) },
        { "LoadingProgress.StartupImpact.DefDatabaseAddAllInMods", new Color(168f/255, 99f/255, 64f/255) },
        { "LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.NonImplied", new Color(94f/255, 122f/255, 151f/255) },
        { "LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Early", new Color(137f/255, 121f/255, 161f/255) },
        { "LoadingProgress.StartupImpact.DefOfHelperRebindAllDefOfs.Final", new Color(28f/255, 76f/255, 84f/255) },
        { "LoadingProgress.StartupImpact.TKeySystemBuildMappings", new Color(182f/255, 168f/255, 119f/255) },
        { "LoadingProgress.StartupImpact.BackStoryTranslationUtilityLoadAndInjectBackstoryData", new Color(60f/255, 49f/255, 109f/255) },
        { "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AudioClips", new Color(147f/255, 170f/255, 143f/255) },
        { "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.Textures", new Color(157f/255, 140f/255, 104f/255) },
        { "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.Strings", new Color(92f/255, 86f/255, 82f/255) },
        { "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AssetBundles", new Color(163f/255, 155f/255, 110f/255) },
        { "LoadingProgress.StartupImpact.LoadedLanguageInjectIntoDataBeforeImpliedDefs", new Color(122f/255, 71f/255, 122f/255) },
        { "LoadingProgress.StartupImpact.ColoredTextResetStaticData", new Color(169f/255, 142f/255, 172f/255) },
        { "LoadingProgress.StartupImpact.DefGeneratorGenerateImpliedDefsPreResolve", new Color(148f/255, 87f/255, 58f/255) },
        { "LoadingProgress.StartupImpact.ResolveAllWantedCrossReferences.Implied", new Color(145f/255, 106f/255, 75f/255) },
        { "LoadingProgress.StartupImpact.PlayDataLoaderResetStaticDataPre", new Color(189f/255, 171f/255, 133f/255) },
        { "LoadingProgress.StartupImpact.ResolveReferences", new Color(96f/255, 126f/255, 110f/255) },
        { "LoadingProgress.StartupImpact.DefGeneratorGenerateImpliedDefsPostResolve", new Color(76f/255, 104f/255, 132f/255) },
        { "LoadingProgress.StartupImpact.PlayDataLoaderResetStaticDataPost", new Color(164f/255, 189f/255, 208f/255) },
        { "LoadingProgress.StartupImpact.ErrorCheckAllDefs", new Color(157f/255, 140f/255, 104f/255) },
        { "LoadingProgress.StartupImpact.KeyPrefsInit", new Color(133f/255, 105f/255, 128f/255) },
        { "LoadingProgress.StartupImpact.ShortHashGiverGiveAllShortHashes", new Color(176f/255, 157f/255, 147f/255) },
        { "LoadingProgress.StartupImpact.SolidBioDatabaseLoadAllBios", new Color(86f/255, 98f/255, 136f/255) },
        { "LoadingProgress.StartupImpact.LoadedLanguageInjectIntoDataAfterImpliedDefs", new Color(142f/255, 153f/255, 170f/255) },
        { "LoadingProgress.StartupImpact.StaticConstructorOnStartupUtilityCallAll", new Color(171f/255, 114f/255, 131f/255) },
        { "LoadingProgress.StartupImpact.FloatMenuMakerMapInit", new Color(120f/255, 108f/255, 86f/255) },
        { "LoadingProgress.StartupImpact.GlobalTextureAtlasManagerBakeStaticAtlases", new Color(131f/255, 88f/255, 96f/255) },
        { "LoadingProgress.StartupImpact.AbstractFilesystemClearAllCache", new Color(114f/255, 99f/255, 143f/255) },

        // { "extra-30", new Color(92f/255, 86f/255, 82f/255) },

        { "LoadingProgress.StartupImpact.Total.Mods", new Color(175f/255, 126f/255, 72f/255)},
        { "LoadingProgress.StartupImpact.Total.ModsHidden", new Color(103f/255, 83f/255, 61f/255)},
        { "LoadingProgress.StartupImpact.Total.BaseGame", new Color(72f/255, 121f/255, 175f/255)},
        { "LoadingProgress.StartupImpact.Total.Others", new Color(35f/255, 50f/255, 84f/255)},
    };
    private static readonly Color DefaultColor = new(128f / 255f, 128f / 255f, 128f / 255f);

    private bool _useLogScale;
    private UiTable _table;
    private StartupImpactSessionData _sessionData;
    private StartupImpactSessionViewData _sessionViewData;

    // Set window width to 800 and height to the lesser of 800 or 75% of the screen height
    public override Vector2 InitialSize => new(800f, Math.Min(800f, UI.screenHeight * 0.75f));

    private string _statusText = "";
    private double? _statusTextSetTime;
    private const float StatusTextDisplayTimeSeconds = 5f;
    private string StatusText
    {
        get => _statusText;
        set
        {
            _statusText = value;
            if (!string.IsNullOrEmpty(value))
            {
                _statusTextSetTime = Time.realtimeSinceStartup;
            }
        }
    }

    public DialogStartupImpact()
    {
        _sessionData = StartupImpactSessionData.FromCurrentSession();
        Initialize();

        if (!LoadingProgressMod.Settings.TrackStartupLoadingImpact)
        {
            _statusText = "LoadingProgress.StartupImpact.Disabled".Translate();
        }
    }

    [MemberNotNull([nameof(_sessionViewData), nameof(_table)])]
    private void Initialize()
    {
        _sessionViewData = new StartupImpactSessionViewData(_sessionData);
        _table = new UiTable(_sessionData.Mods.Count, 40, [-40, 30, -80, 38]);
    }


    public override void DoWindowContents(Rect area)
    {
        float y = 0;

        // Log scale checkbox (top right)
        Text.Anchor = TextAnchor.MiddleRight;
        var label = "LoadingProgress.StartupImpact.LogarithmicScale".Translate();
        var checkboxWidth = Text.CalcSize(label).x + 24f + OuterSpacing;
        var checkboxRect = new Rect(area.width - checkboxWidth, y, checkboxWidth, CheckboxHeight);
        Widgets.CheckboxLabeled(checkboxRect, label, ref _useLogScale);
        TooltipHandler.TipRegion(checkboxRect, "LoadingProgress.StartupImpact.LogarithmicScale.Tip".Translate());

        Text.Anchor = TextAnchor.MiddleLeft;
        Text.Font = GameFont.Medium;

        var profilerBar = new ProfilerBar()
        {
            UseLogScale = _useLogScale,
            ProgressBarPadding = 2f,
            DefaultColor = DefaultColor,
        };

        Rect titleRect = new(0, y, area.width, TitleHeight);
        Widgets.Label(titleRect, "LoadingProgress.StartupImpact.StartupTime".Translate(ProfilerBar.TimeText(_sessionData.LoadingTime)));
        y += titleRect.height;

        Rect profileRect = new(0, y, area.width, BarHeight);
        profilerBar.Draw(profileRect, _sessionViewData.MetricsTotal, StartupImpactSessionViewData.CategoriesTotal, _sessionData.LoadingTime, CategoryColors);
        y += profileRect.height + InnerSpacing;

        Rect nonmodsTitleRect = new(0, y, area.width, TitleHeight);
        Widgets.Label(nonmodsTitleRect, "LoadingProgress.StartupImpact.StartupNonmods".Translate(ProfilerBar.TimeText(_sessionViewData.BasegameLoadingTime)));
        y += nonmodsTitleRect.height;

        Rect nonmodsProfileRect = new(0, y, area.width, BarHeight);
        profilerBar.Draw(nonmodsProfileRect, _sessionViewData.MetricsNonMods, _sessionViewData.CategoriesNonMods, _sessionViewData.BasegameLoadingTime, _sessionViewData.CategoryColorsNonMods);
        y += nonmodsProfileRect.height + OuterSpacing;

        Rect modsTitleRect = new(0, y, area.width, TitleHeight);
        Widgets.Label(modsTitleRect, "LoadingProgress.StartupImpact.StartupMods".Translate(ProfilerBar.TimeText(_sessionViewData.ModsLoadingTime)));
        y += modsTitleRect.height + OuterSpacing;
        Text.Font = GameFont.Small;

        var bottomOffset = ButtonHeight + OuterSpacing + InnerSpacing; // Button height + spacing + padding
        _table.StartTable(0, y, area.width, area.height - y - bottomOffset);

        var row = 0;
        foreach (var info in _sessionViewData.ModViewData)
        {
            if (_table.IsRowVisible(row))
            {
                if (Widgets.ButtonImage(_table.Cell(0, row), Textures.Eye, info.HideInUi ? Color.white : Color.grey, tooltip: "LoadingProgress.StartupImpact.ToggleModVisibility.Tip".Translate()))
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
                    profilerBar.Draw(rect2, info.OffThreadMetrics, _sessionViewData.Categories, Math.Max(_sessionViewData.MaxImpact, info.ModData.OffThreadTotalImpact), CategoryColors);
                }
                profilerBar.Draw(rect, info.Metrics, _sessionViewData.Categories, Math.Max(_sessionViewData.MaxImpact, info.ModData.TotalImpact), CategoryColors);
            }
            row++;
        }

        _table.EndTable();

        GUI.color = Color.white;
        var x = area.width - ((ButtonWidth + OuterSpacing) * 3);
        var yBtn = area.height - ButtonHeight - 3f;

        HandleStatus(area, yBtn);
        if (Widgets.ButtonText(new Rect(x, yBtn, ButtonWidth, ButtonHeight), "Save".Translate()))
        {
            try
            {
                SaveSessionData();
                StatusText = "LoadingProgress.StartupImpact.Saved".Translate();
            }
            catch (Exception ex)
            {
                StatusText = "LoadingProgress.StartupImpact.SaveFailed".Translate(ex.Message);
            }
        }
        x += ButtonWidth + OuterSpacing;
        if (Widgets.ButtonText(new Rect(x, yBtn, ButtonWidth, ButtonHeight), "Load".Translate()))
        {
            try
            {
                var newSessionData = LoadSessionData();
                if (newSessionData != null)
                {
                    _sessionData = newSessionData;
                    Initialize();
                    StatusText = "LoadingProgress.StartupImpact.Loaded".Translate();
                }
                else
                {
                    StatusText = "LoadingProgress.StartupImpact.LoadFailedNoData".Translate();
                }
            }
            catch (Exception ex)
            {
                StatusText = "LoadingProgress.StartupImpact.LoadFailed".Translate(ex.Message);
            }
        }
        x += ButtonWidth + OuterSpacing;
        if (Widgets.ButtonText(new Rect(x, yBtn, ButtonWidth, ButtonHeight), "Close".Translate(), true, false, true))
        {
            Close();
        }
        Text.Anchor = TextAnchor.UpperLeft;

        void HandleStatus(Rect area, float yBtn)
        {
            // Show SaveStatus if set, and clear after timeout
            if (_statusTextSetTime.HasValue && Time.realtimeSinceStartup - _statusTextSetTime.Value > StatusTextDisplayTimeSeconds)
            {
                StatusText = "";
            }
            else if (!string.IsNullOrEmpty(StatusText))
            {
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(0, yBtn, area.width - ((ButtonWidth + OuterSpacing) * 3) - OuterSpacing, ButtonHeight), (TaggedString)StatusText);
            }
        }
    }

    private const string SaveFileName = "StartupImpactData.xml";
    private const string SaveLabel = "sessionData";

    private static string SaveFilePath => Path.Combine(GenFilePaths.SaveDataFolderPath, GenText.SanitizeFilename(SaveFileName));

    private void SaveSessionData()
    {
        Scribe.saver.InitSaving(SaveFilePath, "StartupImpactSession");
        Scribe_Deep.Look(ref _sessionData, SaveLabel);
        Scribe.saver.FinalizeSaving();
    }

    private static StartupImpactSessionData? LoadSessionData()
    {
        StartupImpactSessionData? sessionData = null;
        if (File.Exists(SaveFilePath))
        {
            Scribe.loader.InitLoading(SaveFilePath);
            Scribe_Deep.Look(ref sessionData, SaveLabel);
            Scribe.loader.FinalizeLoading();
        }
        return sessionData;
    }


    [StaticConstructorOnStartup]
    private static class Textures
    {
        public static readonly Texture2D Eye = ContentFinder<Texture2D>.Get("LP_Eye");
    }
}
