namespace ilyvion.LoadingProgress.StartupImpact;

public class ModInfo
{
    public ModInfo(ModContentPack m)
    {
        Mod = m;
        Profiler = new Profiler(Mod.Name);
    }

    public ModContentPack Mod { get; }

    public Profiler Profiler { get; }

    public void Start(string cat)
    {
        Profiler.Start(cat);
    }

    public float Stop(string cat)
    {
        return Profiler.Stop(cat);
    }
}
