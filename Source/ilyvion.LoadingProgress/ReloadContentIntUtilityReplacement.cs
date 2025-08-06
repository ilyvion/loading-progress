using System.Reflection;
using System.Reflection.Emit;

namespace ilyvion.LoadingProgress;

internal class ReloadContentIntReplacement
{
    public static IEnumerable ReloadContentInt(ModContentPack modContentPack)
    {
        yield return "audio clips";
        DeepProfiler.Start("Reload audio clips");
        try
        {
            modContentPack.audioClips.ReloadAll(false);
        }
        finally
        {
            DeepProfiler.End();
        }

        yield return "textures";
        DeepProfiler.Start("Reload textures");
        try
        {
            modContentPack.textures.ReloadAll(false);
        }
        finally
        {
            DeepProfiler.End();
        }

        yield return "strings";
        DeepProfiler.Start("Reload strings");
        try
        {
            modContentPack.strings.ReloadAll(false);
        }
        finally
        {
            DeepProfiler.End();
        }

        yield return "asset bundles";
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

        public static IEnumerable<(MethodInfo method, FieldInfo thisField)> FindMethod()
        {
            // Find all possible candidates, both from the wrapping type and all nested types.
            var candidates = AccessTools
                .GetDeclaredMethods(typeof(ModContentPack))
                .Where(m => !m.IsGenericMethod)
                .ToHashSet();
            candidates.AddRange(
                typeof(ModContentPack)
                    .GetNestedTypes(AccessTools.all)
                    .SelectMany(AccessTools.GetDeclaredMethods)
                    .Where(m => !m.IsGenericMethod));

            //check all candidates for the target instructions, return those that match.
            foreach (var method in candidates)
            {
                var instructions = PatchProcessor.GetCurrentInstructions(method);
                var matched = instructions.Matches(toMatch);
                if (matched)
                {
                    var field = AccessTools.GetDeclaredFields(method.DeclaringType)
                        .Single(f => f.Name.Contains("this"));
                    yield return (method, field);
                }
            }
            yield break;
        }
    }
}
