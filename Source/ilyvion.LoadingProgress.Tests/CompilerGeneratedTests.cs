using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

// One check serves both the deferred-action owner, which looks inside the closures it finds,
// and the names given to long events, which walk out of them.
[TestFixture(TestType.MainMenu)]
internal sealed class CompilerGeneratedTests
{
    [Test]
    public static void AClosureIsCompilerGenerated()
    {
        var captured = 0;
        Action action = () => captured++;

        Expect.IsTrue(CompilerGenerated.Is(action.Target.GetType()));
    }

    [Test]
    public static void ALambdaThatCapturesNothingIsCompilerGenerated()
    {
        Action action = () => { };

        Expect.IsTrue(CompilerGenerated.Is(action.Target.GetType()));
    }

    [Test]
    public static void AnIteratorIsCompilerGenerated() =>
        Expect.IsTrue(CompilerGenerated.Is(Numbers().GetType()));

    [Test]
    public static void AWrittenTypeIsNot()
    {
        Expect.IsFalse(CompilerGenerated.Is(typeof(CompilerGeneratedTests)));
        Expect.IsFalse(CompilerGenerated.Is(typeof(ThingDef)));
    }

    // A file-local type's name starts with '<' too, but its author wrote it: a def it holds
    // must not move a deferred action from the code's mod to the def's.
    [Test]
    public static void AFileLocalTypeIsNot() =>
        Expect.IsFalse(CompilerGenerated.Is(typeof(FileLocalHolder)));

    private static IEnumerable<int> Numbers()
    {
        yield return 1;
    }
}

file sealed class FileLocalHolder { }
