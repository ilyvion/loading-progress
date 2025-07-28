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
