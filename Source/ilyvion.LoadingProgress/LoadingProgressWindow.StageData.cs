namespace ilyvion.LoadingProgress;

public enum LoadingStage
{
    Initializing,
    LoadingModClasses,
    LoadModXml,
    CombineIntoUnifiedXml,
    TKeySystemParse,
    ErrorCheckPatches,
    ApplyPatches,
    ParseAndProcessXml,
    XmlInheritanceResolve,
    LoadingDefs,
    ClearCachedPatches,
    XmlInheritanceClear,
    LoadLanguageMetadata,
    LoadLanguage,
    CopyAllDefsToGlobalDatabases,
    ResolveCrossReferencesBetweenNonImpliedDefs,
    RebindDefOfsEarly,
    TKeySystemBuildMappings,
    LegacyBackstoryTranslations,
    InjectSelectedLanguageDataEarly,
    GlobalOperationsEarly,
    ResolveCrossReferencesBetweenImpliedDefs,
    RebindDefOfsFinal,
    OtherOperationsPreResolve,
    ResolveReferences,
    OtherOperationsPostResolve,
    ErrorCheckAllDefs,
    ShortHashGiving,
    LoadingAllBios,
    InjectSelectedLanguageDataIntoGameData,
    StaticConstructorOnStartupCallAll,
    AtlasBaking,
    GarbageCollection,
    Finished,
}

public partial class LoadingProgressWindow
{
    private delegate bool StagePredicate(string value);
    private delegate void StageAction(string value);
    private delegate string StageDisplayLabel(string value);

    private record StageRule(
        StagePredicate Predicate,
        StageAction Action,
        LoadingStage Stage,
        StageDisplayLabel? CustomLabel = null
    );

    private static string? GetStageTranslation(LoadingStage stage, params NamedArgument[] args)
    {
        return LanguageDatabase.activeLanguage is null
            ? null
            : $"LoadingProgress.Stage.{stage}.Text".Translate(args);
    }

