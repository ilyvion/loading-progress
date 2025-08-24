namespace ilyvion.LoadingProgress.StartupImpact;

internal sealed class ModInfoList
{
    private readonly List<ModInfo> _modInfos = [];
    private readonly Dictionary<ModContentPack, ModInfo> _contentPackModInfoMap = [];
    private ModContentPack? _coreMod;

    public ModInfo? GetModInfoFor(ModContentPack? mod)
    {
        if (mod != null && _coreMod == null && mod.IsCoreMod)
        {
            _coreMod = mod;
        }

        mod ??= _coreMod;
        if (mod == null)
        {
            return null;
        }

        if (_contentPackModInfoMap.TryGetValue(mod, out var res))
        {
            return res;
        }

        res = new ModInfo(mod);
        _modInfos.Add(res);
        _contentPackModInfoMap.Add(mod, res);
        return res;
    }

    public List<ModInfo> ModsInImpactOrder => [.. _modInfos.OrderBy(x => -x.Profiler.TotalImpact)];
}
