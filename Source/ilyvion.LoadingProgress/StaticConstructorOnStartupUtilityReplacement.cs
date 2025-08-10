using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ilyvion.LoadingProgress;

internal class StaticConstructorOnStartupUtilityReplacement
{
    internal static void Interject()
    {
        Utilities.LongEventHandlerPrependQueue(() =>
        {
            LongEventHandler.QueueLongEvent(CallAll(), "LoadingProgress.CallAll");
            // When we're done, resume the original ExecuteToExecuteWhenFinished method.
            LongEventHandler.QueueLongEvent(LongEventHandler_ExecuteToExecuteWhenFinished_Patches.ExecuteToExecuteWhenFinished(), "LoadingProgress.ExecuteToExecuteWhenFinished");
        });
    }

    internal static bool _callAllCalled = false;
    private static IEnumerable CallAll()
    {
        _callAllCalled = true;
        DeepProfiler.Start("StaticConstructorOnStartupUtilityReplacement.CallAll()");
        List<Type> list = GenTypes.AllTypesWithAttribute<StaticConstructorOnStartup>();
        for (int i = 0; i < list.Count; i++)
        {
            Type item = list[i];
            try
            {
                LoadingProgressWindow.SetCurrentLoadingActivityRaw(item.ToString());
                LoadingProgressWindow.StageProgress = (i + 1, list.Count);

                RuntimeHelpers.RunClassConstructor(item.TypeHandle);
            }
            catch (Exception ex)
            {
                Log.Error("Error in static constructor of " + item?.ToString() + ": " + ex);
            }
            yield return null;
        }
        DeepProfiler.End();
        StaticConstructorOnStartupUtility.coreStaticAssetsLoaded = true;
    }
}

internal static partial class LongEventHandler_ExecuteToExecuteWhenFinished_Patches
{
    private static class StaticConstructorOnStartupCallAllFinder
    {
        private static readonly MethodInfo _method_StaticConstructorOnStartupUtility_CallAll
            = AccessTools.Method(
                typeof(StaticConstructorOnStartupUtility),
                nameof(StaticConstructorOnStartupUtility.CallAll));

        private static readonly CodeMatch[] toMatch =
        [
            new(OpCodes.Call, _method_StaticConstructorOnStartupUtility_CallAll),
        ];

        public static IEnumerable<MethodInfo> FindMethodCalling()
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
    }
}
