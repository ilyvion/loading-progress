using DevTools.Testing;
using static ilyvion.LoadingProgress.Constants;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class LoadingProgressWindowHeightTests
{
    private static float NoteLineHeight => Text.LineHeightOf(GameFont.Small) + VerticalWidgetMargin;

    [Test]
    public static void WithNothingExtraTheWindowHasItsBaseHeight() =>
        Expect.AreEqual(110f, LoadingProgressWindow.WindowHeight(false, false, false, false));

    [Test]
    public static void ShowingTheLastLoadingTimeAddsTheTimerRow() =>
        Expect.AreEqual(140f, LoadingProgressWindow.WindowHeight(true, false, false, false));

    [Test]
    public static void SampleCountAndModHashNotesEachAddALine() =>
        Expect.AreEqual(
            140f + (2 * NoteLineHeight),
            LoadingProgressWindow.WindowHeight(true, true, true, false)
        );

    [Test]
    public static void SampleCountAndModHashNotesNeedTheTimerRow() =>
        Expect.AreEqual(110f, LoadingProgressWindow.WindowHeight(false, true, true, false));

    [Test]
    public static void CachingTexturesAddsALine() =>
        Expect.AreEqual(
            140f + (3 * NoteLineHeight),
            LoadingProgressWindow.WindowHeight(true, true, true, true)
        );

    [Test]
    public static void CachingTexturesAddsALineWithoutTheTimerRow() =>
        Expect.AreEqual(
            110f + NoteLineHeight,
            LoadingProgressWindow.WindowHeight(false, false, false, true)
        );

    // On 1.7 this reads a private field of the game's texture cache, which throws if the field
    // has gone away or can't be read at runtime.
    [Test]
    public static void TextureCacheStateIsReadable() => _ = TextureCache.IsCaching;
}
