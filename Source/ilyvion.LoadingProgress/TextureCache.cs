namespace ilyvion.LoadingProgress;

internal static class TextureCache
{
    /// <summary>
    /// Whether this launch has written any textures to the game's texture cache, which only
    /// happens for textures the cache hasn't seen before (or whose source files have changed).
    /// </summary>
    /// <remarks>
    /// The cache starts its writer threads on the first write and never stops them, so this stays
    /// true for the rest of the launch, including for writes made before this mod was loaded.
    /// </remarks>
    internal static bool IsCaching =>
#if v1_7
        Volatile.Read(ref Waylith.Rendering.TextureCacheStore._writers) > 0;
#else
        false;
#endif
}
