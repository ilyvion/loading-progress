namespace ilyvion.LoadingProgress;

[HarmonyPatch(typeof(ModContentPack), nameof(ModContentPack.ReloadContentInt))]
internal static class ModContentPack_ReloadContentInt_Patches
{
    public static string? CurrentMod = null;
    private static void Prefix(ModContentPack __instance, bool hotReload)
    {
        if (hotReload)
        {
            return;
        }

        CurrentMod = __instance.Name;
    }
}