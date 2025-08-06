using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Xml;

namespace ilyvion.LoadingProgress;

internal static class LoadingDataTracker
{
    public static string? Previous = null;
    public static string? Current = null;
    public static bool ModChanged => Previous != Current;

    internal static Def? LastDef = null;
    internal static int WantedRefTryResolveCount = 0;
    internal static int WantedRefApplyCount = 0;
}

[HarmonyPatch(typeof(LoadedModManager))]
internal static class LoadedModManager_LoadingDataTracker_Patches
{
    [HarmonyPatch(nameof(LoadedModManager.CombineIntoUnifiedXML))]
    [HarmonyPrefix]
    private static void CombineIntoUnifiedXMLPrefix(List<LoadableXmlAsset> xmls)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.CombineIntoUnifiedXml)
        {
            return;
        }

        var total = xmls.SelectMany(x => x.xmlDoc.DocumentElement.ChildNodes.Cast<XmlNode>()).Count();
        LoadingProgressWindow.StageProgress = (0, total);
    }

    [HarmonyPatch(nameof(LoadedModManager.CombineIntoUnifiedXML))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> CombineIntoUnifiedXMLTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.SearchForward(i => i.opcode == OpCodes.Castclass && i.operand is Type type && type == typeof(XmlNode));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("LoadedModManager.CombineIntoUnifiedXML: Could not find a cast to XmlNode.");
            return original;
        }

        _ = codeMatcher.Advance(2).Insert([
                new(OpCodes.Call, AccessTools.Method(typeof(LoadedModManager_LoadingDataTracker_Patches), nameof(CombineIntoUnifiedXMLProgress))),
            ]);

        return codeMatcher.Instructions();
    }

    private static void CombineIntoUnifiedXMLProgress()
    {
        if (LoadingProgressWindow.CurrentStage == LoadingStage.CombineIntoUnifiedXml && LoadingProgressWindow.StageProgress is (float index, float total))
        {
            LoadingProgressWindow.StageProgress = ((int)index + 1, total);
        }
    }
}

[HarmonyPatch(typeof(ModContentPack))]
internal static class ModContentPack_LoadingDataTracker_Patches
{
    [HarmonyPatch(nameof(ModContentPack.ReloadContentInt))]
    [HarmonyPrefix]
    private static void ReloadContentIntPrefix(ModContentPack __instance, bool hotReload)
    {
        if (hotReload)
        {
            return;
        }

        LoadingDataTracker.Previous = LoadingDataTracker.Current;
        LoadingDataTracker.Current = __instance.Name;
    }

    [HarmonyPatch(nameof(ModContentPack.LoadPatches))]
    [HarmonyPrefix]
    private static void LoadPatchesPrefix(ModContentPack __instance)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.ErrorCheckPatches)
        {
            return;
        }

        LoadingDataTracker.Previous = LoadingDataTracker.Current;
        LoadingDataTracker.Current = __instance.Name;
    }
}

[HarmonyPatch(typeof(XmlInheritance))]
internal static class XmlInheritance_LoadingDataTracker_Patches
{
    [HarmonyPatch(nameof(XmlInheritance.TryRegister))]
    [HarmonyPrefix]
    private static void TryRegisterPrefix(ModContentPack? mod)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.ParseAndProcessXml)
        {
            return;
        }

        if (mod != null)
        {
            LoadingDataTracker.Previous = LoadingDataTracker.Current;
            LoadingDataTracker.Current = mod.Name;
        }
    }

    [HarmonyPatch(nameof(XmlInheritance.ResolveXmlNodes))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ResolveXmlNodesTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.SearchForward(i => i.Calls(AccessTools.Indexer(typeof(List<XmlInheritance.XmlInheritanceNode>), [typeof(int)]).GetGetMethod()));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("XmlInheritance.ResolveXmlNodes: Could not find a call to List<>.get_Item.");
            return original;
        }

        _ = codeMatcher.Insert([
                new(OpCodes.Call, AccessTools.Method(typeof(XmlInheritance_LoadingDataTracker_Patches), nameof(XmlInheritanceProgress))),
                new(OpCodes.Ldloc_0),
                new(OpCodes.Ldloc_1),
            ]);

        return codeMatcher.Instructions();
    }

    private static void XmlInheritanceProgress(List<XmlInheritance.XmlInheritanceNode> unresolvedNodes, int index)
    {
        if (LoadingProgressWindow.CurrentStage == LoadingStage.XmlInheritanceResolve && unresolvedNodes.Count > 0)
        {
            LoadingProgressWindow.StageProgress = (index + 1, unresolvedNodes.Count);
        }
    }
}

