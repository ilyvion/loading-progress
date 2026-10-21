using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class LoadingProgressWindowHeightTests
{
    // Vanilla's Small and Medium line heights.
    private const float Small = 22f;
    private const float Medium = 29f;

    private static float Height(
        bool showLastLoadingTime,
        bool showSampleCount,
        bool modHashChanged,
        bool cachingTextures,
        float small = Small,
        float medium = Medium
    ) =>
        LoadingProgressWindow.WindowHeight(
            showLastLoadingTime,
            showSampleCount,
            modHashChanged,
            cachingTextures,
            small,
            medium
        );

    [Test]
    public static void WithNothingExtraTheWindowHasItsBaseHeight() =>
        Expect.AreEqual(110f, Height(false, false, false, false));

    [Test]
    public static void ShowingTheLastLoadingTimeAddsTheTimerRow() =>
        Expect.AreEqual(143f, Height(true, false, false, false));

    [Test]
    public static void SampleCountAndModHashNotesEachAddALine() =>
        Expect.AreEqual(195f, Height(true, true, true, false));

    [Test]
    public static void SampleCountAndModHashNotesNeedTheTimerRow() =>
        Expect.AreEqual(110f, Height(false, true, true, false));

    [Test]
    public static void CachingTexturesAddsALine() =>
        Expect.AreEqual(221f, Height(true, true, true, true));

    [Test]
    public static void CachingTexturesAddsALineWithoutTheTimerRow() =>
        Expect.AreEqual(136f, Height(false, false, false, true));

    // A theme font 4px taller in Small and 6px taller in Medium: the title and timer rows grow by
    // 6 each, the activity line and three notes by 4 each.
    [Test]
    public static void TallerFontsGrowEveryRow() =>
        Expect.AreEqual(221f + 28f, Height(true, true, true, true, Small + 4f, Medium + 6f));

    [Test]
    public static void TheFasterGameLoadingWindowHasTheBaseHeight() =>
        Expect.AreEqual(110f, LoadingProgressWindow.BaseWindowHeight(Small, Medium));

    [Test]
    public static void TheInGameWindowFitsItsRows() =>
        Expect.AreEqual(151f, InGameLoadingWindow.WindowHeight(Small, Medium));

    [Test]
    public static void TheLineHeightOfAFontIsTheOneItIsDrawnWith()
    {
        var oldFont = Text.Font;
        try
        {
            Text.Font = GameFont.Medium;
            var drawn = Text.LineHeight;
            Text.Font = GameFont.Small;
            Expect.AreEqual(drawn, Utilities.LineHeightOf(GameFont.Medium));
        }
        finally
        {
            Text.Font = oldFont;
        }
    }

    [Test]
    public static void MeasuringALineHeightKeepsTheCurrentFont()
    {
        var oldFont = Text.Font;
        try
        {
            Text.Font = GameFont.Small;
            _ = Utilities.LineHeightOf(GameFont.Medium);
            Expect.AreEqual(GameFont.Small, Text.Font);
        }
        finally
        {
            Text.Font = oldFont;
        }
    }

    // On 1.7 this reads a private field of the game's texture cache, which throws if the field
    // has gone away or can't be read at runtime.
    [Test]
    public static void TextureCacheStateIsReadable() => _ = TextureCache.IsCaching;
}
