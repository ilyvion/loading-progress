using System.Reflection.Emit;
using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact.Patches;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class LoadModXmlTimingTests
{
    // The timing used to swap the call to LoadDefs in LoadModXML for a wrapper of its own, so
    // another mod's transpiler on LoadModXML that looks for the call, applied after this one,
    // would not find it. The call now stays, and the defs it returns are read through the
    // timing, which is handed the same mod.
    [Test]
    public static void TheTranspiledLoopStillCallsLoadDefs()
    {
        var original = PatchProcessor.GetOriginalInstructions(Method, out var generator);

        var patched = LoadedModManager_LoadModXML.Transpiler(original, generator, Method).ToList();

        var call = patched.FindIndex(instruction => instruction.Calls(LoadDefs));
        Expect.GreaterThanOrEqualTo(call, 2);
        var loadMod = patched[call - 2];
        Expect.AreEqual(
            typeof(ModContentPack),
            LoadedModManager_LoadModXML.LocalTypeLoaded(loadMod, Method)
        );
        Expect.AreEqual(loadMod.opcode, patched[call + 1].opcode);
        Expect.AreEqual(loadMod.operand, patched[call + 1].operand);
        Expect.IsTrue(patched[call + 2].Calls(ReadDefsTimed));
    }

    // The transpiler takes the instruction two before the call to LoadDefs as the one that
    // loads the mod, and passes the same load on to the timing. It used to check only that
    // the instruction loaded some local, so another mod's transpiler leaving a different
    // local there would have had that local passed on as the mod, and the patched method
    // would fail. A local of any other type now leaves LoadModXML untimed.
    [Test]
    [ErrorsAllowed("Could not find the mod ModContentPack.LoadDefs is called on")]
    public static void ALocalOfAnotherTypeBeforeTheCallLeavesLoadModXmlUntimed()
    {
        var original = PatchProcessor.GetOriginalInstructions(Method, out var generator);
        var call = original.FindIndex(instruction => instruction.Calls(LoadDefs));
        Expect.GreaterThanOrEqualTo(call, 2);
        original[call - 2] = new CodeInstruction(
            OpCodes.Ldloc_S,
            generator.DeclareLocal(typeof(bool))
        );

        var patched = LoadedModManager_LoadModXML.Transpiler(original, generator, Method).ToList();

        Expect.IsFalse(patched.Any(instruction => instruction.Calls(ReadDefsTimed)));
    }

    private static MethodInfo Method =>
        AccessTools.Method(typeof(LoadedModManager), nameof(LoadedModManager.LoadModXML));

    private static MethodInfo LoadDefs =>
        AccessTools.Method(typeof(ModContentPack), nameof(ModContentPack.LoadDefs));

    private static MethodInfo ReadDefsTimed =>
        AccessTools.Method(
            typeof(LoadedModManager_LoadModXML),
            nameof(LoadedModManager_LoadModXML.ReadDefsTimed)
        );
}
