
using System.Reflection;
using System.Runtime.CompilerServices;
using RimWorld.IO;
using UnityEngine;

namespace ilyvion.LoadingProgress;

public static class PlayDataLoaderMethods
{
    private static readonly MethodInfo _method_StaticConstructorOnStartupUtility_CallAll
        = AccessTools.Method(
            typeof(StaticConstructorOnStartupUtility), nameof(StaticConstructorOnStartupUtility.CallAll));

    public static IEnumerable CallingAllStaticConstructors()
    {
        DeepProfiler.Start("Static constructor calls");
        try
        {
            DeepProfiler.Start("StaticConstructorOnStartupUtility.CallAll()");
            List<Type> list = GenTypes.AllTypesWithAttribute<StaticConstructorOnStartup>();
            for (int i = 0; i < list.Count; i++)
            {
                Type item = list[i];
                try
                {
                    lock (LoadingProgressWindow.windowLock)
                    {
                        LoadingProgressWindow.SetCurrentLoadingActivityRaw(item.ToString());
                        LoadingProgressWindow.StageProgress = (i + 1, list.Count);
                    }
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
            if (Prefs.DevMode)
            {
                StaticConstructorOnStartupUtility.ReportProbablyMissingAttributes();
            }

            yield return null;

            var patches = Harmony.GetPatchInfo(_method_StaticConstructorOnStartupUtility_CallAll);
            if (patches != null)
            {
                var potentiallyProblematicPrefixes = new List<MethodInfo>();
                foreach (var patch in patches.Prefixes)
                {
                    potentiallyProblematicPrefixes.Add(patch.PatchMethod);
                }
                var potentiallyProblematicTranspilers = new List<MethodInfo>();
                foreach (var patch in patches.Transpilers)
                {
                    potentiallyProblematicTranspilers.Add(patch.PatchMethod);
                }
                if (potentiallyProblematicPrefixes.Count > 0)
                {
                    LoadingProgressMod.Warning($"These patches may not work as expected because "
                    + $"Loading Progress replaces StaticConstructorOnStartupUtility.CallAll. "
                    + "It still gets called, however, so as long as the patches aren't extremely "
                    + "timing sensitive, they should still work. Here's the list of potentially "
                    + "problematic prefixes:\n- "
                    + string.Join("\n- ", potentiallyProblematicPrefixes.Select(m => m.ToString())));
                }
                if (potentiallyProblematicTranspilers.Count > 0)
                {
                    LoadingProgressMod.Warning($"These transpilers may not work as expected because "
                    + $"Loading Progress replaces StaticConstructorOnStartupUtility.CallAll. "
                    + "It still gets called, however, so as long as the patches aren't extremely "
                    + "timing sensitive, they should still work. Here's the list of potentially "
                    + "problematic transpilers:\n- "
                    + string.Join("\n- ", potentiallyProblematicTranspilers.Select(m => m.ToString())));
                }
            }

            // To make sure any custom patches made to StaticConstructorOnStartupUtility.CallAll get to run,
            // we call it here. Since we've already run all static constructors, this will be a no-op apart
            // from the patches other mods may have made.
            StaticConstructorOnStartupUtility.CallAll();
        }
        finally
        {
            DeepProfiler.End();
        }
    }

    public static void AtlasBaking()
    {
        DeepProfiler.Start("Atlas baking.");
        try
        {
            GlobalTextureAtlasManager.BakeStaticAtlases();
        }
        finally
        {
            DeepProfiler.End();
        }
    }
    public static void GarbageCollection()
    {
        DeepProfiler.Start("Garbage Collection");
        try
        {
            AbstractFilesystem.ClearAllCache();
            GC.Collect(int.MaxValue, GCCollectionMode.Forced);
            _ = Resources.UnloadUnusedAssets();
        }
        finally
        {
            DeepProfiler.End();
        }
    }
}