﻿using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace ilyvion.LoadingProgress;


internal class LoadingProgressMod : Mod
{
#pragma warning disable CS8618 // Set by constructor
    internal static LoadingProgressMod instance;
#pragma warning restore CS8618

    public LoadingProgressMod(ModContentPack content) : base(content)
    {
        instance = this;

        new Harmony(content.PackageId).PatchAll(Assembly.GetExecutingAssembly());

        Message("Loading Progress initialized! Enjoy the rest of your loading experience!");
    }

    public static Settings Settings => instance.GetSettings<Settings>();

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoSettingsWindowContents(inRect);
    }

    public override string SettingsCategory()
    {
        return Content.Name;
    }

    public static void Message(string msg)
    {
        Log.Message("[Loading Progress] " + msg);
    }

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
        DevMessage(message);
    }

    public static void Warning(string msg)
    {
        Log.Warning("[Loading Progress] " + msg);
    }

    public static void Error(string msg)
    {
        Log.Error("[Loading Progress] " + msg);
    }

    public static void Exception(string msg, Exception? e = null)
    {
        Message(msg);
        if (e != null)
        {
            Log.Error(e.ToString());
        }
    }
}
