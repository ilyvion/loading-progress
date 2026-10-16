using System.Diagnostics;

namespace ilyvion.LoadingProgress;

internal static class MemoryUsage
{
    // GC.GetTotalMemory is a fast, uncollected estimate; the more precise
    // GC.GetGCMemoryInfo() isn't available on this project's net481 target.
    public static long ManagedHeapBytes => GC.GetTotalMemory(false);

    private static readonly Process CurrentProcess = Process.GetCurrentProcess();

    // CoreCLR's Process caches its values until Refresh() is called.
    public static long ProcessBytes
    {
        get
        {
            CurrentProcess.Refresh();
            return CurrentProcess.WorkingSet64;
        }
    }
}
