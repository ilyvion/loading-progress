using DevTools.Testing;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ThrowHelperExtensionsTests
{
    private static TException? Catch<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException e)
        {
            return e;
        }
        return null;
    }

    [Test]
    public static void ThrowIfNullThrowsWithArgumentNameOnNull()
    {
        object? argument = null;

        var caught = Catch<ArgumentNullException>(() =>
            ArgumentNullException.ThrowIfNull(argument)
        );

        Expect.IsTrue(caught?.GetType() == typeof(ArgumentNullException));
        Expect.AreEqual(nameof(argument), caught?.ParamName);
    }

    [Test]
    public static void ThrowIfNullDoesNotThrowOnNonNull()
    {
        var argument = new object();

        var caught = Catch<ArgumentNullException>(() =>
            ArgumentNullException.ThrowIfNull(argument)
        );

        Expect.IsTrue(caught == null);
    }

    [Test]
    public static void ThrowIfNegativeThrowsWithArgumentNameAndValueOnNegative()
    {
        var count = -1;

        var caught = Catch<ArgumentOutOfRangeException>(() =>
            ArgumentOutOfRangeException.ThrowIfNegative(count)
        );

        Expect.IsTrue(caught?.GetType() == typeof(ArgumentOutOfRangeException));
        Expect.AreEqual(nameof(count), caught?.ParamName);
        Expect.IsTrue(Equals(caught?.ActualValue, -1));
    }

    [Test]
    public static void SignHelpersAcceptAndRejectTheExpectedValues()
    {
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfNegative(0))
                == null
        );
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfZero(0))
                != null
        );
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfZero(-1f))
                == null
        );
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() =>
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(0L)
            ) != null
        );
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() =>
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(0.5)
            ) == null
        );
    }

    [Test]
    public static void ComparisonHelpersAcceptAndRejectTheExpectedValues()
    {
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() => ArgumentOutOfRangeException.ThrowIfEqual(1, 1))
                != null
        );
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() =>
                ArgumentOutOfRangeException.ThrowIfNotEqual(1, 1)
            ) == null
        );
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() =>
                ArgumentOutOfRangeException.ThrowIfGreaterThan(2, 1)
            ) != null
        );
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() =>
                ArgumentOutOfRangeException.ThrowIfGreaterThan(1, 1)
            ) == null
        );
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() =>
                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(1, 1)
            ) != null
        );
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() =>
                ArgumentOutOfRangeException.ThrowIfLessThan(0, 1)
            ) != null
        );
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() =>
                ArgumentOutOfRangeException.ThrowIfLessThan(1, 1)
            ) == null
        );
        Expect.IsTrue(
            Catch<ArgumentOutOfRangeException>(() =>
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(1, 1)
            ) != null
        );
    }
}
