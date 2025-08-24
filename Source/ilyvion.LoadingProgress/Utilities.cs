using System.Collections.ObjectModel;
using System.Text;

namespace ilyvion.LoadingProgress;

[Flags]
internal enum PatchKinds
{
    None = 0,
    Prefix = 1 << 0,
    Transpiler = 1 << 1,
    Postfix = 1 << 2,
    Finalizer = 1 << 3,
    All = Prefix | Transpiler | Postfix | Finalizer,
}

internal static class Utilities
{
    public static void LongEventHandlerPrependQueue(
        Action prependAction,
        string keepPrefix = "LoadingProgress.")
    {
        //LoadingProgressMod.Debug("Event queue before modification:\n- " + string.Join("\n- ", LongEventHandler.eventQueue.Select(e => $"{e.eventTextKey} ({e.eventText})"))); // + "\n" + Environment.StackTrace);

        // Separate events to keep and to temporarily remove
        var keepEvents = LongEventHandler.eventQueue
            .Where(e => e.eventTextKey != null
                && e.eventTextKey.StartsWith(keepPrefix, StringComparison.Ordinal))
            .ToList();
        var queue = LongEventHandler.eventQueue
            .Where(e => e.eventTextKey == null
                || !e.eventTextKey.StartsWith(keepPrefix, StringComparison.Ordinal))
            .ToList();
        LongEventHandler.eventQueue.Clear();

        // Re-add kept events first (preserving their order)
        foreach (var kept in keepEvents)
        {
            LongEventHandler.eventQueue.Enqueue(kept);
        }

        prependAction();

        // Re-add the rest of the queue
        foreach (var queuedEvent in queue)
        {
            LongEventHandler.eventQueue.Enqueue(queuedEvent);
        }

        //LoadingProgressMod.Debug("Event queue after modification:\n- " + string.Join("\n- ", LongEventHandler.eventQueue.Select(e => $"{e.eventTextKey} ({e.eventText})")));
    }

    public static void WarnAboutPatches(
        MethodBase method,
        bool stillCallsOriginal,
        Assembly[]? ignoredAssemblies = null,
        MethodBase[]? ignoredMethods = null,
        PatchKinds warnKinds = PatchKinds.All)
    {
        HashSet<Assembly> ignoredAssemblySet =
        [
            Assembly.GetExecutingAssembly(),
            .. ignoredAssemblies ?? []
        ];
        HashSet<MethodBase> ignoredMethodsSet = [.. ignoredMethods ?? []];

        var patches = Harmony.GetPatchInfo(method);
        if (patches != null)
        {
            var potentiallyProblematicPrefixes = CollectPotentiallyProblematicPatches(
                warnKinds,
                PatchKinds.Prefix,
                patches.Prefixes,
                ignoredAssemblySet,
                ignoredMethodsSet);

            var potentiallyProblematicTranspilers = CollectPotentiallyProblematicPatches(
                warnKinds,
                PatchKinds.Transpiler,
                patches.Transpilers,
                ignoredAssemblySet,
                ignoredMethodsSet);

            var potentiallyProblematicPostfixes = CollectPotentiallyProblematicPatches(
                warnKinds,
                PatchKinds.Postfix,
                patches.Postfixes,
                ignoredAssemblySet,
                ignoredMethodsSet);

            var potentiallyProblematicFinalizers = CollectPotentiallyProblematicPatches(
                warnKinds,
                PatchKinds.Finalizer,
                patches.Finalizers,
                ignoredAssemblySet,
                ignoredMethodsSet);

            var totalCount = (potentiallyProblematicPrefixes?.Count ?? 0)
                              + (potentiallyProblematicTranspilers?.Count ?? 0)
                              + (potentiallyProblematicPostfixes?.Count ?? 0)
                              + (potentiallyProblematicFinalizers?.Count ?? 0);
            if (totalCount > 0)
            {
                var sb = new StringBuilder();
                _ = sb.Append("These patches may not work as expected because ")
                      .Append($"Loading Progress replaces {method.DeclaringType}:{method}.\n");

                if (stillCallsOriginal)
                {
                    _ = sb.Append("Note: The original method is still called; unless patches are ");
                    _ = sb.Append("extremely timing-sensitive, they should still work.\n");
                }

                AppendPatchWarning(sb, warnKinds, PatchKinds.Prefix, potentiallyProblematicPrefixes, "prefixes");
                AppendPatchWarning(sb, warnKinds, PatchKinds.Transpiler, potentiallyProblematicTranspilers, "transpilers");
                AppendPatchWarning(sb, warnKinds, PatchKinds.Postfix, potentiallyProblematicPostfixes, "postfixes");
                AppendPatchWarning(sb, warnKinds, PatchKinds.Finalizer, potentiallyProblematicFinalizers, "finalizers");


                LoadingProgressMod.Warning(sb.ToString().TrimEnd());
            }
        }
    }

