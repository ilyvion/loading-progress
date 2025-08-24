using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact.Patches;

[HarmonyPatch]
[HarmonyPatchCategory("StartupImpact")]
internal static class DefDatabase_AddAllInMods_Patches
{
    private static readonly MethodInfo _method_GenGeneric_InvokeStaticMethodOnGenericType
        = AccessTools.Method(
            typeof(GenGeneric),
            nameof(GenGeneric.InvokeStaticMethodOnGenericType),
            [typeof(Type), typeof(string), typeof(string)]);

    private static readonly CodeMatch[] toMatch =
    [
        new(OpCodes.Ldstr, "AddAllInMods"),
            new(OpCodes.Call, _method_GenGeneric_InvokeStaticMethodOnGenericType),
        ];

    internal static bool Prepare() => TargetMethods().Count() == 1;

    internal static IEnumerable<MethodBase> TargetMethods()
    {
        var methods = Utilities.FindMethodsDoing(typeof(PlayDataLoader), toMatch).ToList();
        if (methods.Count != 1)
        {
            LoadingProgressMod.Error("Could not find call to GenGeneric.InvokeStaticMethodOnGenericType in PlayDataLoader");
        }
        else
        {
            yield return methods.First();
        }
    }

    internal static void Prefix()
            => StartupImpactProfilerUtil.StartBaseGameProfiler("LoadingProgress.StartupImpact.DefDatabaseAddAllInMods");

    internal static void Postfix()
        => StartupImpactProfilerUtil.StopBaseGameProfiler("LoadingProgress.StartupImpact.DefDatabaseAddAllInMods");
}
