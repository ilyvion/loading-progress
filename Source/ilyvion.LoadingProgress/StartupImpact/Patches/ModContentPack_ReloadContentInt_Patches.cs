using System.Reflection.Emit;

namespace ilyvion.LoadingProgress.StartupImpact.Patches;

/// <summary>
/// Times each content reload step under the mod whose content it loads, whoever calls
/// <see cref="ModContentPack.ReloadContentInt"/>, such as another mod's early content loader,
/// so a hook replacing a step's work inside it is credited to that step.
/// </summary>
[HarmonyPatch(typeof(ModContentPack), nameof(ModContentPack.ReloadContentInt))]
[HarmonyPatchCategory("StartupImpact")]
internal static class ModContentPack_ReloadContentInt_Patches
{
    internal const string AudioClipsCategory =
        "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AudioClips";
    internal const string TexturesCategory =
        "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.Textures";
    internal const string StringsCategory =
        "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.Strings";
    internal const string AssetBundlesCategory =
        "LoadingProgress.StartupImpact.ModContentPackReloadContentInt.AssetBundles";

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        const string ReloadAll = nameof(ModContentHolder<>.ReloadAll);
        (MethodInfo reloadAll, string helper)[] replacements =
        [
            (
                AccessTools.Method(typeof(ModContentHolder<AudioClip>), ReloadAll),
                nameof(ReloadAudioClips)
            ),
            (
                AccessTools.Method(typeof(ModContentHolder<Texture2D>), ReloadAll),
                nameof(ReloadTextures)
            ),
            (
                AccessTools.Method(typeof(ModContentHolder<string>), ReloadAll),
                nameof(ReloadStrings)
            ),
            (
                AccessTools.Method(
                    typeof(ModAssetBundlesHandler),
                    nameof(ModAssetBundlesHandler.ReloadAll)
                ),
                nameof(ReloadAssetBundles)
            ),
        ];

        var found = new bool[replacements.Length];
        foreach (var instruction in instructions)
        {
            var index = Array.FindIndex(replacements, r => instruction.Calls(r.reloadAll));
            if (index < 0)
            {
                yield return instruction;
                continue;
            }

            // The holder and hotReload are on the stack; the helper also takes the pack.
            found[index] = true;
            yield return new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction);
            yield return new CodeInstruction(
                OpCodes.Call,
                AccessTools.Method(
                    typeof(ModContentPack_ReloadContentInt_Patches),
                    replacements[index].helper
                )
            ).MoveBlocksFrom(instruction);
        }

        for (var i = 0; i < replacements.Length; i++)
        {
            if (!found[i])
            {
                LoadingProgressMod.Warning(
                    $"ModContentPack.ReloadContentInt: Could not find a call to {replacements[i].reloadAll.DeclaringType}.ReloadAll; "
                        + "its time will not be shown under its loading step."
                );
            }
        }
    }

    internal static void ReloadAudioClips(
        ModContentHolder<AudioClip> holder,
        bool hotReload,
        ModContentPack mod
    )
    {
        StartupImpactProfilerUtil.StartModProfiler(mod, AudioClipsCategory);
        try
        {
            holder.ReloadAll(hotReload);
        }
        finally
        {
            StartupImpactProfilerUtil.StopModProfiler(mod, AudioClipsCategory);
        }
    }

    internal static void ReloadTextures(
        ModContentHolder<Texture2D> holder,
        bool hotReload,
        ModContentPack mod
    )
    {
        StartupImpactProfilerUtil.StartModProfiler(mod, TexturesCategory);
        try
        {
            holder.ReloadAll(hotReload);
        }
        finally
        {
            StartupImpactProfilerUtil.StopModProfiler(mod, TexturesCategory);
        }
    }

    internal static void ReloadStrings(
        ModContentHolder<string> holder,
        bool hotReload,
        ModContentPack mod
    )
    {
        StartupImpactProfilerUtil.StartModProfiler(mod, StringsCategory);
        try
        {
            holder.ReloadAll(hotReload);
        }
        finally
        {
            StartupImpactProfilerUtil.StopModProfiler(mod, StringsCategory);
        }
    }

    internal static void ReloadAssetBundles(
        ModAssetBundlesHandler handler,
        bool hotReload,
        ModContentPack mod
    )
    {
        StartupImpactProfilerUtil.StartModProfiler(mod, AssetBundlesCategory);
        try
        {
            handler.ReloadAll(hotReload);
        }
        finally
        {
            StartupImpactProfilerUtil.StopModProfiler(mod, AssetBundlesCategory);
        }
    }
}
