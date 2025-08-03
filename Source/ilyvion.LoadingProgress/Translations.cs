using System.IO;

namespace ilyvion.LoadingProgress;

internal static class Translations
{
    private static Dictionary<string, string>? TranslationValues = null;

    public static string? GetTranslation(string translationKey, params object[] args)
    {
        if (translationKey == null)
        {
            return null;
        }

        if (TranslationValues is not null)
        {
            if (TranslationValues.TryGetValue(translationKey, out var translation))
            {
                return string.Format(translation, args);
            }
            else
            {
                Log.Warning($"No translation found for {translationKey}");
                return Translator.PseudoTranslated(translationKey);
            }
        }

        TranslationValues = [];
        foreach (var file in Directory.GetFiles(LoadingProgressMod.instance.Content.RootDir + "/Common/Languages/English/Keyed", "*.xml"))
        {
            var stageTranslationsContent = File.ReadAllText(file);
            foreach (var x in DirectXmlLoaderSimple.ValuesFromXmlFile(stageTranslationsContent))
            {
                TranslationValues[x.key] = x.value;
            }
        }

        return GetTranslation(translationKey, args);
    }
}