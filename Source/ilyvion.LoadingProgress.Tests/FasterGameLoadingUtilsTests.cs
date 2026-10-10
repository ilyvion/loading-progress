using System.Runtime.Serialization;
using DevTools.Testing;
using ilyvion.LoadingProgress.FasterGameLoading;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class FasterGameLoadingUtilsTests
{
    private const string FasterGameLoadingId = "taranchuk.fastergameloading";

    // The game appends "_steam" to a Workshop mod's package id while a local copy with the
    // same id is installed. The lookup used to compare that id, so a player with both copies
    // had Faster Game Loading go unseen: its early content loading was neither shown nor
    // taken into account.
    [Test]
    public static void TheWorkshopCopyIsFoundDespiteThePostfix()
    {
        var mod = ModWithId(FasterGameLoadingId, steamPostfix: true);

        Expect.IsTrue(
            mod.PackageId.EndsWith(ModMetaData.SteamModPostfix, StringComparison.Ordinal)
        );
        Expect.IsTrue(FasterGameLoadingUtils.IsFasterGameLoading(mod));
    }

    [Test]
    public static void ALocalCopyIsFound() =>
        Expect.IsTrue(
            FasterGameLoadingUtils.IsFasterGameLoading(
                ModWithId(FasterGameLoadingId, steamPostfix: false)
            )
        );

    [Test]
    public static void AnotherModIsNotTakenForIt() =>
        Expect.IsFalse(
            FasterGameLoadingUtils.IsFasterGameLoading(
                ModWithId("taranchuk.performanceoptimizer", steamPostfix: true)
            )
        );

    // A ModMetaData made without its constructor, which reads a mod folder from disk; the two
    // fields set here are all the package id getters read.
    private static ModMetaData ModWithId(string packageId, bool steamPostfix)
    {
        var mod = (ModMetaData)FormatterServices.GetUninitializedObject(typeof(ModMetaData));
        mod.packageIdLowerCase = packageId;
        mod.appendPackageIdSteamPostfix = steamPostfix;
        return mod;
    }
}
