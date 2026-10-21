using DevTools.Testing;
using ilyvion.LoadingProgress.RimThemes;

namespace ilyvion.LoadingProgress.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class RimThemesTests
{
    private const string RimThemesId = "arandomkiwi.rimthemes";

    [Test]
    public static void TheWorkshopCopyIsFoundDespiteThePostfix() =>
        Expect.IsTrue(RimThemesUtils.IsRimThemes(ModWithId(RimThemesId, steamPostfix: true)));

    [Test]
    public static void ALocalCopyIsFound() =>
        Expect.IsTrue(RimThemesUtils.IsRimThemes(ModWithId(RimThemesId, steamPostfix: false)));

    [Test]
    public static void AnotherModIsNotTakenForIt() =>
        Expect.IsFalse(
            RimThemesUtils.IsRimThemes(
                ModWithId("taranchuk.fastergameloading", steamPostfix: false)
            )
        );

    // Vanilla-sized screen with a Small-font label: the label spans y 1048 to 1070, while the
    // theme selector button spans 984 to 1080.
    [Test]
    public static void TheThemeSelectorEndsAGapAboveTheLabel()
    {
        var labelTop = VersionControl_DrawInfoInCorner_Patch.ComputeCornerLabelTop(1080f, 22f);
        Expect.AreEqual(1048f, labelTop);

        var y = RimThemes_DoMainMenuControls_Patch_Postfix_Patches.ComputeThemeSelectorY(
            1080f - 96f,
            labelTop
        );
        Expect.AreEqual(labelTop - GenUI.Gap, y + 96f);
    }

    [Test]
    public static void AThemeSelectorAlreadyAboveTheLabelStaysPut() =>
        Expect.AreEqual(
            500f,
            RimThemes_DoMainMenuControls_Patch_Postfix_Patches.ComputeThemeSelectorY(500f, 1048f)
        );

    // A ModMetaData made without its constructor, which reads a mod folder from disk; the two
    // fields set here are all the package id getters read.
    private static ModMetaData ModWithId(string packageId, bool steamPostfix)
    {
        var mod = AccessTools.CreateInstance<ModMetaData>();
        mod.packageIdLowerCase = packageId;
        mod.appendPackageIdSteamPostfix = steamPostfix;
        return mod;
    }
}
