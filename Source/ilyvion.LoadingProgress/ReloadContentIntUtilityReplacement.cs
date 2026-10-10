using System.Reflection.Emit;
using ilyvion.LoadingProgress.StartupImpact.Patches;

namespace ilyvion.LoadingProgress;

internal sealed class ReloadContentIntReplacement
{
    public static IEnumerable ReloadContentInt(ModContentPack modContentPack)
    {
        yield return "audio clips";
        DeepProfiler.Start("Reload audio clips");
        try
        {
            ModContentPack_ReloadContentInt_Patches.ReloadAudioClips(
                modContentPack.audioClips,
                false,
                modContentPack
            );
        }
        finally
        {
            DeepProfiler.End();
        }
        // Re-yield the same label so the caller gets a chance to repaint with it still
        // showing, before we move on and set the *next* label. Without this, a slow step
        // here gets displayed under the following step's name; see the comment on the
        // matching yield in StaticConstructorOnStartupUtilityReplacement.CallAll().
        yield return "audio clips";

        yield return "textures";
        DeepProfiler.Start("Reload textures");
        try
        {
            ModContentPack_ReloadContentInt_Patches.ReloadTextures(
                modContentPack.textures,
                false,
                modContentPack
            );
        }
        finally
        {
            DeepProfiler.End();
        }
        yield return "textures";

        yield return "strings";
        DeepProfiler.Start("Reload strings");
        try
        {
            ModContentPack_ReloadContentInt_Patches.ReloadStrings(
                modContentPack.strings,
                false,
                modContentPack
            );
        }
        finally
        {
            DeepProfiler.End();
        }
        yield return "strings";

        yield return "asset bundles";
        DeepProfiler.Start("Reload asset bundles");
        try
        {
            ModContentPack_ReloadContentInt_Patches.ReloadAssetBundles(
                modContentPack.assetBundles,
                false,
                modContentPack
            );
            modContentPack.allAssetNamesInBundleCached = null;
            modContentPack.allAssetNamesInBundleCachedTrie = null;
        }
        finally
        {
            DeepProfiler.End();
        }
        yield return "asset bundles";
    }
}

internal static partial class LongEventHandler_ExecuteToExecuteWhenFinished_Patches
{
    private static class ReloadContentIntFinder
    {
        private static readonly MethodInfo _method_ModContentPack_ReloadContentInt =
            AccessTools.Method(typeof(ModContentPack), nameof(ModContentPack.ReloadContentInt));

        private static readonly CodeMatch[] toMatch =
        [
            new(OpCodes.Call, _method_ModContentPack_ReloadContentInt),
        ];

        public static IEnumerable<(MethodInfo method, FieldInfo thisField)> FindMethodCalling()
        {
            // Find all possible candidates, both from the wrapping type and all nested types.
            var candidates = Utilities.FindInTypeAndInnerTypeMethods(
                typeof(ModContentPack),
                m => !m.ContainsGenericParameters
            );

            //check all candidates for the target instructions, return those that match.
            foreach (var method in candidates)
            {
                var instructions = PatchProcessor.GetCurrentInstructions(method);
                var matched = instructions.Matches(toMatch);
                if (matched)
                {
                    var field = AccessTools
                        .GetDeclaredFields(method.DeclaringType)
                        .SingleOrDefault(f => f.Name.Contains("this", StringComparison.Ordinal));
                    if (field is null)
                    {
                        LoadingProgressMod.Error(
                            $"Could not find closure field on {method.DeclaringType} "
                                + $"for method {method}({method.FullDescription()}); skipping candidate."
                        );
                        continue;
                    }
                    yield return (method, field);
                }
            }
            yield break;
        }
    }
}
