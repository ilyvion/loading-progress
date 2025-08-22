namespace ilyvion.LoadingProgress.StartupImpact.Patches;

internal static partial class ModContentPack_LoadPatches_Patches
{
#pragma warning disable IDE0028
    internal static ConditionalWeakTable<PatchOperation, ModContentPack> modContentPackTable = new();
#pragma warning restore IDE0028

    static partial void AfterPostfix(ModContentPack? modContentPack)
    {
        if (modContentPack == null)
        {
            return;
        }

        foreach (var patch in modContentPack.patches)
        {
            modContentPackTable.Add(patch, modContentPack);
        }
    }
}
