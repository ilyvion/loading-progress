namespace ilyvion.LoadingProgress;

public static class Utilities
{
    public static void LongEventHandlerPrependQueue(Action prependAction, string keepPrefix = "LoadingProgress.")
    {
        LoadingProgressMod.Message("Event queue before modification:\n- " + string.Join("\n- ", LongEventHandler.eventQueue.Select(e => $"{e.eventTextKey} ({e.eventText})")));

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

        LoadingProgressMod.Message("Event queue after modification:\n- " + string.Join("\n- ", LongEventHandler.eventQueue.Select(e => $"{e.eventTextKey} ({e.eventText})")));
    }
}