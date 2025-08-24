using System.Reflection.Emit;

namespace ilyvion.LoadingProgress;

internal sealed class ReloadContentIntReplacement
{
    public static IEnumerable ReloadContentInt(ModContentPack modContentPack)
    {
        var info = LoadingProgressMod.instance.StartupImpact.Modlist.GetModInfoFor(modContentPack);

        yield return "audio clips";
        info?.Start("LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AudioClips");
        DeepProfiler.Start("Reload audio clips");
        try
        {
            modContentPack.audioClips.ReloadAll(false);
        }
        finally
        {
            DeepProfiler.End();
        }
        _ = info?.Stop("LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AudioClips");

        yield return "textures";
        info?.Start("LoadingProgress.StartupImpact.ModContentPackReloadContentInt.Textures");
        DeepProfiler.Start("Reload textures");
        try
        {
            modContentPack.textures.ReloadAll(false);
        }
        finally
        {
            DeepProfiler.End();
        }
        _ = info?.Stop("LoadingProgress.StartupImpact.ModContentPackReloadContentInt.Textures");

        yield return "strings";
        info?.Start("LoadingProgress.StartupImpact.ModContentPackReloadContentInt.Strings");
        DeepProfiler.Start("Reload strings");
        try
        {
            modContentPack.strings.ReloadAll(false);
        }
        finally
        {
            DeepProfiler.End();
        }
        _ = info?.Stop("LoadingProgress.StartupImpact.ModContentPackReloadContentInt.Strings");

        yield return "asset bundles";
        info?.Start("LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AssetBundles");
        DeepProfiler.Start("Reload asset bundles");
        try
        {
            modContentPack.assetBundles.ReloadAll(false);
            modContentPack.allAssetNamesInBundleCached = null;
            modContentPack.allAssetNamesInBundleCachedTrie = null;
        }
        finally
        {
            DeepProfiler.End();
        }
        _ = info?.Stop("LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AssetBundles");
    }
}

internal static partial class LongEventHandler_ExecuteToExecuteWhenFinished_Patches
{
    private static class ReloadContentIntFinder
    {
        private static readonly MethodInfo _method_ModContentPack_ReloadContentInt
            = AccessTools.Method(
                typeof(ModContentPack),
                nameof(ModContentPack.ReloadContentInt));

        private static readonly CodeMatch[] toMatch =
        [
            new(OpCodes.Call, _method_ModContentPack_ReloadContentInt),
        ];

        public static IEnumerable<(MethodInfo method, FieldInfo thisField)> FindMethodCalling()
        {
            // Find all possible candidates, both from the wrapping type and all nested types.
            var candidates = Utilities.FindInTypeAndInnerTypeMethods(
                typeof(ModContentPack), m => !m.IsGenericMethod);

            //check all candidates for the target instructions, return those that match.
            foreach (var method in candidates)
            {
                var instructions = PatchProcessor.GetCurrentInstructions(method);
                var matched = instructions.Matches(toMatch);
                if (matched)
                {
                    var field = AccessTools.GetDeclaredFields(method.DeclaringType)
                        .Single(f => f.Name.Contains("this", StringComparison.Ordinal));
                    yield return (method, field);
                }
            }
            yield break;
        }
    }
}
