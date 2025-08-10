using System.IO;

namespace ilyvion.LoadingProgress;

internal static class Translations
{
    private static Dictionary<string, string>? EnglishTranslationValues = null;
    private static bool _englishTranslationsLoaded = false;
    private static Dictionary<string, string>? ActiveLanguageTranslationValues = null;
    private static bool _activeLanguageTranslationsLoaded = false;

    public static void Clear()
    {
        EnglishTranslationValues = null;
        ActiveLanguageTranslationValues = null;
    }

    public static string GetTranslation(string translationKey, params object[] args)
    {
        if (translationKey == null)
        {
            return "[null translation key]";
        }

        if (!_englishTranslationsLoaded)
        {
            var englishLanguageDirectory = Path.Join(Path.Join(Path.Join(LoadingProgressMod.instance.Content.RootDir, "Common"), "Languages", LanguageDatabase.DefaultLangFolderName), "Keyed");
            LoadLanguage(ref EnglishTranslationValues, englishLanguageDirectory);
            _englishTranslationsLoaded = true;
        }

        if (!_activeLanguageTranslationsLoaded && Prefs.LangFolderName != "English")
        {
            var languageFolderName = Prefs.LangFolderName;
            foreach (var mod in LoadedModManager.RunningMods)
            {
                foreach (var loadFolder in mod.foldersToLoadDescendingOrder)
                {
                    var languageDirectory = Path.Join(Path.Join(loadFolder, "Languages", languageFolderName), "Keyed");
                    if (Directory.Exists(languageDirectory))
                    {
                        LoadLanguage(ref ActiveLanguageTranslationValues, languageDirectory);
                        if (ActiveLanguageTranslationValues is not null)
                        {
                            LoadingProgressMod.Message($"Loaded translations for {languageFolderName} from {mod.Name} from {languageDirectory}.");
                            break;
                        }
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
            if (ActiveLanguageTranslationValues.TryGetValue(translationKey, out var activeLanguageTranslation))
            {
                return string.Format(activeLanguageTranslation, args);
            }
        }

        if (EnglishTranslationValues!.TryGetValue(translationKey, out var englishTranslation))
        {
            return string.Format(englishTranslation, args);
        }
        else
        {
            LoadingProgressMod.Warning($"No translation found for {translationKey}");
            return Translator.PseudoTranslated(translationKey);
        }
    }

    private static void LoadLanguage(ref Dictionary<string, string>? languageDictionary, string languageDirectory)
    {
        foreach (var file in Directory.GetFiles(languageDirectory, "*.xml"))
        {
            var translationContent = File.ReadAllText(file);
            if (!translationContent.Contains("LoadingProgress."))
            {
                continue;
            }
            foreach (var x in DirectXmlLoaderSimple.ValuesFromXmlFile(translationContent))
            {
                languageDictionary ??= [];
                languageDictionary[x.key] = x.value;
            }
        }
    }
}
