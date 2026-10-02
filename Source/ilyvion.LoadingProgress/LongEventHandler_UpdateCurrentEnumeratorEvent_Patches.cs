using System.Reflection.Emit;

namespace ilyvion.LoadingProgress;

// Vanilla's UpdateCurrentEnumeratorEvent calls eventActionEnumerator.MoveNext() in a do-while
// loop, only stopping once the *cumulative* real time spent inside the loop (checked after each
// MoveNext() returns, against a budget captured once at loop entry) exceeds 0.1s. Since Unity
// only repaints once this method returns for the frame, any run of cheap MoveNext() calls -
// each just setting a label and yielding - keeps looping with zero repaints in between, because
// the loop has no way to know in advance that the *next* MoveNext() call is the expensive one.
// When that next call turns out to run a slow synchronous step (e.g. a mod's texture reload),
// it blocks for however long that takes, still inside the same Update() call, so the label the
// player sees once the frame finally renders is whichever one was last set *before* this whole
// burst started - not the step that's actually running.
//
// Forcing a repaint after *every* yield fixes that, but costs minutes of load time once you
// account for the thousands of trivial yields spread across static constructors, XML parsing,
// def loading, etc. - that's exactly what the 0.1s batching in vanilla exists to amortize. So we
// keep vanilla's batching and instead transplant a single extra condition into its
// loop-continuation check: RequestImmediateRepaint() lets specific call sites demand that the loop
// stops and let the current frame render, same as if the 0.1s budget had already run out. Gated
// in ShouldStopEarly() to only affect our own queued enumerators (identified by their
// "LoadingProgress." textKey prefix - see the QueueLongEvent calls in
// StaticConstructorOnStartupUtilityReplacement and LongEventHandler_ExecuteToExecuteWhenFinished_Patches),
// so every other enumerator-based long event keeps unmodified vanilla timing.
//
// Only applied when the ForceImmediateRepaints setting is on, since the extra repaints slow
// down asset loading.
[HarmonyPatch(typeof(LongEventHandler), nameof(LongEventHandler.UpdateCurrentEnumeratorEvent))]
internal static class LongEventHandler_UpdateCurrentEnumeratorEvent_Patches
{
    private static bool _repaintRequested;

    internal static bool Prepare() => LoadingProgressMod.Settings.ForceImmediateRepaints;

    internal static void RequestImmediateRepaint() => _repaintRequested = true;

    private static readonly MethodInfo _methodShouldStopEarly = AccessTools.Method(
        typeof(LongEventHandler_UpdateCurrentEnumeratorEvent_Patches),
        nameof(ShouldStopEarly)
    );

    // Consumed (and reset) at most once per loop-continuation check, right where vanilla's own
    // 0.1s budget check happens, so a request only ever cuts short the batch it was made in.
    private static bool ShouldStopEarly()
    {
        if (
            LongEventHandler.currentEvent is not { } currentEvent
            || !currentEvent.eventTextKey.StartsWith("LoadingProgress.", StringComparison.Ordinal)
        )
        {
            return false;
        }

        var stop = _repaintRequested;
        _repaintRequested = false;
        return stop;
    }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
    {
        var originalInstructionList = instructions.ToList();

        // The compiled `while (!(num <= Time.realtimeSinceStartup));` condition boils down to a
        // single `bgt.un.s <loop start>` branch: jump back and keep looping while the deadline
        // hasn't passed, otherwise fall through into the `leave` that exits the loop. That's the
        // one and only such branch in this method.
        var branchIndex = originalInstructionList.FindIndex(i =>
            i.opcode == OpCodes.Bgt_Un_S || i.opcode == OpCodes.Bgt_Un
        );
        if (branchIndex < 0)
        {
            LoadingProgressMod.Error(
                "Could not patch LongEventHandler.UpdateCurrentEnumeratorEvent, IL does not "
                    + "match expectations ([bgt.un(.s) <loop start>]); the loading window will "
                    + "not repaint immediately on stage changes or before slow reload steps."
            );
            return originalInstructionList;
        }

        var branchInstruction = originalInstructionList[branchIndex];
        var loopStartLabel = (Label)branchInstruction.operand;

        // Whatever comes right after the branch is vanilla's existing "budget expired" exit
        // path (a `leave.s` out of the try block) - reuse it as our own fallthrough target
        // instead of duplicating it.
        var fallthroughInstruction = originalInstructionList[branchIndex + 1];
        var fallthroughLabel = generator.DefineLabel();
        fallthroughInstruction.labels.Add(fallthroughLabel);

        // Replace `bgt.un.s loopStart` with:
        //   ble.un.s fallthrough        // deadline passed -> exit exactly like vanilla did
        //   call ShouldStopEarly
        //   brfalse.s loopStart         // no explicit request -> keep looping like vanilla did
        //   (falls through to fallthrough when a request was consumed)
        List<CodeInstruction> replacement =
        [
            new(OpCodes.Ble_Un_S, fallthroughLabel) { labels = branchInstruction.labels },
            new(OpCodes.Call, _methodShouldStopEarly),
            new(OpCodes.Brfalse_S, loopStartLabel),
        ];

        originalInstructionList.RemoveAt(branchIndex);
        originalInstructionList.InsertRange(branchIndex, replacement);

        return originalInstructionList;
    }
}
