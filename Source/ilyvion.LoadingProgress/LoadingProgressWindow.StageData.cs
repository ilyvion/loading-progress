using System.Globalization;
using System.Text;

using LudeonTK;

namespace ilyvion.LoadingProgress;

internal enum LoadingStage
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
    ResolveCrossReferencesBetweenNonImpliedDefsStage1,
    ResolveCrossReferencesBetweenNonImpliedDefsStage2,
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

internal sealed partial class LoadingProgressWindow
{
    private delegate bool StagePredicate(string value);
    private delegate void StageAction(string value);
    private delegate string? StageDisplayLabel(string value);

    private sealed record StageRule(
        StagePredicate Predicate,
        StageAction Action,
        LoadingStage Stage,
        StageDisplayLabel? CustomLabel = null
    );


    private static string GetStageTranslation(LoadingStage stage, params object[] args)
        => Translations.GetTranslation($"LoadingProgress.Stage.{stage}", args);

    private static string GetStageTranslationWithSecondary(
        LoadingStage stage,
        string secondary,
        params object[] args)
        => Translations.GetTranslation($"LoadingProgress.Stage.{stage}.{secondary}", args);

    private static readonly List<StageRule> StageRules =
    [
        new(
            value => false,
            value => {},
            LoadingStage.Initializing
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadingModClasses
                && value.StartsWith("Loading ", StringComparison.Ordinal)
                && value.EndsWith(" mod class", StringComparison.Ordinal),
            value => {
                CurrentStage = LoadingStage.LoadingModClasses;
                #pragma warning disable IDE0045
                if (StageProgress is (float current, float max))
                {
                    StageProgress = ((int)current + 1, max);
                }
                else
                {
                    StageProgress = (1,  typeof(Mod).InstantiableDescendantsAndSelf().Count());
                }
                #pragma warning restore IDE0045
                _currentLoadingActivity = value.Substring(
                    "Loading ".Length,
                    value.Length - "Loading ".Length - " mod class".Length);
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
            value => CurrentStage <= LoadingStage.LoadModXml
                && value.StartsWith("Loading ", StringComparison.Ordinal)
                && !value.StartsWith("Loading asset", StringComparison.Ordinal),
            value => {
                #pragma warning disable IDE0045
                if (StageProgress is (float current, float max))
                {
                    StageProgress = ((int)current + 1, max);
                }
                else
                {
                    StageProgress = (1, LoadedModManager.RunningModsListForReading.Count);
                }
                #pragma warning restore IDE0045
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
            value => CurrentStage <= LoadingStage.CombineIntoUnifiedXml
                && value == "TKeySystem.Parse()",
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
            value => CurrentStage == LoadingStage.ErrorCheckPatches
                && value.StartsWith("Loading all patches", StringComparison.Ordinal),
            value => {
                if (StageProgress is (float current, float max))
                {
                    if (LoadingDataTracker.ModChanged)
                    {
                        StageProgress = ((int)current + 1, max);
                    }
                }
                else
                {
                    StageProgress = (1, LoadedModManager.RunningModsListForReading.Count);
                }
            },
            LoadingStage.ErrorCheckPatches,
            activity => GetStageTranslationWithSecondary(
                LoadingStage.ErrorCheckPatches,
                $"ForMod",
                LoadingDataTracker.Current ?? "")
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
            value => CurrentStage <= LoadingStage.ApplyPatches
                && value.EndsWith(" Worker", StringComparison.Ordinal),
            value =>
            {
                #pragma warning disable IDE0045
                if (StageProgress is (float current, float max))
                {
                    StageProgress = ((int)current + 1, max);
                }
                else
                {
                    StageProgress = (1, GetApproximatePatchOperationCount());
                }
                #pragma warning restore IDE0045
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
            value => CurrentStage == LoadingStage.ParseAndProcessXml
                && value.StartsWith("Loading asset nodes", StringComparison.Ordinal),
            value =>
            {
                value = value["Loading asset nodes ".Length..];
                if (int.TryParse(value, out var count))
                {
                    StageProgress = (0, count);
                }
            },
            LoadingStage.ParseAndProcessXml
        ),
        new(
            value => CurrentStage == LoadingStage.ParseAndProcessXml
                && value == "XmlInheritance.TryRegister",
            value =>
            {
                if (StageProgress is (float current, float max))
                {
                    StageProgress = ((int)current + 1, max);
                }
            },
            LoadingStage.ParseAndProcessXml,
            activity => GetStageTranslationWithSecondary(
                LoadingStage.ParseAndProcessXml,
                $"ForMod",
                LoadingDataTracker.Current ?? "")
        ),
        new(
            value => CurrentStage <= LoadingStage.ParseAndProcessXml
                && value == "XmlInheritance.Resolve()",
            value =>
            {
                CurrentStage = LoadingStage.XmlInheritanceResolve;
            },
            LoadingStage.XmlInheritanceResolve
        ),
        new(
            value => CurrentStage <= LoadingStage.XmlInheritanceResolve
                && value.StartsWith("Loading defs for ", StringComparison.Ordinal),
            value =>
            {
                CurrentStage = LoadingStage.LoadingDefs;
                var valueCount = value["Loading defs for ".Length..];
                var spaceIndex = valueCount.IndexOf(' ', StringComparison.Ordinal);
                if (spaceIndex >= 0)
                {
                    var count = int.Parse(valueCount[..spaceIndex], CultureInfo.InvariantCulture);
                    StageProgress = (0, count);
                }
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.LoadingDefs
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadingDefs
                && value.StartsWith("ParseValueAndReturnDef ", StringComparison.Ordinal),
            value =>
            {
                if (StageProgress is (float current, float max))
                {
                    StageProgress = ((int)current + 1, max);
                }
                _currentLoadingActivity = value["ParseValueAndReturnDef (for ".Length..][..^1];
            },
            LoadingStage.LoadingDefs,
            _ => LoadingDataTracker.LastDef is Def def
                ? GetStageTranslationWithSecondary(
                    LoadingStage.LoadingDefs,
                    $"WithDef",
                    def.modContentPack?.Name ?? "[unknown mod]",
                    def.GetType().FullName,
                    def.defName)
                : null
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
            value => CurrentStage <= LoadingStage.ClearCachedPatches
                && value == "XmlInheritance.Clear()",
            value =>
            {
                CurrentStage = LoadingStage.XmlInheritanceClear;
            },
            LoadingStage.XmlInheritanceClear
        ),
        new(
            value => CurrentStage <= LoadingStage.XmlInheritanceClear
                && value == "Load language metadata.",
            value =>
            {
                CurrentStage = LoadingStage.LoadLanguageMetadata;
            },
            LoadingStage.LoadLanguageMetadata
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadLanguageMetadata
                && value.StartsWith("Loading language data:", StringComparison.Ordinal),
            value =>
            {
                CurrentStage = LoadingStage.LoadLanguage;
                _currentLoadingActivity = value["Loading language data: ".Length..];
            },
            LoadingStage.LoadLanguage
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadLanguage
                && value == "Copy all Defs from mods to global databases.",
            value =>
            {
                CurrentStage = LoadingStage.CopyAllDefsToGlobalDatabases;
            },
            LoadingStage.CopyAllDefsToGlobalDatabases
        ),
        new(
            value => CurrentStage <= LoadingStage.CopyAllDefsToGlobalDatabases
                && value == "Resolve cross-references between non-implied Defs.",
            value =>
            {
                CurrentStage = LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefsStage1;
            },
            LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefsStage1
        ),
        new(
            value => false,
            value => { },
            LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefsStage2
        ),
        new(
            value => CurrentStage <= LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefsStage2
                && value == "Rebind DefOfs (early).",
            value =>
            {
                CurrentStage = LoadingStage.RebindDefOfsEarly;
            },
            LoadingStage.RebindDefOfsEarly
        ),
        new(
            value => CurrentStage <= LoadingStage.RebindDefOfsEarly
                && value == "TKeySystem.BuildMappings()",
            value =>
            {
                CurrentStage = LoadingStage.TKeySystemBuildMappings;
            },
            LoadingStage.TKeySystemBuildMappings
        ),
        new(
            value => CurrentStage <= LoadingStage.TKeySystemBuildMappings
                && value == "Legacy backstory translations.",
            value =>
            {
                CurrentStage = LoadingStage.LegacyBackstoryTranslations;
            },
            LoadingStage.LegacyBackstoryTranslations
        ),
        new(
            value => CurrentStage <= LoadingStage.LegacyBackstoryTranslations
                && value == "Inject selected language data into game data (early pass).",
            value =>
            {
                CurrentStage = LoadingStage.InjectSelectedLanguageDataEarly;
            },
            LoadingStage.InjectSelectedLanguageDataEarly
        ),
        new(
            value => CurrentStage <= LoadingStage.InjectSelectedLanguageDataEarly
                && value == "Global operations (early pass).",
            value =>
            {
                CurrentStage = LoadingStage.GlobalOperationsEarly;
            },
            LoadingStage.GlobalOperationsEarly
        ),
        new(
            value => CurrentStage <= LoadingStage.GlobalOperationsEarly
                && value == "Generate implied Defs (pre-resolve).",
            value =>
            {
                CurrentStage = LoadingStage.GenerateImpliedDefs;
            },
            LoadingStage.GenerateImpliedDefs
        ),
        new(
            value => CurrentStage <= LoadingStage.GenerateImpliedDefs
                && value == "Resolve cross-references between Defs made by the implied defs.",
            value =>
            {
                CurrentStage = LoadingStage.ResolveCrossReferencesBetweenImpliedDefs;
            },
            LoadingStage.ResolveCrossReferencesBetweenImpliedDefs
        ),
        new(
            value => CurrentStage <= LoadingStage.ResolveCrossReferencesBetweenImpliedDefs
                && value == "Rebind DefOfs (final).",
            value =>
            {
                CurrentStage = LoadingStage.RebindDefOfsFinal;
            },
            LoadingStage.RebindDefOfsFinal
        ),
        new(
            value => CurrentStage <= LoadingStage.RebindDefOfsFinal
                && value == "Other def binding, resetting and global operations (pre-resolve).",
            value =>
            {
                CurrentStage = LoadingStage.OtherOperationsPreResolve;
            },
            LoadingStage.OtherOperationsPreResolve
        ),
        new(
            value => CurrentStage <= LoadingStage.OtherOperationsPreResolve
                && value == "Resolve references.",
            value =>
            {
                CurrentStage = LoadingStage.ResolveReferences;
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.ResolveReferences
        ),
        new(
            value => CurrentStage <= LoadingStage.ResolveReferences
                && value.StartsWith("ResolveAllReferences ", StringComparison.Ordinal),
            value =>
            {
                _currentLoadingActivity = value["ResolveAllReferences ".Length..];
            },
            LoadingStage.ResolveReferences
        ),
        new(
            value => CurrentStage <= LoadingStage.ResolveReferences
                && value == "Other def binding, resetting and global operations (post-resolve).",
            value =>
            {
                CurrentStage = LoadingStage.OtherOperationsPostResolve;
            },
            LoadingStage.OtherOperationsPostResolve
        ),
        new(
            value => CurrentStage <= LoadingStage.OtherOperationsPostResolve
                && value == "Error check all defs.",
            value =>
            {
                CurrentStage = LoadingStage.ErrorCheckAllDefs;
            },
            LoadingStage.ErrorCheckAllDefs
        ),
        new(
            value => CurrentStage <= LoadingStage.ErrorCheckAllDefs
                && value == "Short hash giving.",
            value =>
            {
                CurrentStage = LoadingStage.ShortHashGiving;
            },
            LoadingStage.ShortHashGiving
        ),
        new(
            value => CurrentStage <= LoadingStage.ShortHashGiving
                && (value == "ExecuteToExecuteWhenFinished()"),
            value =>
            {
                CurrentStage = LoadingStage.ExecuteToExecuteWhenFinished;
            },
            LoadingStage.ExecuteToExecuteWhenFinished
        ),
        new(
            value => CurrentStage == LoadingStage.ExecuteToExecuteWhenFinished
                && value.StartsWith("LP.Reload ", StringComparison.Ordinal),
            value =>
            {
                _currentLoadingActivity = value["LP.Reload ".Length..];
            },
            LoadingStage.ExecuteToExecuteWhenFinished,
            activity => GetStageTranslationWithSecondary(
                LoadingStage.ExecuteToExecuteWhenFinished,
                $"Reloading.{activity.Replace(" ", "_", StringComparison.Ordinal)}",
                LoadingDataTracker.Current ?? "")!),
        new(
            value => CurrentStage == LoadingStage.ExecuteToExecuteWhenFinished
                && value.Contains(" -> ", StringComparison.Ordinal),
            value =>
            {
                _currentLoadingActivity = value;
            },
            LoadingStage.ExecuteToExecuteWhenFinished
        ),
        new(
            value => CurrentStage <= LoadingStage.ExecuteToExecuteWhenFinished
                && value == "Load all bios",
            value =>
            {
                CurrentStage = LoadingStage.LoadingAllBios;
            },
            LoadingStage.LoadingAllBios
        ),
        new(
            value => CurrentStage <= LoadingStage.LoadingAllBios
                && value == "Inject selected language data into game data.",
            value =>
            {
                CurrentStage = LoadingStage.InjectSelectedLanguageDataIntoGameData;
            },
            LoadingStage.InjectSelectedLanguageDataIntoGameData
        ),
        new(
            value => CurrentStage <= LoadingStage.InjectSelectedLanguageDataIntoGameData
                && value == "StaticConstructorOnStartupUtilityReplacement.CallAll()",
            value =>
            {
                CurrentStage = LoadingStage.StaticConstructorOnStartupCallAll;
                _currentLoadingActivity = string.Empty;
            },
            LoadingStage.StaticConstructorOnStartupCallAll
        ),
        new(
            value => CurrentStage <= LoadingStage.StaticConstructorOnStartupCallAll
                && value == "ExecuteToExecuteWhenFinished()",
            value =>
            {
                CurrentStage = LoadingStage.ExecuteToExecuteWhenFinished2;
            },
            LoadingStage.ExecuteToExecuteWhenFinished2,
            activity => GetStageTranslation(LoadingStage.ExecuteToExecuteWhenFinished, activity)!
        ),
        new(
            value => CurrentStage <= LoadingStage.ExecuteToExecuteWhenFinished2
                && value == "Atlas baking.",
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
            value => CurrentStage <= LoadingStage.GarbageCollection
                && value == "Misc Init (InitializingInterface)",
            value =>
            {
                CurrentStage = LoadingStage.Finished;
                if (_loadingStopwatch is { } loadingStopwatch)
                {
                    LoadingProgressMod.Settings.LastLoadingTime
                        = (float)loadingStopwatch.Elapsed.TotalSeconds;
                    LoadingProgressMod.Settings.LastLoadingModHash = _currentModHash;
                    LoadingProgressMod.Settings.Write();
                    loadingStopwatch.Stop();
                    Translations.Clear();
                }
            },
            LoadingStage.Finished
        )
    ];

    private static int GetApproximatePatchOperationCount()
    {
        var count = 0;
        var patches = LoadedModManager.RunningMods.SelectMany(rm => rm.Patches);
        foreach (var patch in patches)
        {
            if (patch is PatchOperationSequence sequence)
            {
                count += sequence.operations.Count;
            }
            else if (patch is PatchOperationConditional or PatchOperationFindMod)
            {
                count += 2;
            }
            else
            {
                count++;
            }
        }
        return count;
    }

    private static LoadingStage currentStage = LoadingStage.Initializing;
    internal static LoadingStage CurrentStage
    {
        get => currentStage;
        set
        {
            if (currentStage != value)
            {
                // Reset progress when stage changes
                StageProgress = null;
                LoadingDataTracker.Current = null;

                currentStage = value;
            }
        }
    }

    private static string _currentLoadingActivity = string.Empty;
    internal static void SetCurrentLoadingActivityRaw(string value)
        => _currentLoadingActivity = value;

    private static StageRule CurrentStageRule = StageRules[0];
    internal static string CurrentLoadingActivity
    {
        get => _currentLoadingActivity;
        set
        {
            // Skip very frequent messages (10k-100k+ in big mod packs) to avoid the cost of
            // the processing below
            var currentStage = CurrentStage;
#pragma warning disable IDE0010
            switch (currentStage)
            {
                case LoadingStage.ParseAndProcessXml:
                    if (value is "assetlookup.TryGetValue"
                        or "XmlInheritance.TryRegister")
                    {
                        return;
                    }
                    break;
                case LoadingStage.XmlInheritanceResolve:
                    if (value.StartsWith("RecursiveNodeCopyOverwriteElements",
                        StringComparison.Ordinal))
                    {
                        return;
                    }
                    break;
                case LoadingStage.LoadingDefs:
                    if (value.StartsWith("RegisterObjectWantsCrossRef", StringComparison.Ordinal)
                        || value == "RegisterListWantsCrossRef")
                    {
                        return;
                    }
                    break;
                case LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefsStage1:
                    if (value == "TryResolveDef")
                    {
                        return;
                    }
                    break;
                case LoadingStage.GenerateImpliedDefs:
                    if (value == "RegisterListWantsCrossRef")
                    {
                        return;
                    }
                    break;
                case LoadingStage.ResolveReferences:
                    if (value == "Resolver call")
                    {
                        return;
                    }
                    break;
            }
#pragma warning restore IDE0010

            // This one is in so many stages that it's not worth differentiating
            if (value == "TryDoPostLoad")
            {
                return;
            }

            for (var i = 0; i < StageRules.Count; i++)
            {
                var rule = StageRules[i];
                if (rule.Predicate(value))
                {
                    // Remove all prior rules that do NOT share the same LoadingStage as 
                    // the matched rule
                    if (i > 0)
                    {
                        var matchedStage = rule.Stage;
                        var removeCount = 0;
                        for (var j = 0; j < i; j++)
                        {
                            if (StageRules[j].Stage != matchedStage)
                            {
                                removeCount++;
                            }
                        }
                        if (removeCount > 0)
                        {
                            // Remove only those rules
                            StageRules.RemoveAll((r, idx) => idx < i && r.Stage != matchedStage);
                        }
                    }
                    CurrentStageRule = rule;
                    rule.Action(value);
                    return;
                }
            }

            if (Prefs.DevMode)
            {
                lock (_unmatchedStageActivitiesLock)
                {
                    if (!_unmatchedStageActivities.TryGetValue(value, out var tuple))
                    {
                        _unmatchedStageActivities.Add(value, (1, [CurrentStage]));
                    }
                    else
                    {
                        _ = tuple.Item2.Add(CurrentStage);
                        _unmatchedStageActivities[value] = (tuple.Item1 + 1, tuple.Item2);
                    }
                }
            }
        }
    }

    internal static (float currentValue, float maxValue)? StageProgress
    {
        get; set;
    }

    private static readonly object _unmatchedStageActivitiesLock = new();
    private static readonly Dictionary<string, (int, HashSet<LoadingStage>)>
        _unmatchedStageActivities = [];

    [DebugOutput("Loading Progress", false)]
    public static void ShowUnmatchedStageActivities()
    {
        if (_unmatchedStageActivities.Count == 0)
        {
            LoadingProgressMod.Warning(
                "Unmatched stage activities are only recorded when the game is launched with "
                    + "development mode enabled.");
            return;
        }

        var stringBuilder = new StringBuilder();
        foreach (var kvp in _unmatchedStageActivities.OrderByDescending(kvp => kvp.Value.Item1))
        {
            _ = stringBuilder.AppendLine($"{kvp.Key}: {kvp.Value.Item1} times, in stages: "
                + string.Join(", ", kvp.Value.Item2.Select(s => s.ToString())));
        }
        LoadingProgressMod.DevMessage(stringBuilder.ToString());
    }
}
