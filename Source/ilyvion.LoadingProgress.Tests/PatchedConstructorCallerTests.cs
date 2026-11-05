using System.Diagnostics;
using System.Reflection.Emit;
using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Patches;

namespace ilyvion.LoadingProgress.Tests;

/// <summary>
/// What a stack-walking API sees as its caller when it's called from a constructor patched the way
/// startup impact tracking patches every mod constructor, and the rewrite of calls to
/// Multiplayer's <c>MP.RegisterAll()</c>, which finds the assembly to register this way.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class PatchedConstructorCallerTests
{
    private const string TestHarmonyId = "ilyvion.LoadingProgress.Tests.PatchedConstructorCaller";

    private sealed class Constructed
    {
        public readonly MethodBase? Caller;
        public readonly Assembly? Executing;

        // A primary constructor can't carry the attribute that keeps this frame on the stack.
#pragma warning disable IDE0290
        [MethodImpl(MethodImplOptions.NoInlining)]
        public Constructed()
        {
            Caller = FindCaller();
            Executing = Assembly.GetExecutingAssembly();
        }
#pragma warning restore IDE0290
    }

    // The same frame MP.RegisterAll() reads: the method calling this one.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static MethodBase? FindCaller() => new StackTrace().GetFrame(1)?.GetMethod();

    private static Assembly? AssemblyOf(MethodBase? caller) => caller?.ReflectedType?.Assembly;

    // The shape of Multiplayer's API class.
    private static class FakeApi
    {
        public static Assembly? Registered;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RegisterAll() =>
            RegisterAll(new StackTrace().GetFrame(1)?.GetMethod()?.ReflectedType?.Assembly);

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void RegisterAll(Assembly? assembly) => Registered = assembly;

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Unrelated() { }
    }

    private sealed class ConstructedRegistering
    {
        // A branch lands on the call, so the rewrite has to keep the call's label.
        [MethodImpl(MethodImplOptions.NoInlining)]
        public ConstructedRegistering(bool callUnrelated)
        {
            if (callUnrelated)
            {
                FakeApi.Unrelated();
            }
            FakeApi.RegisterAll();
        }
    }

    public static void NoOpPrefix() { }

    public static IEnumerable<CodeInstruction> RedirectingTranspiler(
        IEnumerable<CodeInstruction> instructions
    ) =>
        Mod_Constructor_Patches.RedirectRegisterAll(
            instructions,
            typeof(ConstructedRegistering),
            typeof(FakeApi).FullName ?? ""
        );

    private static HarmonyMethod HookNamed(string name) =>
        new(typeof(PatchedConstructorCallerTests), name);

    [TearDown]
    public static void TearDown()
    {
        new Harmony(TestHarmonyId).UnpatchAll(TestHarmonyId);
        FakeApi.Registered = null;
    }

    [Test]
    public static void AnUnpatchedConstructorSeesItsOwnAssembly() =>
        Expect.AreEqual(typeof(Constructed).Assembly, AssemblyOf(new Constructed().Caller));

    // On 1.6 the frame gives Harmony's assembly, which registers nothing without an error. On 1.7
    // it gives no assembly, which throws.
    [Test]
    public static void APatchedConstructorDoesNotSeeItsOwnAssembly()
    {
        _ = new Harmony(TestHarmonyId).Patch(
            AccessTools.Constructor(typeof(Constructed)),
            prefix: HookNamed(nameof(NoOpPrefix))
        );

        Expect.AreNotEqual(typeof(Constructed).Assembly, AssemblyOf(new Constructed().Caller));
    }

    [Test]
    public static void APatchedConstructorGetsItsOwnExecutingAssembly()
    {
        _ = new Harmony(TestHarmonyId).Patch(
            AccessTools.Constructor(typeof(Constructed)),
            prefix: HookNamed(nameof(NoOpPrefix))
        );

        Expect.AreEqual(typeof(Constructed).Assembly, new Constructed().Executing);
    }

    [Test]
    public static void RegisterAllInAPatchedConstructorRegistersTheConstructorsAssembly(
        [Parameters(true, false)] bool callUnrelated
    )
    {
        _ = new Harmony(TestHarmonyId).Patch(
            AccessTools.Constructor(typeof(ConstructedRegistering), [typeof(bool)]),
            prefix: HookNamed(nameof(NoOpPrefix)),
            transpiler: HookNamed(nameof(RedirectingTranspiler))
        );

        _ = new ConstructedRegistering(callUnrelated);

        Expect.AreEqual(typeof(ConstructedRegistering).Assembly, FakeApi.Registered);
    }

    [Test]
    public static void OtherCallsAreLeftAlone()
    {
        var unrelated = new CodeInstruction(
            OpCodes.Call,
            AccessTools.DeclaredMethod(typeof(FakeApi), nameof(FakeApi.Unrelated))
        );
        var withAssembly = new CodeInstruction(
            OpCodes.Call,
            AccessTools.DeclaredMethod(
                typeof(FakeApi),
                nameof(FakeApi.RegisterAll),
                [typeof(Assembly)]
            )
        );

        var result = Mod_Constructor_Patches
            .RedirectRegisterAll(
                [unrelated, withAssembly],
                typeof(ConstructedRegistering),
                typeof(FakeApi).FullName ?? ""
            )
            .ToList();

        Expect.AreEqual(2, result.Count);
        Expect.ReferencesAreEqual(unrelated, result[0]);
        Expect.ReferencesAreEqual(withAssembly, result[1]);
    }
}