    private static readonly List<StageRule> StageRules =
    [
        new(
            value => false,
            value => {},
            LoadingStage.Initializing,
            activity => "Initializing..."
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadingModClasses && value.StartsWith("Loading ") && value.EndsWith(" mod class"),
            value => {
                CurrentStage = LoadingStage.LoadingModClasses;
                _currentLoadingActivity = value.Substring("Loading ".Length, value.Length - "Loading ".Length - " mod class".Length);
            },
            LoadingStage.LoadingModClasses,
            activity => $"Loading mod classes... (<i>{activity}</i>)"
        ),
        new(
            value => CurrentStage == LoadingStage.LoadingModClasses && value == "LoadModXML()",
            value => {
                CurrentStage = LoadingStage.LoadModXml;
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.LoadModXml,
            activity => $"Loading mod XML... (<i>{activity}</i>)"
        ),
        new(
            value => CurrentStage == LoadingStage.LoadModXml && value.StartsWith("Loading ") && !value.StartsWith("Loading asset"),
            value => {
                _currentLoadingActivity = value["Loading ".Length..];
            },
            LoadingStage.LoadModXml,
            activity => $"Loading mod XML... (<i>{activity}</i>)"
        ),
        new(
            value => CurrentStage == LoadingStage.LoadModXml && value == "CombineIntoUnifiedXML()",
            value => {
                CurrentStage = LoadingStage.CombineIntoUnifiedXml;
            },
            LoadingStage.CombineIntoUnifiedXml,
            activity => "Combining XML..."
        ),
        new(
            value => CurrentStage == LoadingStage.CombineIntoUnifiedXml && value == "TKeySystem.Parse()",
            value => {
                CurrentStage = LoadingStage.TKeySystemParse;
            },
            LoadingStage.TKeySystemParse,
            activity => "Parsing Translation Key system..."
        ),
        new(
            value => CurrentStage == LoadingStage.TKeySystemParse && value == "ErrorCheckPatches()",
            value => {
                CurrentStage = LoadingStage.ErrorCheckPatches;
            },
            LoadingStage.ErrorCheckPatches,
            activity => "Checking XML patches for errors..."
        ),
        new(
            value => CurrentStage == LoadingStage.ErrorCheckPatches && value == "ApplyPatches()",
            value => {
                CurrentStage = LoadingStage.ApplyPatches;
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.ApplyPatches,
            activity => $"Applying XML patches... (<i>{activity}</i>)"
        ),
        new(
            value => CurrentStage == LoadingStage.ApplyPatches && value.EndsWith(" Worker"),
            value => {
                _currentLoadingActivity = value[..^" Worker".Length];
            },
            LoadingStage.ApplyPatches,
            activity => $"Applying XML patches... (<i>{activity}</i>)"
        ),
        new(
            value => CurrentStage == LoadingStage.ApplyPatches && value == "ParseAndProcessXML()",
            value => {
                CurrentStage = LoadingStage.ParseAndProcessXml;
            },
            LoadingStage.ParseAndProcessXml,
            activity => "Parsing and processing XML..."
        ),
        new(
            value => CurrentStage == LoadingStage.ParseAndProcessXml && value == "XmlInheritance.Resolve()",
            value => {
                CurrentStage = LoadingStage.XmlInheritanceResolve;
            },
            LoadingStage.XmlInheritanceResolve,
            activity => "Resolving XML inheritance..."
        ),
        new(
            value => CurrentStage == LoadingStage.XmlInheritanceResolve && value.StartsWith("Loading defs for "),
            value => {
                CurrentStage = LoadingStage.LoadingDefs;
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.LoadingDefs,
            activity => $"Loading defs... (<i>{activity}</i>)"
        ),
        new(
            value => CurrentStage == LoadingStage.LoadingDefs && value.StartsWith("ParseValueAndReturnDef "),
            value => {
                _currentLoadingActivity = value["ParseValueAndReturnDef (for ".Length..][..^1];
            },
            LoadingStage.LoadingDefs,
            activity => $"Loading defs... (<i>{activity}</i>)"
        ),
        new(
            value => CurrentStage == LoadingStage.LoadingDefs && value == "ClearCachedPatches()",
            value => {
                CurrentStage = LoadingStage.ClearCachedPatches;
            },
            LoadingStage.ClearCachedPatches
        ),
        new(
            value => CurrentStage == LoadingStage.ClearCachedPatches && value == "XmlInheritance.Clear()",
            value => {
                CurrentStage = LoadingStage.XmlInheritanceClear;
            },
            LoadingStage.XmlInheritanceClear
        ),
        new(
            value => CurrentStage == LoadingStage.XmlInheritanceClear && value == "Load language metadata.",
            value => {
                CurrentStage = LoadingStage.LoadLanguageMetadata;
            },
            LoadingStage.LoadLanguageMetadata
        ),
        new(
            value => CurrentStage == LoadingStage.LoadLanguageMetadata && value.StartsWith("Loading language data:"),
            value => {
                CurrentStage = LoadingStage.LoadLanguage;
                _currentLoadingActivity = value["Loading language data: ".Length..];
            },
            LoadingStage.LoadLanguage
        ),
        new(
            value => CurrentStage == LoadingStage.LoadLanguage && value == "Copy all Defs from mods to global databases.",
            value => {
                CurrentStage = LoadingStage.CopyAllDefsToGlobalDatabases;
            },
            LoadingStage.CopyAllDefsToGlobalDatabases
        ),
        new(
            value => CurrentStage == LoadingStage.CopyAllDefsToGlobalDatabases && value == "Resolve cross-references between non-implied Defs.",
            value => {
                CurrentStage = LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefs;
            },
            LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefs
        ),
        new(
            value => CurrentStage == LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefs && value == "Rebind DefOfs (early).",
            value => {
                CurrentStage = LoadingStage.RebindDefOfsEarly;
            },
            LoadingStage.RebindDefOfsEarly
        ),
        new(
            value => CurrentStage == LoadingStage.RebindDefOfsEarly && value == "TKeySystem.BuildMappings()",
            value => {
                CurrentStage = LoadingStage.TKeySystemBuildMappings;
            },
            LoadingStage.TKeySystemBuildMappings
        ),
        new(
            value => CurrentStage == LoadingStage.TKeySystemBuildMappings && value == "Legacy backstory translations.",
            value => {
                CurrentStage = LoadingStage.LegacyBackstoryTranslations;
            },
            LoadingStage.LegacyBackstoryTranslations
        ),
        new(
            value => CurrentStage == LoadingStage.LegacyBackstoryTranslations && value == "Inject selected language data into game data (early pass).",
            value => {
                CurrentStage = LoadingStage.InjectSelectedLanguageDataEarly;
            },
            LoadingStage.InjectSelectedLanguageDataEarly
        ),
        new(
            value => CurrentStage == LoadingStage.InjectSelectedLanguageDataEarly && value == "Global operations (early pass).",
            value => {
                CurrentStage = LoadingStage.GlobalOperationsEarly;
            },
            LoadingStage.GlobalOperationsEarly
        ),
        new(
            value => CurrentStage == LoadingStage.GlobalOperationsEarly && value == "Resolve cross-references between Defs made by the implied defs.",
            value => {
                CurrentStage = LoadingStage.ResolveCrossReferencesBetweenImpliedDefs;
            },
            LoadingStage.ResolveCrossReferencesBetweenImpliedDefs
        ),
        new(
            value => CurrentStage == LoadingStage.ResolveCrossReferencesBetweenImpliedDefs && value == "Rebind DefOfs (final).",
            value => {
                CurrentStage = LoadingStage.RebindDefOfsFinal;
            },
            LoadingStage.RebindDefOfsFinal
        ),
        new(
            value => CurrentStage == LoadingStage.RebindDefOfsFinal && value == "Other def binding, resetting and global operations (pre-resolve).",
            value => {
                CurrentStage = LoadingStage.OtherOperationsPreResolve;
            },
            LoadingStage.OtherOperationsPreResolve
        ),
        new(
            value => CurrentStage == LoadingStage.OtherOperationsPreResolve && value == "Resolve references.",
            value => {
                CurrentStage = LoadingStage.ResolveReferences;
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.ResolveReferences
        ),
        new(
            value => CurrentStage == LoadingStage.ResolveReferences && value.StartsWith("ResolveAllReferences "),
            value => {
                _currentLoadingActivity = value["ResolveAllReferences ".Length..];
            },
            LoadingStage.ResolveReferences
        ),
        new(
            value => CurrentStage == LoadingStage.ResolveReferences && value == "Other def binding, resetting and global operations (post-resolve).",
            value => {
                CurrentStage = LoadingStage.OtherOperationsPostResolve;
            },
            LoadingStage.OtherOperationsPostResolve
        ),
        new(
            value => CurrentStage == LoadingStage.OtherOperationsPostResolve && value == "Error check all defs.",
            value => {
                CurrentStage = LoadingStage.ErrorCheckAllDefs;
            },
            LoadingStage.ErrorCheckAllDefs
        ),
        new(
            value => CurrentStage == LoadingStage.ErrorCheckAllDefs && value == "Short hash giving.",
            value => {
                CurrentStage = LoadingStage.ShortHashGiving;
            },
            LoadingStage.ShortHashGiving
        ),
        new(
            value => CurrentStage == LoadingStage.ShortHashGiving && value == "Load all bios",
            value => {
                CurrentStage = LoadingStage.LoadingAllBios;
            },
            LoadingStage.LoadingAllBios
        ),
        new(
            value => CurrentStage == LoadingStage.LoadingAllBios && value == "Inject selected language data into game data.",
            value => {
                CurrentStage = LoadingStage.InjectSelectedLanguageDataIntoGameData;
            },
            LoadingStage.InjectSelectedLanguageDataIntoGameData
        ),
        new(
            value => CurrentStage == LoadingStage.InjectSelectedLanguageDataIntoGameData && value == "Static constructor calls",
            value => {
                CurrentStage = LoadingStage.StaticConstructorOnStartupCallAll;
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.StaticConstructorOnStartupCallAll
        ),
        new(
            value => CurrentStage == LoadingStage.StaticConstructorOnStartupCallAll && value == "Atlas baking.",
            value => {
                CurrentStage = LoadingStage.AtlasBaking;
            },
            LoadingStage.AtlasBaking
        ),
        new(
            value => CurrentStage == LoadingStage.AtlasBaking && value == "Garbage Collection",
            value => {
                CurrentStage = LoadingStage.GarbageCollection;
            },
            LoadingStage.GarbageCollection
        ),
        new(
            value => CurrentStage == LoadingStage.GarbageCollection && value == "Misc Init (InitializingInterface)",
            value => {
                CurrentStage = LoadingStage.Finished;
            },
            LoadingStage.Finished,
            _ => string.Empty
        )
    ];

    public static LoadingStage CurrentStage { get; private set; } = LoadingStage.LoadingModClasses;

    private static string _currentLoadingActivity = string.Empty;
    internal static void SetCurrentLoadingActivityRaw(string value)
    {
        _currentLoadingActivity = value;
    }

    internal static string CurrentLoadingActivity
    {
        get => _currentLoadingActivity;
        set
        {
            foreach (var rule in StageRules)
            {
                if (rule.Predicate(value))
                {
                    rule.Action(value);
                    return;
                }
            }
        }
    }
}