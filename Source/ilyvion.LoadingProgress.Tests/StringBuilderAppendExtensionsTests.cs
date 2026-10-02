using System.Text;
using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class StringBuilderAppendExtensionsTests
{
    private static readonly NumberFormatInfo CommaDecimals = new() { NumberDecimalSeparator = "," };

    private sealed class Shouting : IFormatProvider, ICustomFormatter
    {
        public object? GetFormat(Type? formatType) =>
            formatType == typeof(ICustomFormatter) ? this : null;

        public string Format(string? format, object? arg, IFormatProvider? formatProvider) =>
            $"{arg}!".ToUpperInvariant();
    }

    [Test]
    public static void ValuesAreFormattedWithTheProvider()
    {
        var value = 1.5;

        Expect.AreEqual("1,5", new StringBuilder().Append(CommaDecimals, $"{value}").ToString());
        Expect.AreEqual(
            "1.5",
            new StringBuilder().Append(CultureInfo.InvariantCulture, $"{value}").ToString()
        );
    }

    [Test]
    public static void LiteralsAndValuesAreAppendedInOrder()
    {
        var name = "pawns";
        var count = 3;
        var builder = new StringBuilder("Found ");

        var returned = builder.Append(CultureInfo.InvariantCulture, $"{count} {name}.");

        Expect.IsTrue(ReferenceEquals(builder, returned));
        Expect.AreEqual("Found 3 pawns.", builder.ToString());
    }

    [Test]
    public static void FormatStringsAndAlignmentAreApplied()
    {
        var value = 255;

        Expect.AreEqual(
            "[FF][  255][255  ][   FF]",
            new StringBuilder()
                .Append(
                    CultureInfo.InvariantCulture,
                    $"[{value:X}][{value, 5}][{value, -5}][{value, 5:X}]"
                )
                .ToString()
        );
    }

    [Test]
    public static void NullValuesAppendNothing()
    {
        string? text = null;
        object? value = null;

        Expect.AreEqual(
            "[][]",
            new StringBuilder()
                .Append(CultureInfo.InvariantCulture, $"[{text}][{value}]")
                .ToString()
        );
    }

    [Test]
    public static void ACustomFormatterFormatsEveryValue()
    {
        var name = "pawn";
        var count = 2;

        Expect.AreEqual(
            "PAWN! x 2!",
            new StringBuilder().Append(new Shouting(), $"{name} x {count}").ToString()
        );
    }
}
