using System.Text;
using DevTools.Testing;
using ilyvion.LoadingProgress.StartupImpact;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class DeferredActionOwnerTests
{
    private static ModContentPack FirstPack => LoadedModManager.RunningModsListForReading[0];

    private static ModContentPack AnotherPack =>
        LoadedModManager.RunningModsListForReading.Count > 1
            ? LoadedModManager.RunningModsListForReading[1]
            : FirstPack;

    // The mod this test assembly is loaded from.
    private static ModContentPack? TestsPack =>
        Utilities.FindModByAssembly(typeof(DeferredActionOwnerTests).Assembly);

    // The engine queues one deferred action per def for its graphic and reference setup, all
    // from its own assembly, so going by the assembly filed every one of them under the base
    // game whatever mod the def came from.
    [Test]
    public static void AnActionOnTheDefItselfBelongsToTheDefsMod()
    {
        var def = new ThingDef { modContentPack = FirstPack };
        Action action = def.PostLoad;

        Expect.ReferencesAreEqual(FirstPack, DeferredActionOwner.OwningContentPack(action));
    }

    // A def passing itself into a property's setup captures itself in a closure instead.
    [Test]
    public static void AClosureHoldingTheDefBelongsToTheDefsMod()
    {
        var def = new ThingDef { modContentPack = FirstPack };
        Action action = () => _ = def.defName;

        Expect.ReferencesAreEqual(FirstPack, DeferredActionOwner.OwningContentPack(action));
    }

    // A framework's per-def work captures another mod's def in its closure; the def is found.
    [Test]
    public static void ADefFromAnotherModIsFoundThroughTheClosure()
    {
        var def = new TerrainDef { modContentPack = AnotherPack };
        Action action = () => _ = def.defName;

        Expect.ReferencesAreEqual(AnotherPack, DeferredActionOwner.OwningContentPack(action));
    }

    // This test assembly belongs to a mod of its own, and the action is still credited to the
    // mod that declared the def: a framework's per-def work belongs to the def's mod.
    [Test]
    public static void TheDefsModWinsOverTheCodesMod()
    {
        Expect.AreNotEqual(TestsPack, AnotherPack);
        var def = new TerrainDef { modContentPack = AnotherPack };
        Action action = () => _ = def.defName;

        var owner = StartupImpactProfilerUtil.OwnerOfDeferredAction(action, out var isBaseGame);

        Expect.ReferencesAreEqual(AnotherPack, owner);
        Expect.IsFalse(isBaseGame);
    }

    [Test]
    public static void WithoutADefTheCodesModOwnsTheAction()
    {
        Action action = static () => { };

        var owner = StartupImpactProfilerUtil.OwnerOfDeferredAction(action, out var isBaseGame);

        Expect.IsNotNull(TestsPack);
        Expect.ReferencesAreEqual(TestsPack, owner);
        Expect.IsFalse(isBaseGame);
    }

    // The engine's own work that traces to no def stays with the base game.
    [Test]
    public static void TheEnginesOwnActionBelongsToTheBaseGame()
    {
        Action action = LongEventHandler.ClearQueuedEvents;

        var owner = StartupImpactProfilerUtil.OwnerOfDeferredAction(action, out var isBaseGame);

        Expect.IsNull(owner);
        Expect.IsTrue(isBaseGame);
    }

    // Code no mod owns, such as the runtime's, is neither a mod's nor the base game's.
    [Test]
    public static void CodeNoModOwnsBelongsToNeither()
    {
        var holder = new StringBuilder();
        Func<string> action = holder.ToString;

        var owner = StartupImpactProfilerUtil.OwnerOfDeferredAction(action, out var isBaseGame);

        Expect.IsNull(owner);
        Expect.IsFalse(isBaseGame);
    }

    // Start and Stop time deferred actions and post-load events on the timer this picks for
    // their owner. Code no mod owns gets none, and is left untimed.
    [Test]
    public static void AnOwnersCodeIsTimedOnItsProfiler()
    {
        var startupImpact = LoadingProgressMod.instance.StartupImpact;
        Expect.IsNotNull(TestsPack);

        Expect.ReferencesAreEqual(
            startupImpact.Modlist.GetModInfoFor(TestsPack)?.Profiler,
            StartupImpactProfilerUtil.ProfilerFor(TestsPack, isBaseGame: false)
        );
        Expect.ReferencesAreEqual(
            startupImpact.BaseGameProfiler,
            StartupImpactProfilerUtil.ProfilerFor(null, isBaseGame: true)
        );
        Expect.IsNull(StartupImpactProfilerUtil.ProfilerFor(null, isBaseGame: false));
    }

    [Test]
    public static void AnActionWithNoTargetHasNoOwnerHere()
    {
        Action action = static () => { };

        Expect.IsNull(DeferredActionOwner.OwningContentPack(action));
    }

    [Test]
    public static void ADefWithoutAModHasNoOwnerHere()
    {
        var def = new ThingDef();
        Action action = def.PostLoad;

        Expect.IsNull(DeferredActionOwner.OwningContentPack(action));
    }

    [Test]
    public static void AnUnrelatedTargetHasNoOwnerHere()
    {
        var holder = new StringBuilder();
        Func<string> action = holder.ToString;

        Expect.IsNull(DeferredActionOwner.OwningContentPack(action));
    }
}
