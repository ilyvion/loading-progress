#if !v1_7_OR_GREATER
using System.Text;

namespace ilyvion.LoadingProgress;

/// <summary>
/// Provides <c>StringBuilder.Append(IFormatProvider, ref AppendInterpolatedStringHandler)</c>,
/// which .NET Framework lacks.
/// </summary>
internal static class StringBuilderAppendExtensions
{
    /// <summary>
    /// Appends <paramref name="handler"/>'s interpolated string to <paramref name="builder"/>,
    /// formatting its values with <paramref name="provider"/>.
    /// </summary>
    /// <param name="builder">The builder to append to.</param>
    /// <param name="provider">The culture-specific formatting information to use.</param>
    /// <param name="handler">The interpolated string, which writes itself to <paramref name="builder"/>.</param>
    /// <returns><paramref name="builder"/>.</returns>
#pragma warning disable IDE0060 // The compiler passes provider to the handler, which does the appending.
    public static StringBuilder Append(
        this StringBuilder builder,
        IFormatProvider? provider,
        [InterpolatedStringHandlerArgument(nameof(builder), nameof(provider))]
            ref StringBuilderAppendInterpolatedStringHandler handler
    ) => builder;
#pragma warning restore IDE0060
}

/// <summary>
/// Writes an interpolated string straight to a <see cref="StringBuilder"/>, for
/// <see cref="StringBuilderAppendExtensions.Append"/>.
/// </summary>
/// <param name="literalLength">The number of constant characters in the interpolated string.</param>
/// <param name="formattedCount">The number of interpolation holes in the interpolated string.</param>
/// <param name="builder">The builder to append to.</param>
/// <param name="provider">The culture-specific formatting information to use.</param>
[InterpolatedStringHandler]
#pragma warning disable CA1815 // Only constructed by the compiler for an interpolated string.
#pragma warning disable CS9113 // The compiler's handler pattern requires literalLength and formattedCount.
internal readonly struct StringBuilderAppendInterpolatedStringHandler(
    int literalLength,
    int formattedCount,
    StringBuilder builder,
    IFormatProvider? provider
)
#pragma warning restore CS9113
#pragma warning restore CA1815
{
    private readonly StringBuilder _builder = builder;
    private readonly IFormatProvider? _provider = provider;
    private readonly ICustomFormatter? _customFormatter =
        provider?.GetFormat(typeof(ICustomFormatter)) as ICustomFormatter;

    /// <summary>Appends a constant part of the interpolated string.</summary>
    /// <param name="value">The constant text.</param>
    public void AppendLiteral(string value) => _builder.Append(value);

    /// <summary>Appends a value.</summary>
    /// <param name="value">The value to append.</param>
    public void AppendFormatted<T>(T value) => AppendFormatted(value, 0, null);

    /// <summary>Appends a value with a format string.</summary>
    /// <param name="value">The value to append.</param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, string? format) => AppendFormatted(value, 0, format);

    /// <summary>Appends a value padded to <paramref name="alignment"/>.</summary>
    /// <param name="value">The value to append.</param>
    /// <param name="alignment">The minimum width; negative left-aligns the value.</param>
    public void AppendFormatted<T>(T value, int alignment) =>
        AppendFormatted(value, alignment, null);

    /// <summary>Appends a value with a format string, padded to <paramref name="alignment"/>.</summary>
    /// <param name="value">The value to append.</param>
    /// <param name="alignment">The minimum width; negative left-aligns the value.</param>
    /// <param name="format">The format string.</param>
    public void AppendFormatted<T>(T value, int alignment, string? format)
    {
        var text =
            _customFormatter is not null ? _customFormatter.Format(format, value, _provider)
            : value is IFormattable formattable ? formattable.ToString(format, _provider)
            : value?.ToString();
        text ??= string.Empty;

        var padding = Math.Abs(alignment) - text.Length;
        if (padding > 0 && alignment > 0)
        {
            _ = _builder.Append(' ', padding);
        }
        _ = _builder.Append(text);
        if (padding > 0 && alignment < 0)
        {
            _ = _builder.Append(' ', padding);
        }
    }
}
#endif
