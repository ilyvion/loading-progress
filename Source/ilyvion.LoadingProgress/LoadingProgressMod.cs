using System.Diagnostics;

namespace ilyvion.LoadingProgress;

internal sealed class LoadingProgressMod : Mod
{
#pragma warning disable CS8618 // Set by constructor
    internal static LoadingProgressMod instance;
    internal StartupImpact.StartupImpact StartupImpact;
    internal Harmony harmony;
#pragma warning restore CS8618

    public LoadingProgressMod(ModContentPack content)
        : base(content)
    {
        instance = this;
        StartupImpact = new StartupImpact.StartupImpact();

        // The patch that times every other mod's constructor is applied below, so this one is
        // the only constructor it can never see; it is credited by hand instead.
        const string ownConstructor = "LoadingProgress.StartupImpact.ModConstructor";
        global::ilyvion.LoadingProgress.StartupImpact.StartupImpactProfilerUtil.StartModProfiler(
            content,
            ownConstructor
        );
        try
        {
            harmony = new(content.PackageId);
            harmony.PatchAllUncategorized(Assembly.GetExecutingAssembly());

            if (Settings.TrackStartupLoadingImpact)
            {
                harmony.PatchCategory(Assembly.GetExecutingAssembly(), "StartupImpact");
            }
        }
        finally
        {
            global::ilyvion.LoadingProgress.StartupImpact.StartupImpactProfilerUtil.StopModProfiler(
                content,
                ownConstructor
            );
        }

        Message(
            $"Loading Progress v{content.ModMetaData.ModVersion} initialized! Enjoy the rest of your loading experience!"
        );
    }

    public static Settings Settings => instance.GetSettings<Settings>();

    public override void DoSettingsWindowContents(Rect inRect) =>
        Settings.DoSettingsWindowContents(inRect);

    public override string SettingsCategory() => Content.Name;

    public static void Message(string msg) => Log.Message("[Loading Progress] " + msg);

    public static void DevMessage(string msg)
    {
        if (Prefs.DevMode)
        {
            Log.Message($"[Loading Progress][DEV] " + msg);
        }
    }

    [Conditional("DEBUG")]
    public static void Debug(string message)
    {
        Log.ResetMessageCount();
        DevMessage(message);
    }

    public static void Warning(string msg) => Log.Warning("[Loading Progress] " + msg);

    public static void Error(string msg) => Log.Error("[Loading Progress] " + msg);

    public static void Exception(string msg, Exception? e = null)
    {
        Message(msg);
        if (e != null)
        {
            Log.Error(e.ToString());
        }
    }
}