    private static void AppendPatchWarning(StringBuilder sb, PatchKinds warnFlags, PatchKinds warnCheckedFlag, List<MethodInfo>? methods, string label)
    {
        if ((warnFlags & warnCheckedFlag) != 0 && methods != null && methods.Count > 0)
        {
            _ = sb.Append($"Potentially problematic {label} ");
            _ = sb.Append($"({methods.Count}):\n  - ").Append(string.Join("\n  - ", methods.Select(m => $"{m.DeclaringType}:{m}"))).Append('\n');
        }
    }

    private static List<MethodInfo>? CollectPotentiallyProblematicPatches(
        PatchKinds warnFlags,
        PatchKinds warnCheckedFlag,
        IEnumerable<Patch> patchesEnumerable,
        HashSet<Assembly> ignoredAssemblySet,
        HashSet<MethodBase> ignoredMethodsSet)
    {
        List<MethodInfo>? potentiallyProblematicPatches = null;
        if ((warnFlags & warnCheckedFlag) != 0)
        {
            potentiallyProblematicPatches = [];
            foreach (var patch in patchesEnumerable)
            {
                if (ignoredAssemblySet.Contains(patch.PatchMethod.DeclaringType.Assembly)
                    || ignoredMethodsSet.Contains(patch.PatchMethod))
                {
                    continue; // Skip ignored assemblies and methods
                }
                potentiallyProblematicPatches.Add(patch.PatchMethod);
            }
        }

        return potentiallyProblematicPatches;
    }

    public static Color Darken(this Color color, float amount)
    {
        Color.RGBToHSV(color, out var h, out var s, out var v);
        v = Mathf.Max(0, v - amount); // reduce lightness
        return Color.HSVToRGB(h, s, v);
    }

    public static Color Brighten(this Color color, float amount)
    {
        Color.RGBToHSV(color, out var h, out var s, out var v);
        v = Mathf.Min(1, v + amount); // increase lightness
        return Color.HSVToRGB(h, s, v);
    }

