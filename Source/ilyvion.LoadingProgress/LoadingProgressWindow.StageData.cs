using System.IO;

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
    GenerateImpliedDefs,
    ResolveCrossReferencesBetweenImpliedDefs,
    RebindDefOfsFinal,
    OtherOperationsPreResolve,
    ResolveReferences,
    OtherOperationsPostResolve,
    ErrorCheckAllDefs,
    ShortHashGiving,
    ExecuteToExecuteWhenFinished,
    LoadingAllBios,
    InjectSelectedLanguageDataIntoGameData,
    StaticConstructorOnStartupCallAll,
    ExecuteToExecuteWhenFinished2,
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

    private static Dictionary<string, string>? Translations = null;
    private static string? GetStageTranslation(LoadingStage stage, params object[] args)
    {
        if (Translations is not null)
        {
            if (Translations.TryGetValue($"LoadingProgress.Stage.{stage}", out var translation))
            {
                return string.Format(translation, args);
            }
            else
            {
                Log.Warning($"No translation for LoadingProgress.Stage.{stage}");
                return $"LoadingProgress.Stage.{stage}";
            }
        }

        var stageTranslations = LoadingProgressMod.instance.Content.RootDir + "/Common/Languages/English/Keyed/Stages.xml";
        var stageTranslationsContent = File.ReadAllText(stageTranslations);
        Translations = DirectXmlLoaderSimple.ValuesFromXmlFile(stageTranslationsContent).ToDictionary(x => x.key, x => x.value);

        return GetStageTranslation(stage, args);
    }

    private static string? GetStageTranslationWithSecondary(LoadingStage stage, string secondary, params object[] args)
    {
        if (Translations is not null)
        {
            if (Translations.TryGetValue($"LoadingProgress.Stage.{stage}.{secondary}", out var translation))
            {
                try
                {
                    return string.Format(translation, args);
                }
                catch (Exception e)
                {
                    Log.Error($"Error formatting translation for LoadingProgress.Stage.{stage}.{secondary} with translation {translation} and args: {string.Join(", ", args)}.\nException was: {e}");
                    return $"LoadingProgress.Stage.{stage}.{secondary}";
                }
            }
            else
            {
                Log.Warning($"No translation for LoadingProgress.Stage.{stage}.{secondary}");
                return $"LoadingProgress.Stage.{stage}.{secondary}";
            }
        }

        var stageTranslations = LoadingProgressMod.instance.Content.RootDir + "/Common/Languages/English/Keyed/Stages.xml";
        var stageTranslationsContent = File.ReadAllText(stageTranslations);
        Translations = DirectXmlLoaderSimple.ValuesFromXmlFile(stageTranslationsContent).ToDictionary(x => x.key, x => x.value);

        return GetStageTranslationWithSecondary(stage, secondary, args);
    }

    private static readonly List<StageRule> StageRules =
    [
        new(
            value => false,
            value => {},
            LoadingStage.Initializing
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadingModClasses && value.StartsWith("Loading ") && value.EndsWith(" mod class"),
            value => {
                CurrentStage = LoadingStage.LoadingModClasses;
                if (StageProgress is (float current, float max))
                {
                    StageProgress = ((int)current + 1, max);
                }
                else
                {
                    StageProgress = (1,  typeof(Mod).InstantiableDescendantsAndSelf().Count());
                }
                _currentLoadingActivity = value.Substring("Loading ".Length, value.Length - "Loading ".Length - " mod class".Length);
            },
            LoadingStage.LoadingModClasses
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadingModClasses && value == "LoadModXML()",
            value => {
                CurrentStage = LoadingStage.LoadModXml;
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.LoadModXml
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadModXml && value.StartsWith("Loading ") && !value.StartsWith("Loading asset"),
            value => {
                if (StageProgress is (float current, float max))
                {
                    StageProgress = ((int)current + 1, max);
                }
                else
                {
                    StageProgress = (1, LoadedModManager.RunningModsListForReading.Count);
                }
                _currentLoadingActivity = value["Loading ".Length..];
            },
            LoadingStage.LoadModXml
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadModXml && value == "CombineIntoUnifiedXML()",
            value =>
            {
                CurrentStage = LoadingStage.CombineIntoUnifiedXml;
            },
            LoadingStage.CombineIntoUnifiedXml
        ),
        new(
            value => CurrentStage <= LoadingStage.CombineIntoUnifiedXml && value == "TKeySystem.Parse()",
            value =>
            {
                CurrentStage = LoadingStage.TKeySystemParse;
            },
            LoadingStage.TKeySystemParse
        ),
        new(
            value => CurrentStage <= LoadingStage.TKeySystemParse && value == "ErrorCheckPatches()",
            value =>
            {
                CurrentStage = LoadingStage.ErrorCheckPatches;
            },
            LoadingStage.ErrorCheckPatches
        ),
        new(
            value => CurrentStage <= LoadingStage.ErrorCheckPatches && value == "ApplyPatches()",
            value =>
            {
                CurrentStage = LoadingStage.ApplyPatches;
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.ApplyPatches
        ),
        new(
            value => CurrentStage <= LoadingStage.ApplyPatches && value.EndsWith(" Worker"),
            value =>
            {
                if (StageProgress is (float current, float max))
                {
                    StageProgress = ((int)current + 1, max);
                }
                else
                {
                    StageProgress = (1, LoadedModManager.RunningMods.SelectMany(rm => rm.Patches).Count());
                }
                _currentLoadingActivity = value[..^" Worker".Length];
            },
            LoadingStage.ApplyPatches
        ),
        new(
            value => CurrentStage <= LoadingStage.ApplyPatches && value == "ParseAndProcessXML()",
            value =>
            {
                CurrentStage = LoadingStage.ParseAndProcessXml;
            },
            LoadingStage.ParseAndProcessXml
        ),
        new(
            value => CurrentStage <= LoadingStage.ParseAndProcessXml && value == "XmlInheritance.Resolve()",
            value =>
            {
                CurrentStage = LoadingStage.XmlInheritanceResolve;
            },
            LoadingStage.XmlInheritanceResolve
        ),
        new(
            value => CurrentStage <= LoadingStage.XmlInheritanceResolve && value.StartsWith("Loading defs for "),
            value =>
            {
                CurrentStage = LoadingStage.LoadingDefs;
                var valueCount = value["Loading defs for ".Length..];
                var spaceIndex = valueCount.IndexOf(' ');
                if (spaceIndex >= 0)
                {
                    var count = int.Parse(valueCount[..spaceIndex]);
                    StageProgress = (0, count);
                }
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.LoadingDefs
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadingDefs && value.StartsWith("ParseValueAndReturnDef "),
            value =>
            {
                if (StageProgress is (float current, float max))
                {
                    StageProgress = ((int)current + 1, max);
                }
                _currentLoadingActivity = value["ParseValueAndReturnDef (for ".Length..][..^1];
            },
            LoadingStage.LoadingDefs
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadingDefs && value == "ClearCachedPatches()",
            value =>
            {
                CurrentStage = LoadingStage.ClearCachedPatches;
            },
            LoadingStage.ClearCachedPatches
        ),
        new(
            value => CurrentStage <= LoadingStage.ClearCachedPatches && value == "XmlInheritance.Clear()",
            value =>
            {
                CurrentStage = LoadingStage.XmlInheritanceClear;
            },
            LoadingStage.XmlInheritanceClear
        ),
        new(
            value => CurrentStage <= LoadingStage.XmlInheritanceClear && value == "Load language metadata.",
            value =>
            {
                CurrentStage = LoadingStage.LoadLanguageMetadata;
            },
            LoadingStage.LoadLanguageMetadata
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadLanguageMetadata && value.StartsWith("Loading language data:"),
            value =>
            {
                CurrentStage = LoadingStage.LoadLanguage;
                _currentLoadingActivity = value["Loading language data: ".Length..];
            },
            LoadingStage.LoadLanguage
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadLanguage && value == "Copy all Defs from mods to global databases.",
            value =>
            {
                CurrentStage = LoadingStage.CopyAllDefsToGlobalDatabases;
            },
            LoadingStage.CopyAllDefsToGlobalDatabases
        ),
        new(
            value => CurrentStage <= LoadingStage.CopyAllDefsToGlobalDatabases && value == "Resolve cross-references between non-implied Defs.",
            value =>
            {
                CurrentStage = LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefs;
            },
            LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefs
        ),
        new(
            value => CurrentStage <= LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefs && value == "Rebind DefOfs (early).",
            value =>
            {
                CurrentStage = LoadingStage.RebindDefOfsEarly;
            },
            LoadingStage.RebindDefOfsEarly
        ),
        new(
            value => CurrentStage <= LoadingStage.RebindDefOfsEarly && value == "TKeySystem.BuildMappings()",
            value =>
            {
                CurrentStage = LoadingStage.TKeySystemBuildMappings;
            },
            LoadingStage.TKeySystemBuildMappings
        ),
        new(
            value => CurrentStage <= LoadingStage.TKeySystemBuildMappings && value == "Legacy backstory translations.",
            value =>
            {
                CurrentStage = LoadingStage.LegacyBackstoryTranslations;
            },
            LoadingStage.LegacyBackstoryTranslations
        ),
        new(
            value => CurrentStage <= LoadingStage.LegacyBackstoryTranslations && value == "Inject selected language data into game data (early pass).",
            value =>
            {
                CurrentStage = LoadingStage.InjectSelectedLanguageDataEarly;
            },
            LoadingStage.InjectSelectedLanguageDataEarly
        ),
        new(
            value => CurrentStage <= LoadingStage.InjectSelectedLanguageDataEarly && value == "Global operations (early pass).",
            value =>
            {
                CurrentStage = LoadingStage.GlobalOperationsEarly;
            },
            LoadingStage.GlobalOperationsEarly
        ),
        new(
            value => CurrentStage <= LoadingStage.GlobalOperationsEarly && value == "Generate implied Defs (pre-resolve).",
            value =>
            {
                CurrentStage = LoadingStage.GenerateImpliedDefs;
            },
            LoadingStage.GenerateImpliedDefs
        ),
        new(
            value => CurrentStage <= LoadingStage.GenerateImpliedDefs && value == "Resolve cross-references between Defs made by the implied defs.",
            value =>
            {
                CurrentStage = LoadingStage.ResolveCrossReferencesBetweenImpliedDefs;
            },
            LoadingStage.ResolveCrossReferencesBetweenImpliedDefs
        ),
        new(
            value => CurrentStage <= LoadingStage.ResolveCrossReferencesBetweenImpliedDefs && value == "Rebind DefOfs (final).",
            value =>
            {
                CurrentStage = LoadingStage.RebindDefOfsFinal;
            },
            LoadingStage.RebindDefOfsFinal
        ),
        new(
            value => CurrentStage <= LoadingStage.RebindDefOfsFinal && value == "Other def binding, resetting and global operations (pre-resolve).",
            value =>
            {
                CurrentStage = LoadingStage.OtherOperationsPreResolve;
            },
            LoadingStage.OtherOperationsPreResolve
        ),
        new(
            value => CurrentStage <= LoadingStage.OtherOperationsPreResolve && value == "Resolve references.",
            value =>
            {
                CurrentStage = LoadingStage.ResolveReferences;
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.ResolveReferences
        ),
        new(
            value => CurrentStage <= LoadingStage.ResolveReferences && value.StartsWith("ResolveAllReferences "),
            value =>
            {
                _currentLoadingActivity = value["ResolveAllReferences ".Length..];
            },
            LoadingStage.ResolveReferences
        ),
        new(
            value => CurrentStage <= LoadingStage.ResolveReferences && value == "Other def binding, resetting and global operations (post-resolve).",
            value =>
            {
                CurrentStage = LoadingStage.OtherOperationsPostResolve;
            },
            LoadingStage.OtherOperationsPostResolve
        ),
        new(
            value => CurrentStage <= LoadingStage.OtherOperationsPostResolve && value == "Error check all defs.",
            value =>
            {
                CurrentStage = LoadingStage.ErrorCheckAllDefs;
            },
            LoadingStage.ErrorCheckAllDefs
        ),
        new(
            value => CurrentStage <= LoadingStage.ErrorCheckAllDefs && value == "Short hash giving.",
            value =>
            {
                CurrentStage = LoadingStage.ShortHashGiving;
            },
            LoadingStage.ShortHashGiving
        ),
        new(
            value => CurrentStage <= LoadingStage.ShortHashGiving && (value == "ExecuteToExecuteWhenFinished()"),
            value =>
            {
                CurrentStage = LoadingStage.ExecuteToExecuteWhenFinished;
            },
            LoadingStage.ExecuteToExecuteWhenFinished
        ),
        new(
            value => CurrentStage == LoadingStage.ExecuteToExecuteWhenFinished && value.StartsWith("Reload "),
            value =>
            {
                _currentLoadingActivity = value["Reload ".Length..];
            },
            LoadingStage.ExecuteToExecuteWhenFinished,
            activity => GetStageTranslationWithSecondary(
                LoadingStage.ExecuteToExecuteWhenFinished,
                "Reloading",
                activity,
                ModContentPack_ReloadContentInt_Patches.CurrentMod ?? "")!
        ),
        new(
            value => CurrentStage == LoadingStage.ExecuteToExecuteWhenFinished && value.Contains(" -> "),
            value =>
            {
                _currentLoadingActivity = value;
            },
            LoadingStage.ExecuteToExecuteWhenFinished
        ),
        new(
            value => CurrentStage <= LoadingStage.ExecuteToExecuteWhenFinished && value == "Load all bios",
            value =>
            {
                CurrentStage = LoadingStage.LoadingAllBios;
            },
            LoadingStage.LoadingAllBios
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadingAllBios && value == "Inject selected language data into game data.",
            value =>
            {
                CurrentStage = LoadingStage.InjectSelectedLanguageDataIntoGameData;
            },
            LoadingStage.InjectSelectedLanguageDataIntoGameData
        ),
        new(
            value => CurrentStage <= LoadingStage.InjectSelectedLanguageDataIntoGameData && value == "StaticConstructorOnStartupUtilityReplacement.CallAll()",
            value =>
            {
                CurrentStage = LoadingStage.StaticConstructorOnStartupCallAll;
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.StaticConstructorOnStartupCallAll
        ),
        new(
            value => CurrentStage <= LoadingStage.StaticConstructorOnStartupCallAll && value == "ExecuteToExecuteWhenFinished()",
            value =>
            {
                CurrentStage = LoadingStage.ExecuteToExecuteWhenFinished2;
            },
            LoadingStage.ExecuteToExecuteWhenFinished2,
            activity => GetStageTranslation(LoadingStage.ExecuteToExecuteWhenFinished, activity)!
        ),
        new(
            value => CurrentStage <= LoadingStage.ExecuteToExecuteWhenFinished2 && value == "Atlas baking.",
            value =>
            {
                CurrentStage = LoadingStage.AtlasBaking;
            },
            LoadingStage.AtlasBaking
        ),
        new(
            value => CurrentStage <= LoadingStage.AtlasBaking && value == "Garbage Collection",
            value =>
            {
                CurrentStage = LoadingStage.GarbageCollection;
            },
            LoadingStage.GarbageCollection
        ),
        new(
            value => CurrentStage <= LoadingStage.GarbageCollection && value == "Misc Init (InitializingInterface)",
            value =>
            {
                CurrentStage = LoadingStage.Finished;
            },
            LoadingStage.Finished
        )
    ];

    private static LoadingStage currentStage = LoadingStage.Initializing;
    public static LoadingStage CurrentStage
    {
        get => currentStage;
        private set
        {
            if (currentStage != value)
            {
                StageProgress = null; // Reset progress when stage changes
                currentStage = value;
            }
        }
    }

    private static string _currentLoadingActivity = string.Empty;
    internal static void SetCurrentLoadingActivityRaw(string value)
    {
        _currentLoadingActivity = value;
    }

    private static StageRule CurrentStageRule = StageRules[0];
    internal static string CurrentLoadingActivity
    {
        get => _currentLoadingActivity;
        set
        {
            foreach (var rule in StageRules)
            {
                if (rule.Predicate(value))
                {
                    CurrentStageRule = rule;
                    rule.Action(value);
                    return;
                }
            }
        }
    }

    internal static (float currentValue, float maxValue)? StageProgress { get; set; } = null;
}