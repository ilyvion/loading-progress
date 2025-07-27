// Based on code from
// <https://github.com/PeteTimesSix/TranspilerExamples/blob/main/Source/HarmonyPatchExamples/CompilerGeneratedClasses.cs>

using System.Reflection;
using System.Reflection.Emit;

namespace ilyvion.LoadingProgress;

[HarmonyPatch]
internal static class PlayDataLoader_DoPlayLoad_StaticConstructorOnStartup_Patch
{
    private static readonly MethodInfo _method_StaticConstructorOnStartupUtility_CallAll
        = AccessTools.Method(
            typeof(StaticConstructorOnStartupUtility), nameof(StaticConstructorOnStartupUtility.CallAll));

    private static readonly CodeMatch[] toMatch =
    [
        new(OpCodes.Call, _method_StaticConstructorOnStartupUtility_CallAll),
    ];

    // Find the method we need to intercept. Otherwise, Harmony throws an error if given no methods in HarmonyTargetMethods.
    [HarmonyPrepare]
    private static bool ShouldPatch()
    {
        if (!LoadingProgressMod.Settings.PatchInitialization)
        {
            LoadingProgressMod.Message("Patching of initialization code is disabled in the settings, skipping patch.");
            return false;
        }

        // We can reuse the same method as HarmonyTargetMethods will use afterward.
        var methods = FindMethod();

        //check that we have one and only one match. If we get more, the match is giving false positives.
        if (methods.Count() == 1)
        {
            return true;
        }
        else
        {
            LoadingProgressMod.Error("Could not find call to StaticConstructorOnStartupUtility.CallAll in PlayDataLoader.");
            return false;
        }
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodInfo> FindMethod()
    {
        // Find all possible candidates, both from the wrapping type and all nested types.
        var candidates = AccessTools.GetDeclaredMethods(typeof(PlayDataLoader)).ToHashSet();
        candidates.AddRange(typeof(PlayDataLoader).GetNestedTypes(AccessTools.all).SelectMany(AccessTools.GetDeclaredMethods));

        //check all candidates for the target instructions, return those that match.
        foreach (var method in candidates)
        {
            var instructions = PatchProcessor.GetCurrentInstructions(method);
            var matched = instructions.Matches(toMatch);
            if (matched)
            {
                yield return method;
            }
        }
        yield break;
    }
    private static bool Prefix()
    {
        var queue = LongEventHandler.eventQueue.ToList();
        LongEventHandler.eventQueue.Clear();

        LongEventHandler.QueueLongEvent(PlayDataLoaderMethods.CallingAllStaticConstructors(), "LoadingProgress.CallingAllStaticConstructors");
        LongEventHandler.QueueLongEvent(FloatMenuMakerMap.Init, "LoadingProgress.FloatMenuMakerMap", false, null);
        LongEventHandler.QueueLongEvent(PlayDataLoaderMethods.AtlasBaking, "LoadingProgress.AtlasBaking", false, null);
        LongEventHandler.QueueLongEvent(PlayDataLoaderMethods.GarbageCollection, "LoadingProgress.GarbageCollection", false, null);

        foreach (var queuedEvent in queue)
        {
            LongEventHandler.eventQueue.Enqueue(queuedEvent);
        }

        return false;
    }
}