    public static string ClampTextWithEllipsisMarkupAware(Rect rect, string text)
    {
        if (text.Length <= 4)
        {
            return text;
        }

        if (Text.CalcSize(text).x <= rect.width - 13f)
        {
            return text;
        }

        var output = new StringBuilder();
        var stack = new Stack<string>();
        var visibleChars = 0;

        // forward pass to capture tag info
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                var closing = text.IndexOf('>', i);
                if (closing == -1)
                {
                    break;
                }

                var tag = text.Substring(i, closing - i + 1);
                _ = output.Append(tag);

                if (!tag.StartsWith("</", StringComparison.Ordinal))
                {
                    var spaceIdx = tag.IndexOf(' ', StringComparison.Ordinal);
                    var tagNameEnd = spaceIdx != -1 ? spaceIdx : tag.Length - 1;
                    stack.Push(tag[1..tagNameEnd]);
                }
                else if (stack.Count > 0)
                {
                    _ = stack.Pop();
                }
                i = closing;
            }
            else
            {
                _ = output.Append(text[i]);
                visibleChars++;
                if (Text.CalcSize(output.ToString() + "...").x > rect.width - 13f)
                {
                    output.Length -= 1; // remove last character
                    break;
                }
            }
        }

        _ = output.Append("...");
        // close tags
        while (stack.Count > 0)
        {
            _ = output.Append("</" + stack.Pop() + ">");
        }

        return output.ToString();
    }

    private static readonly Dictionary<Assembly, ModContentPack?> _modAssemblyCache = [];
    public static ModContentPack? FindModByAssembly(Assembly assembly)
    {
        if (_modAssemblyCache.TryGetValue(assembly, out var modContentPack))
        {
            return modContentPack;
        }

        modContentPack = (from modpack in LoadedModManager.RunningMods
                          where modpack.assemblies.loadedAssemblies.Contains(assembly)
                          select modpack).FirstOrDefault();

        _modAssemblyCache[assembly] = modContentPack;
        return modContentPack;
    }

    public static HashSet<MethodInfo> FindInTypeAndInnerTypeMethods(
        Type type,
        Func<MethodInfo, bool>? predicate = null)
    {
        predicate ??= _ => true;

        // Find all possible candidates, both from the wrapping type and all nested types.
        var candidates = AccessTools.GetDeclaredMethods(type)
            .Where(predicate)
            .ToHashSet();
        candidates.AddRange(type
            .GetNestedTypes(AccessTools.all)
            .SelectMany(AccessTools.GetDeclaredMethods)
            .Where(predicate));

        return candidates;
    }

    public static IEnumerable<MethodInfo> FindMethodsDoing(Type containingType, CodeMatch[] toMatch)
    {
        // Find all possible candidates, both from the wrapping type and all nested types.
        var candidates = FindInTypeAndInnerTypeMethods(containingType, m => !m.IsGenericMethod);

        //check all candidates for the target instructions, return those that match.
        foreach (var method in candidates)
        {
            var instructions = PatchProcessor.GetCurrentInstructions(method);
            var matched = instructions.Matches(toMatch);
            if (matched)
            {
                yield return method;
            }
        }
        yield break;
    }

    public static ReadOnlyDictionary<TKey, TValue> AsReadOnly<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary) where TKey : notnull => new(dictionary);
}

internal static class StableListHasher
{
    public static int ComputeListHash(IEnumerable<string> items)
    {
        // Join with a null separator to avoid collisions like ["ab", "c"] vs ["a", "bc"]
        var combined = string.Join("\0", items ?? []);
        var data = Encoding.UTF8.GetBytes(combined);
        return MurmurHash3(data, 42 + 69 + 420);
    }

    // MurmurHash3 x86 32-bit
    private static int MurmurHash3(byte[] data, uint seed)
    {
        const uint c1 = 0xcc9e2d51;
        const uint c2 = 0x1b873593;
        const int r1 = 15;
        const int r2 = 13;
        const uint m = 5;
        const uint n = 0xe6546b64;

        var hash = seed;
        var length = data.Length;
        var remainder = length & 3;
        var blocks = length / 4;

        // Body
        for (var i = 0; i < blocks; i++)
        {
            var index = i * 4;
            var k = BitConverter.ToUInt32(data, index);

            k *= c1;
            k = RotateLeft(k, r1);
            k *= c2;

            hash ^= k;
            hash = RotateLeft(hash, r2);
            hash = (hash * m) + n;
        }

        // Tail
        uint k1 = 0;
        if (remainder > 0)
        {
            switch (remainder)
            {
                case 3:
                    k1 ^= (uint)data[length - 3] << 16;
                    goto case 2;
                case 2:
                    k1 ^= (uint)data[length - 2] << 8;
                    goto case 1;
                case 1:
                    k1 ^= data[length - 1];
                    k1 *= c1;
                    k1 = RotateLeft(k1, r1);
                    k1 *= c2;
                    hash ^= k1;
                    break;
                default:
                    // This should never happen; we `& 3` above; but just in case.
                    throw new NotSupportedException("Invalid remainder length for MurmurHash3.");
            }
        }

        // Finalization
        hash ^= (uint)length;
        hash = FMix(hash);

        return unchecked((int)hash);
    }

    private static uint RotateLeft(uint x, int r) =>
        (x << r) | (x >> (32 - r));

    private static uint FMix(uint h)
    {
        h ^= h >> 16;
        h *= 0x85ebca6b;
        h ^= h >> 13;
        h *= 0xc2b2ae35;
        h ^= h >> 16;
        return h;
    }
}
