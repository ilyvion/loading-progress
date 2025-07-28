using System.Reflection;

namespace ilyvion.LoadingProgress;

public static class Utilities
{
    public static void LongEventHandlerPrependQueue(Action prependAction, string keepPrefix = "LoadingProgress.")
    {
        LoadingProgressMod.DevMessage("Event queue before modification:\n- " + string.Join("\n- ", LongEventHandler.eventQueue.Select(e => $"{e.eventTextKey} ({e.eventText})"))); // + "\n" + Environment.StackTrace);

        // Separate events to keep and to temporarily remove
        var keepEvents = LongEventHandler.eventQueue.Where(e => e.eventTextKey != null && e.eventTextKey.StartsWith(keepPrefix)).ToList();
        var queue = LongEventHandler.eventQueue.Where(e => e.eventTextKey == null || !e.eventTextKey.StartsWith(keepPrefix)).ToList();
        LongEventHandler.eventQueue.Clear();

        // Re-add kept events first (preserving their order)
        foreach (var kept in keepEvents)
        {
            LongEventHandler.eventQueue.Enqueue(kept);
        }

        prependAction();

        // Re-add the rest of the queue
        foreach (var queuedEvent in queue)
        {
            LongEventHandler.eventQueue.Enqueue(queuedEvent);
        }

        LoadingProgressMod.DevMessage("Event queue after modification:\n- " + string.Join("\n- ", LongEventHandler.eventQueue.Select(e => $"{e.eventTextKey} ({e.eventText})")));
    }

    public static void WarnAboutPatches(MethodBase method)
    {
        var patches = Harmony.GetPatchInfo(method);
        if (patches != null)
        {
            var potentiallyProblematicPrefixes = new List<MethodInfo>();
            foreach (var patch in patches.Prefixes)
            {
                if (patch.PatchMethod.DeclaringType.Assembly == Assembly.GetExecutingAssembly())
                {
                    continue; // Skip our own patches
                }

                // Only warn about patches from other assemblies
                potentiallyProblematicPrefixes.Add(patch.PatchMethod);
            }
            var potentiallyProblematicTranspilers = new List<MethodInfo>();
            foreach (var patch in patches.Transpilers)
            {
                if (patch.PatchMethod.DeclaringType.Assembly == Assembly.GetExecutingAssembly())
                {
                    continue; // Skip our own patches
                }

                // Only warn about patches from other assemblies
                potentiallyProblematicTranspilers.Add(patch.PatchMethod);
            }
            var potentiallyProblematicPostfixes = new List<MethodInfo>();
            foreach (var patch in patches.Postfixes)
            {
                if (patch.PatchMethod.DeclaringType.Assembly == Assembly.GetExecutingAssembly())
                {
                    continue; // Skip our own patches
                }

                // Only warn about patches from other assemblies
                potentiallyProblematicPostfixes.Add(patch.PatchMethod);
            }
            var potentiallyProblematicFinalizers = new List<MethodInfo>();
            foreach (var patch in patches.Finalizers)
            {
                if (patch.PatchMethod.DeclaringType.Assembly == Assembly.GetExecutingAssembly())
                {
                    continue; // Skip our own patches
                }

                // Only warn about patches from other assemblies
                potentiallyProblematicFinalizers.Add(patch.PatchMethod);
            }
            if (potentiallyProblematicPrefixes.Count > 0)
            {
                LoadingProgressMod.Warning($"These patches may not work as expected because "
                + $"Loading Progress replaces {method.DeclaringType}:{method}."
                + "It still gets called, however, so as long as the patches aren't extremely "
                + "timing sensitive, they should still work. Here's the list of potentially "
                + "problematic prefixes:\n- "
                + string.Join("\n- ", potentiallyProblematicPrefixes.Select(m => $"{m.DeclaringType}:{m}")));
            }
            if (potentiallyProblematicTranspilers.Count > 0)
            {
                LoadingProgressMod.Warning($"These transpilers may not work as expected because "
                + $"Loading Progress replaces {method.DeclaringType}:{method}."
                + "It still gets called, however, so as long as the patches aren't extremely "
                + "timing sensitive, they should still work. Here's the list of potentially "
                + "problematic transpilers:\n- "
                + string.Join("\n- ", potentiallyProblematicTranspilers.Select(m => $"{m.DeclaringType}:{m}")));
            }
            if (potentiallyProblematicPostfixes.Count > 0)
            {
                LoadingProgressMod.Warning($"These patches may not work as expected because "
                + $"Loading Progress replaces {method.DeclaringType}:{method}."
                + "It still gets called, however, so as long as the patches aren't extremely "
                + "timing sensitive, they should still work. Here's the list of potentially "
                + "problematic postfixes:\n- "
                + string.Join("\n- ", potentiallyProblematicPostfixes.Select(m => $"{m.DeclaringType}:{m}")));
            }
            if (potentiallyProblematicFinalizers.Count > 0)
            {
                LoadingProgressMod.Warning($"These patches may not work as expected because "
                + $"Loading Progress replaces {method.DeclaringType}:{method}."
                + "It still gets called, however, so as long as the patches aren't extremely "
                + "timing sensitive, they should still work. Here's the list of potentially "
                + "problematic finalizers:\n- "
                + string.Join("\n- ", potentiallyProblematicFinalizers.Select(m => m.ToString())));
            }
        }
    }
}