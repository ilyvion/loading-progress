using DevTools.Testing;
using ilyvion.LoadingProgress.ColorPicker;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class Dialog_ColorPickerTests
{
    private const int PickerSize = 300;

    [Test]
    public static void TheBackgroundsAreCreatedAtThePickerSize()
    {
        var picker = new Dialog_ColorPicker(Color.red);

        Expect.AreEqual(PickerSize, picker.ColorPickerBG.width);
        Expect.AreEqual(PickerSize, picker.ColorPickerBG.height);
        Expect.AreEqual(1, picker.HuePickerBG.width);
        Expect.AreEqual(PickerSize, picker.HuePickerBG.height);
        Expect.AreEqual(1, picker.AlphaPickerBG.width);
        Expect.AreEqual(PickerSize, picker.AlphaPickerBG.height);
    }

    [Test]
    public static void TheBackgroundsStartTheirGradientsAtTheBottom()
    {
        var picker = new Dialog_ColorPicker(Color.red);

        Expect.AreEqual(Color.black, picker.ColorPickerBG.GetPixel(0, 0));
        Expect.AreEqual(Color.red, picker.HuePickerBG.GetPixel(0, 0));
        Expect.AreEqual(new Color(1f, 0f, 0f, 0f), picker.AlphaPickerBG.GetPixel(0, 0));
    }

    [Test]
    public static void ChangingTheColorReplacesTheBackgrounds()
    {
        var picker = new Dialog_ColorPicker(Color.red);
        var colorPickerBG = picker.ColorPickerBG;
        var huePickerBG = picker.HuePickerBG;
        var alphaPickerBG = picker.AlphaPickerBG;

        picker.G = 1f;

        Expect.IsFalse(ReferenceEquals(colorPickerBG, picker.ColorPickerBG));
        Expect.IsFalse(ReferenceEquals(huePickerBG, picker.HuePickerBG));
        Expect.IsFalse(ReferenceEquals(alphaPickerBG, picker.AlphaPickerBG));
        Expect.ReferencesAreEqual(picker.AlphaPickerBG, picker.AlphaPickerBG);
        Expect.AreEqual(new Color(1f, 1f, 0f, 0f), picker.AlphaPickerBG.GetPixel(0, 0));
    }
}