[HarmonyPatch(typeof(DirectXmlToObjectNew))]
internal static class DirectXmlToObjectNew_LoadingDataTracker_Patches
{
    [HarmonyPatch(nameof(DirectXmlToObjectNew.DefFromNodeNew))]
    [HarmonyPostfix]
    private static void DefFromNodeNewPostfix(Def __result)
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.LoadingDefs)
        {
            return;
        }

        LoadingDataTracker.LastDef = __result;
    }
}

[HarmonyPatch]
internal static partial class DirectXmlCrossRefLoader_ResolveAllWantedCrossReferences_Parallel_LoadingDataTracker_Patches
{
    private static bool Prepare()
    {
        // We can reuse the same method as HarmonyTargetMethods will use afterward.
        var methods = WantedRef_TryResolveFinder.FindMethod();

        //check that we have one and only one match. If we get more, the match is giving false positives.
        if (methods.Count() == 1)
        {
            return true;
        }
        else
        {
            LoadingProgressMod.Error("Could not patch DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences, could not locate call to DirectXmlCrossRefLoader+WantedRef.TryResolve.");
            return false;
        }

    }

    private static IEnumerable<MethodBase> TargetMethods()
    {
        return WantedRef_TryResolveFinder.FindMethod();
    }

    private static void Postfix()
    {
        _ = Interlocked.Increment(ref LoadingDataTracker.WantedRefTryResolveCount);
    }

    private static class WantedRef_TryResolveFinder
    {
        private static readonly MethodInfo _method_WantedRef_TryResolve
            = AccessTools.Method(
                typeof(DirectXmlCrossRefLoader.WantedRef),
                nameof(DirectXmlCrossRefLoader.WantedRef.TryResolve));

        private static readonly CodeMatch[] toMatch =
        [
            new(OpCodes.Callvirt, _method_WantedRef_TryResolve),
        ];

        public static IEnumerable<MethodInfo> FindMethod()
        {
            // Find all possible candidates, both from the wrapping type and all nested types.
            var candidates = AccessTools.GetDeclaredMethods(typeof(DirectXmlCrossRefLoader))
                .Where(m => !m.IsGenericMethod && !m.IsAbstract && !m.DeclaringType.IsGenericType)
                .ToHashSet();
            candidates.AddRange(typeof(DirectXmlCrossRefLoader)
                .GetNestedTypes(AccessTools.all)
                .SelectMany(AccessTools.GetDeclaredMethods)
                .Where(m => !m.IsGenericMethod && !m.IsAbstract && !m.DeclaringType.IsGenericType));

            //check all candidates for the target instructions, return those that match.
            foreach (var method in candidates)
            {
                List<CodeInstruction> instructions;
                try
                {
                    instructions = PatchProcessor.GetCurrentInstructions(method);
                }
                catch (Exception ex)
                {
                    LoadingProgressMod.Error($"Error while processing method {method}({method.FullDescription()}): {ex}");
                    continue;
                }
                var matched = instructions.Matches(toMatch);
                if (matched)
                {
                    yield return method;
                }
            }
            yield break;
        }
    }
}

[HarmonyPatch(typeof(DirectXmlCrossRefLoader))]
internal static partial class DirectXmlCrossRefLoader_ResolveAllWantedCrossReferences_LoadingDataTracker_Patches
{

    [HarmonyPatch(nameof(DirectXmlCrossRefLoader.ResolveAllWantedCrossReferences))]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> ResolveXmlNodesTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.SearchForward(i => i.Calls(AccessTools.Method(typeof(DirectXmlCrossRefLoader.WantedRef), nameof(DirectXmlCrossRefLoader.WantedRef.Apply))));
        if (codeMatcher.IsInvalid)
        {
            LoadingProgressMod.Error("XmlInheritance.ResolveXmlNodes: Could not find a call to List<>.get_Item.");
            return original;
        }

        _ = codeMatcher.Advance(1).Insert([
                new(OpCodes.Call, AccessTools.Method(typeof(DirectXmlCrossRefLoader_ResolveAllWantedCrossReferences_LoadingDataTracker_Patches), nameof(Stage2Progress))),
            ]);

        return codeMatcher.Instructions();
    }

    private static void Stage2Progress()
    {
        if (LoadingProgressWindow.CurrentStage != LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefsStage2)
        {
            LoadingProgressWindow.CurrentStage = LoadingStage.ResolveCrossReferencesBetweenNonImpliedDefsStage2;
        }
        LoadingProgressWindow.StageProgress = (LoadingDataTracker.WantedRefApplyCount++, DirectXmlCrossRefLoader.wantedRefs.Count);
    }
}
