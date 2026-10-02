#if !v1_7_OR_GREATER
using System.Diagnostics.CodeAnalysis;

namespace ilyvion.LoadingProgress;

/// <summary>
/// Provides <c>ArgumentNullException.ThrowIfNull</c>, which .NET Framework lacks.
/// </summary>
internal static class ArgumentNullExceptionExtensions
{
#pragma warning disable CA1034 // Extension blocks compile to a nested type.
    extension(System.ArgumentNullException)
#pragma warning restore CA1034
    {
        /// <summary>
        /// Throws an <see cref="System.ArgumentNullException"/> if <paramref name="argument"/> is
        /// <see langword="null"/>.
        /// </summary>
        /// <param name="argument">The reference type argument to validate as non-null.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="argument"/> corresponds.</param>
        public static void ThrowIfNull(
            [NotNull] object? argument,
            [CallerArgumentExpression(nameof(argument))] string? paramName = null
        )
        {
            if (argument is null)
            {
                throw new System.ArgumentNullException(paramName);
            }
        }
    }
}
#endif
