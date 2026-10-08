using RimWorld.IO;

namespace ilyvion.LoadingProgress;

internal static class Translations
{
    private static Dictionary<string, string>? EnglishTranslationValues;
    private static bool _englishTranslationsLoaded;
    private static Dictionary<string, string>? ActiveLanguageTranslationValues;
    private static bool _activeLanguageTranslationsLoaded;

    // A lookup after this reads the translations again.
    public static void Clear()
    {
        EnglishTranslationValues = null;
        _englishTranslationsLoaded = false;
        ActiveLanguageTranslationValues = null;
        _activeLanguageTranslationsLoaded = false;
    }

    public static string GetTranslation(string translationKey, params object[] args)
    {
        if (translationKey == null)
        {
            return "[null translation key]";
        }

        if (!_englishTranslationsLoaded)
        {
            var englishLanguageDirectory = AbstractFilesystem
                .GetDirectory(
                    Path.Join(
                        Path.Join(LoadingProgressMod.instance.Content.RootDir, "Common"),
                        "Languages",
                        LanguageDatabase.DefaultLangFolderName
                    )
                )
                .GetDirectory("Keyed");
            LoadLanguage(ref EnglishTranslationValues, englishLanguageDirectory);
            _englishTranslationsLoaded = true;
        }

        if (!_activeLanguageTranslationsLoaded && Prefs.LangFolderName != "English")
        {
            var languageFolderName = Prefs.LangFolderName;
            var legacyLanguageFolderName = languageFolderName.Contains(
                '(',
                StringComparison.Ordinal
            )
                ? languageFolderName[
                    ..(languageFolderName.IndexOf('(', StringComparison.Ordinal) - 1)
                ]
                    .Trim()
                : languageFolderName;
            foreach (var mod in LoadedModManager.RunningMods)
            {
                foreach (var loadFolder in mod.foldersToLoadDescendingOrder)
                {
                    // RimWorld's own LoadedLanguage.AllDirectories accepts either the
                    // canonical folder name (e.g. "Polish (Polski)") or the legacy one
                    // (e.g. "Polish"); translations may ship under either.
                    var candidateFolderNames =
                        languageFolderName == legacyLanguageFolderName
                            ? [languageFolderName]
                            : new[] { languageFolderName, legacyLanguageFolderName };
                    foreach (var candidateFolderName in candidateFolderNames)
                    {
                        var languageDirectory = AbstractFilesystem
                            .GetDirectory(Path.Join(loadFolder, "Languages", candidateFolderName))
                            .GetDirectory("Keyed");
                        LoadLanguage(ref ActiveLanguageTranslationValues, languageDirectory);
                        if (ActiveLanguageTranslationValues is not null)
                        {
                            LoadingProgressMod.Message(
                                $"Loaded translations for {languageFolderName} from {mod.Name} from {languageDirectory.FullPath}."
                            );
                            break;
                        }
                    }
                    if (ActiveLanguageTranslationValues is not null)
                    {
                        break;
                    }
                }
                if (ActiveLanguageTranslationValues is not null)
                {
                    break;
                }
            }
            _activeLanguageTranslationsLoaded = true;
        }

        if (ActiveLanguageTranslationValues is not null)
        {
            if (
                ActiveLanguageTranslationValues.TryGetValue(
                    translationKey,
                    out var activeLanguageTranslation
                )
            )
            {
                return string.Format(CultureInfo.CurrentCulture, activeLanguageTranslation, args);
            }
        }

        if (EnglishTranslationValues!.TryGetValue(translationKey, out var englishTranslation))
        {
            return string.Format(CultureInfo.CurrentCulture, englishTranslation, args);
        }
        else
        {
            LoadingProgressMod.Warning($"No translation found for {translationKey}");
            return Translator.PseudoTranslated(translationKey);
        }
    }

    private static void LoadLanguage(
        ref Dictionary<string, string>? languageDictionary,
        VirtualDirectory languageDirectory
    )
    {
        if (!languageDirectory.Exists)
        {
            return;
        }
        foreach (var file in languageDirectory.GetFiles("*.xml", SearchOption.AllDirectories))
        {
            try
            {
                var translationContent = file.ReadAllText();
                if (!translationContent.Contains("LoadingProgress.", StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (var x in DirectXmlLoaderSimple.ValuesFromXmlFile(translationContent))
                {
                    languageDictionary ??= [];
                    languageDictionary[x.key] = x.value;
                }
            }
            catch (Exception e)
            {
                LoadingProgressMod.Warning(
                    $"Failed to load translations from {file.FullPath}: {e}"
                );
            }
        }
    }
}
