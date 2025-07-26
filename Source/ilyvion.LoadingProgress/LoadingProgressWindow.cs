using UnityEngine;

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

public class LoadingProgressWindow
{
    internal static object windowLock = new();

    internal static readonly Vector2 WindowSize = new(776f, 110f);

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
            if (CurrentStage <= LoadingStage.LoadingModClasses && value.StartsWith("Loading ") && value.EndsWith(" mod class"))
            {
                CurrentStage = LoadingStage.LoadingModClasses;
                _currentLoadingActivity = value.Substring("Loading ".Length, value.Length - "Loading ".Length - " mod class".Length);
                return;
            }
            else if (CurrentStage == LoadingStage.LoadingModClasses && value == "LoadModXML()")
            {
                CurrentStage = LoadingStage.LoadModXml;
                _currentLoadingActivity = string.Empty;
                return;
            }
            else if (CurrentStage == LoadingStage.LoadModXml && value.StartsWith("Loading ") && !value.StartsWith("Loading asset"))
            {
                _currentLoadingActivity = value["Loading ".Length..];
                return;
            }
            else if (CurrentStage == LoadingStage.LoadModXml && value == "CombineIntoUnifiedXML()")
            {
                CurrentStage = LoadingStage.CombineIntoUnifiedXml;
                return;
            }
            else if (CurrentStage == LoadingStage.CombineIntoUnifiedXml && value == "TKeySystem.Parse()")
            {
                CurrentStage = LoadingStage.TKeySystemParse;
                return;
            }
            else if (CurrentStage == LoadingStage.TKeySystemParse && value == "ErrorCheckPatches()")
            {
                CurrentStage = LoadingStage.ErrorCheckPatches;
                return;
            }
            else if (CurrentStage == LoadingStage.ErrorCheckPatches && value == "ApplyPatches()")
            {
                CurrentStage = LoadingStage.ApplyPatches;
                _currentLoadingActivity = string.Empty;
                return;
            }
            else if (CurrentStage == LoadingStage.ApplyPatches && value.EndsWith(" Worker"))
            {
                _currentLoadingActivity = value[..^" Worker".Length];
                return;
            }
            else if (CurrentStage == LoadingStage.ApplyPatches && value == "ParseAndProcessXML()")
            {
                CurrentStage = LoadingStage.ParseAndProcessXml;
                return;
            }
            else if (CurrentStage == LoadingStage.ParseAndProcessXml && value == "XmlInheritance.Resolve()")
            {
                CurrentStage = LoadingStage.XmlInheritanceResolve;
                return;
            }
            else if (CurrentStage == LoadingStage.XmlInheritanceResolve && value.StartsWith("Loading defs for "))
            {
                CurrentStage = LoadingStage.LoadingDefs;
                _currentLoadingActivity = string.Empty;
                return;
            }
            else if (CurrentStage == LoadingStage.LoadingDefs && value.StartsWith("ParseValueAndReturnDef "))
            {
                _currentLoadingActivity = value["ParseValueAndReturnDef (for ".Length..][..^1];
                return;
            }
            else if (CurrentStage == LoadingStage.LoadingDefs && value == "ClearCachedPatches()")
            {
                CurrentStage = LoadingStage.ClearCachedPatches;
                return;
            }
            else if (CurrentStage == LoadingStage.ClearCachedPatches && value == "XmlInheritance.Clear()")
            {
                CurrentStage = LoadingStage.XmlInheritanceClear;
                return;
            }
            else if (CurrentStage == LoadingStage.XmlInheritanceClear && value == "Load language metadata.")
            {
                CurrentStage = LoadingStage.LoadLanguageMetadata;
                return;
            }
            else if (CurrentStage == LoadingStage.LoadLanguageMetadata && value.StartsWith("Loading language data:"))
            {
                CurrentStage = LoadingStage.LoadLanguage;
                _currentLoadingActivity = value["Loading language data: ".Length..];
                return;
            }
            else if (CurrentStage == LoadingStage.LoadLanguage && value == "Copy all Defs from mods to global databases.")
            {
                CurrentStage = LoadingStage.CopyAllDefsToGlobalDatabases;
                return;
            }
            else if (CurrentStage == LoadingStage.CopyAllDefsToGlobalDatabases && value == "Resolve cross-references between non-implied Defs.")
            {
                CurrentStage = LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefs;
                return;
            }
            else if (CurrentStage == LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefs && value == "Rebind DefOfs (early).")
            {
                CurrentStage = LoadingStage.RebindDefOfsEarly;
                return;
            }
            else if (CurrentStage == LoadingStage.RebindDefOfsEarly && value == "TKeySystem.BuildMappings()")
            {
                CurrentStage = LoadingStage.TKeySystemBuildMappings;
                return;
            }
            else if (CurrentStage == LoadingStage.TKeySystemBuildMappings && value == "Legacy backstory translations.")
            {
                CurrentStage = LoadingStage.LegacyBackstoryTranslations;
                return;
            }
            else if (CurrentStage == LoadingStage.LegacyBackstoryTranslations && value == "Inject selected language data into game data (early pass).")
            {
                CurrentStage = LoadingStage.InjectSelectedLanguageDataEarly;
                return;
            }
            else if (CurrentStage == LoadingStage.InjectSelectedLanguageDataEarly && value == "Global operations (early pass).")
            {
                CurrentStage = LoadingStage.GlobalOperationsEarly;
                return;
            }
            else if (CurrentStage == LoadingStage.GlobalOperationsEarly && value == "Resolve cross-references between Defs made by the implied defs.")
            {
                CurrentStage = LoadingStage.ResolveCrossReferencesBetweenImpliedDefs;
                return;
            }
            else if (CurrentStage == LoadingStage.ResolveCrossReferencesBetweenImpliedDefs && value == "Rebind DefOfs (final).")
            {
                CurrentStage = LoadingStage.RebindDefOfsFinal;
                return;
            }
            else if (CurrentStage == LoadingStage.RebindDefOfsFinal && value == "Other def binding, resetting and global operations (pre-resolve).")
            {
                CurrentStage = LoadingStage.OtherOperationsPreResolve;
                return;
            }
            else if (CurrentStage == LoadingStage.OtherOperationsPreResolve && value == "Resolve references.")
            {
                CurrentStage = LoadingStage.ResolveReferences;
                _currentLoadingActivity = string.Empty;
                return;
            }
            else if (CurrentStage == LoadingStage.ResolveReferences && value.StartsWith("ResolveAllReferences "))
            {
                _currentLoadingActivity = value["ResolveAllReferences ".Length..];
                return;
            }
            else if (CurrentStage == LoadingStage.ResolveReferences && value == "Other def binding, resetting and global operations (post-resolve).")
            {
                CurrentStage = LoadingStage.OtherOperationsPostResolve;
                return;
            }
            else if (CurrentStage == LoadingStage.OtherOperationsPostResolve && value == "Error check all defs.")
            {
                CurrentStage = LoadingStage.ErrorCheckAllDefs;
                return;
            }
            else if (CurrentStage == LoadingStage.ErrorCheckAllDefs && value == "Short hash giving.")
            {
                CurrentStage = LoadingStage.ShortHashGiving;
                return;
            }
            else if (CurrentStage == LoadingStage.ShortHashGiving && value == "Load all bios")
            {
                CurrentStage = LoadingStage.LoadingAllBios;
                return;
            }
            else if (CurrentStage == LoadingStage.LoadingAllBios && value == "Inject selected language data into game data.")
            {
                CurrentStage = LoadingStage.InjectSelectedLanguageDataIntoGameData;
                return;
            }
            else if (CurrentStage == LoadingStage.InjectSelectedLanguageDataIntoGameData && value == "Static constructor calls")
            {
                CurrentStage = LoadingStage.StaticConstructorOnStartupCallAll;
                _currentLoadingActivity = string.Empty;
                return;
            }
            else if (CurrentStage == LoadingStage.StaticConstructorOnStartupCallAll && value == "Atlas baking.")
            {
                CurrentStage = LoadingStage.AtlasBaking;
                return;
            }
            else if (CurrentStage == LoadingStage.AtlasBaking && value == "Garbage collection.")
            {
                CurrentStage = LoadingStage.GarbageCollection;
                return;
            }
            else if (CurrentStage == LoadingStage.GarbageCollection && value == "Misc Init (InitializingInterface)")
            {
                CurrentStage = LoadingStage.Finished;
                return;
            }
        }
    }


    internal static void DrawWindow(Rect statusRect)
    {
        Find.WindowStack.ImmediateWindow(62893994, statusRect, WindowLayer.Super, delegate
        {
            DrawContents(statusRect.AtZero());
        });
    }

    internal static void DrawContents(Rect rect)
    {
        Text.Font = GameFont.Medium;
        Text.Anchor = TextAnchor.UpperLeft;
        float num2 = 17f;
        float num3 = num2 + 4f;
        Rect rect3 = rect;
        rect3.x += num3;
        rect3.y += 10f;
        rect3.width -= 2 * num3;

        Widgets.Label(rect3, "Loading progress");
        rect3.yMin += Text.LineHeight + 4f;

        Text.Font = GameFont.Small;

        lock (windowLock)
        {
            switch (CurrentStage)
            {
                case LoadingStage.Initializing:
                    Widgets.Label(rect3, "Initializing...");
                    break;
                case LoadingStage.LoadingModClasses:
                    Widgets.Label(rect3, $"Loading mod classes... (<i>{_currentLoadingActivity}</i>)");
                    break;
                case LoadingStage.LoadModXml:
                    Widgets.Label(rect3, $"Loading mod XML... (<i>{_currentLoadingActivity}</i>)");
                    break;
                case LoadingStage.CombineIntoUnifiedXml:
                    Widgets.Label(rect3, $"Combining XML...");
                    break;
                case LoadingStage.TKeySystemParse:
                    Widgets.Label(rect3, "Parsing Translation Key system...");
                    break;
                case LoadingStage.ErrorCheckPatches:
                    Widgets.Label(rect3, $"Checking XML patches for errors...");
                    break;
                case LoadingStage.ApplyPatches:
                    Widgets.Label(rect3, $"Applying XML patches... (<i>{_currentLoadingActivity}</i>)");
                    break;
                case LoadingStage.ParseAndProcessXml:
                    Widgets.Label(rect3, $"Parsing and processing XML...");
                    break;
                case LoadingStage.XmlInheritanceResolve:
                    Widgets.Label(rect3, "Resolving XML inheritance...");
                    break;
                case LoadingStage.LoadingDefs:
                    Widgets.Label(rect3, $"Loading defs... (<i>{_currentLoadingActivity}</i>)");
                    break;
                case LoadingStage.ClearCachedPatches:
                    Widgets.Label(rect3, $"Clearing cached patches...");
                    break;
                case LoadingStage.XmlInheritanceClear:
                    Widgets.Label(rect3, $"Clearing XML inheritance...");
                    break;
                case LoadingStage.LoadLanguageMetadata:
                    Widgets.Label(rect3, $"Loading language metadata...");
                    break;
                case LoadingStage.LoadLanguage:
                    Widgets.Label(rect3, $"Loading language data... (<i>{_currentLoadingActivity}</i>)");
                    break;
                case LoadingStage.CopyAllDefsToGlobalDatabases:
                    Widgets.Label(rect3, "Copying all Defs from mods to global databases...");
                    break;
                case LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefs:
                    Widgets.Label(rect3, "Resolving cross-references between non-implied Defs...");
                    break;
                case LoadingStage.RebindDefOfsEarly:
                    Widgets.Label(rect3, "Rebinding DefOfs (early)...");
                    break;
                case LoadingStage.TKeySystemBuildMappings:
                    Widgets.Label(rect3, "Building Translation Key system mappings...");
                    break;
                case LoadingStage.LegacyBackstoryTranslations:
                    Widgets.Label(rect3, "Loading legacy backstory translations...");
                    break;
                case LoadingStage.InjectSelectedLanguageDataEarly:
                    Widgets.Label(rect3, "Injecting selected language data into game data (early pass)...");
                    break;
                case LoadingStage.GlobalOperationsEarly:
                    Widgets.Label(rect3, "Running global operations (early pass)...");
                    break;
                case LoadingStage.ResolveCrossReferencesBetweenImpliedDefs:
                    Widgets.Label(rect3, "Resolving cross-references between Defs made by the implied defs...");
                    break;
                case LoadingStage.RebindDefOfsFinal:
                    Widgets.Label(rect3, "Rebinding DefOfs (final)...");
                    break;
                case LoadingStage.OtherOperationsPreResolve:
                    Widgets.Label(rect3, "Running other global operations (pre-resolve)...");
                    break;
                case LoadingStage.ResolveReferences:
                    Widgets.Label(rect3, $"Resolving references... (<i>{_currentLoadingActivity}</i>)");
                    break;
                case LoadingStage.OtherOperationsPostResolve:
                    Widgets.Label(rect3, "Running other global operations (post-resolve)...");
                    break;
                case LoadingStage.ErrorCheckAllDefs:
                    Widgets.Label(rect3, "Checking all defs for errors...");
                    break;
                case LoadingStage.ShortHashGiving:
                    Widgets.Label(rect3, "Giving short hashes to defs...");
                    break;
                case LoadingStage.StaticConstructorOnStartupCallAll:
                    Widgets.Label(rect3, $"Calling all static constructors... (<i>{_currentLoadingActivity}</i>)");
                    break;
                case LoadingStage.LoadingAllBios:
                    Widgets.Label(rect3, "Loading all bios...");
                    break;
                case LoadingStage.InjectSelectedLanguageDataIntoGameData:
                    Widgets.Label(rect3, "Injecting selected language data into game data...");
                    break;
                case LoadingStage.AtlasBaking:
                    Widgets.Label(rect3, "Baking static atlases...");
                    break;
                case LoadingStage.GarbageCollection:
                    Widgets.Label(rect3, "Running garbage collection...");
                    break;
                case LoadingStage.Finished:
                default:
                    break;
            }

            DrawHorizontalProgressBar(
                new Rect(rect3.x, rect3.y + Text.LineHeight + 4f, rect3.width, 20f),
                (int)CurrentStage,
                (int)LoadingStage.Finished);
        }

        Text.Anchor = TextAnchor.UpperLeft;
    }

    protected static void DrawHorizontalProgressBar(
        Rect progressRect,
        float currentValue,
        float maxValue)
    {
        // draw a box for the bar
        GUI.color = Color.gray;
        Widgets.DrawBox(progressRect.ContractedBy(1f));
        GUI.color = Color.white;

        // get the bar rect
        var barRect = progressRect.ContractedBy(2f);
        var unit = barRect.width / maxValue;
        barRect.width = currentValue * unit;

        // draw the bar
        Widgets.DrawBoxSolid(barRect, BarColor);
    }

    private static readonly Color BarColor = new(0.2f, 0.8f, 0.85f);
}
