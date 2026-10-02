#if !v1_7_OR_GREATER
namespace ilyvion.LoadingProgress;

/// <summary>
/// Provides the <c>ArgumentOutOfRangeException.ThrowIf*</c> helpers, which .NET Framework lacks.
/// </summary>
internal static class ArgumentOutOfRangeExceptionExtensions
{
#pragma warning disable CA1034 // Extension blocks compile to a nested type.
    extension(System.ArgumentOutOfRangeException)
#pragma warning restore CA1034
    {
        /// <summary>Throws if <paramref name="value"/> is zero.</summary>
        /// <param name="value">The argument to validate as non-zero.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfZero<T>(
            T value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null
        )
            where T : struct, IComparable<T>
        {
            if (value.CompareTo(default) == 0)
            {
                Throw(paramName, value, $"{paramName} ('{value}') must be a non-zero value.");
            }
        }

        /// <summary>Throws if <paramref name="value"/> is negative.</summary>
        /// <param name="value">The argument to validate as non-negative.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfNegative<T>(
            T value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null
        )
            where T : struct, IComparable<T>
        {
            if (value.CompareTo(default) < 0)
            {
                Throw(paramName, value, $"{paramName} ('{value}') must be a non-negative value.");
            }
        }

        /// <summary>Throws if <paramref name="value"/> is negative or zero.</summary>
        /// <param name="value">The argument to validate as non-zero or non-negative.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfNegativeOrZero<T>(
            T value,
            [CallerArgumentExpression(nameof(value))] string? paramName = null
        )
            where T : struct, IComparable<T>
        {
            if (value.CompareTo(default) <= 0)
            {
                Throw(
                    paramName,
                    value,
                    $"{paramName} ('{value}') must be a non-negative and non-zero value."
                );
            }
        }

        /// <summary>Throws if <paramref name="value"/> is equal to <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as not equal to <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfEqual<T>(
            T value,
            T other,
            [CallerArgumentExpression(nameof(value))] string? paramName = null
        )
        {
            if (EqualityComparer<T>.Default.Equals(value, other))
            {
                Throw(paramName, value, $"{paramName} ('{value}') must not be equal to '{other}'.");
            }
        }

        /// <summary>Throws if <paramref name="value"/> is not equal to <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as equal to <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfNotEqual<T>(
            T value,
            T other,
            [CallerArgumentExpression(nameof(value))] string? paramName = null
        )
        {
            if (!EqualityComparer<T>.Default.Equals(value, other))
            {
                Throw(paramName, value, $"{paramName} ('{value}') must be equal to '{other}'.");
            }
        }

        /// <summary>Throws if <paramref name="value"/> is greater than <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as less than or equal to <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfGreaterThan<T>(
            T value,
            T other,
            [CallerArgumentExpression(nameof(value))] string? paramName = null
        )
            where T : IComparable<T>
        {
            if (value.CompareTo(other) > 0)
            {
                Throw(
                    paramName,
                    value,
                    $"{paramName} ('{value}') must be less than or equal to '{other}'."
                );
            }
        }

        /// <summary>Throws if <paramref name="value"/> is greater than or equal to <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as less than <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfGreaterThanOrEqual<T>(
            T value,
            T other,
            [CallerArgumentExpression(nameof(value))] string? paramName = null
        )
            where T : IComparable<T>
        {
            if (value.CompareTo(other) >= 0)
            {
                Throw(paramName, value, $"{paramName} ('{value}') must be less than '{other}'.");
            }
        }

        /// <summary>Throws if <paramref name="value"/> is less than <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as greater than or equal to <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfLessThan<T>(
            T value,
            T other,
            [CallerArgumentExpression(nameof(value))] string? paramName = null
        )
            where T : IComparable<T>
        {
            if (value.CompareTo(other) < 0)
            {
                Throw(
                    paramName,
                    value,
                    $"{paramName} ('{value}') must be greater than or equal to '{other}'."
                );
            }
        }

        /// <summary>Throws if <paramref name="value"/> is less than or equal to <paramref name="other"/>.</summary>
        /// <param name="value">The argument to validate as greater than <paramref name="other"/>.</param>
        /// <param name="other">The value to compare with <paramref name="value"/>.</param>
        /// <param name="paramName">The name of the parameter with which <paramref name="value"/> corresponds.</param>
        public static void ThrowIfLessThanOrEqual<T>(
            T value,
            T other,
            [CallerArgumentExpression(nameof(value))] string? paramName = null
        )
            where T : IComparable<T>
        {
            if (value.CompareTo(other) <= 0)
            {
                Throw(paramName, value, $"{paramName} ('{value}') must be greater than '{other}'.");
            }
        }
    }

    private static void Throw(string? paramName, object? value, string message) =>
        throw new System.ArgumentOutOfRangeException(paramName, value, message);
}
#endif
