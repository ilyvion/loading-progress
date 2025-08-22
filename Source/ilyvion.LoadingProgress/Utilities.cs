using System.Collections.ObjectModel;
using System.Text;

namespace ilyvion.LoadingProgress;

[System.Flags]
public enum PatchKinds
{
    None = 0,
    Prefix = 1 << 0,
    Transpiler = 1 << 1,
    Postfix = 1 << 2,
    Finalizer = 1 << 3,
    All = Prefix | Transpiler | Postfix | Finalizer,
}

public static class Utilities
{
    public static void LongEventHandlerPrependQueue(Action prependAction, string keepPrefix = "LoadingProgress.")
    {
        //LoadingProgressMod.Debug("Event queue before modification:\n- " + string.Join("\n- ", LongEventHandler.eventQueue.Select(e => $"{e.eventTextKey} ({e.eventText})"))); // + "\n" + Environment.StackTrace);

        // Separate events to keep and to temporarily remove
        var keepEvents = LongEventHandler.eventQueue.Where(e => e.eventTextKey != null && e.eventTextKey.StartsWith(keepPrefix)).ToList();
        var queue = LongEventHandler.eventQueue.Where(e => e.eventTextKey == null || !e.eventTextKey.StartsWith(keepPrefix)).ToList();
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
        HashSet<Assembly> ignoredAssemblySet = [Assembly.GetExecutingAssembly(), .. ignoredAssemblies ?? []];
        HashSet<MethodBase> ignoredMethodsSet = [.. ignoredMethods ?? []];

        var patches = Harmony.GetPatchInfo(method);
        if (patches != null)
        {
            List<MethodInfo>? potentiallyProblematicPrefixes = null;
            if ((warnKinds & PatchKinds.Prefix) != 0)
            {
                potentiallyProblematicPrefixes ??= [];
                foreach (var patch in patches.Prefixes)
                {
                    if (ignoredAssemblySet.Contains(patch.PatchMethod.DeclaringType.Assembly) || ignoredMethodsSet.Contains(patch.PatchMethod))
                    {
                        continue; // Skip ignored assemblies and methods
                    }
                    potentiallyProblematicPrefixes.Add(patch.PatchMethod);
                }
            }

            List<MethodInfo>? potentiallyProblematicTranspilers = null;
            if ((warnKinds & PatchKinds.Transpiler) != 0)
            {
                potentiallyProblematicTranspilers ??= [];
                foreach (var patch in patches.Transpilers)
                {
                    if (ignoredAssemblySet.Contains(patch.PatchMethod.DeclaringType.Assembly) || ignoredMethodsSet.Contains(patch.PatchMethod))
                    {
                        continue; // Skip ignored assemblies and methods
                    }
                    potentiallyProblematicTranspilers.Add(patch.PatchMethod);
                }
            }

            List<MethodInfo>? potentiallyProblematicPostfixes = null;
            if ((warnKinds & PatchKinds.Postfix) != 0)
            {
                potentiallyProblematicPostfixes ??= [];
                foreach (var patch in patches.Postfixes)
                {
                    if (ignoredAssemblySet.Contains(patch.PatchMethod.DeclaringType.Assembly) || ignoredMethodsSet.Contains(patch.PatchMethod))
                    {
                        continue; // Skip ignored assemblies and methods
                    }
                    potentiallyProblematicPostfixes.Add(patch.PatchMethod);
                }
            }

            List<MethodInfo>? potentiallyProblematicFinalizers = null;
            if ((warnKinds & PatchKinds.Finalizer) != 0)
            {
                potentiallyProblematicFinalizers ??= [];
                foreach (var patch in patches.Finalizers)
                {
                    if (ignoredAssemblySet.Contains(patch.PatchMethod.DeclaringType.Assembly) || ignoredMethodsSet.Contains(patch.PatchMethod))
                    {
                        continue; // Skip ignored assemblies and methods
                    }
                    potentiallyProblematicFinalizers.Add(patch.PatchMethod);
                }
            }
            int totalCount = (potentiallyProblematicPrefixes?.Count ?? 0)
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
                    _ = sb.Append("Note: The original method is still called; unless patches are extremely timing-sensitive, they should still work.\n");
                }

                if ((warnKinds & PatchKinds.Prefix) != 0 && potentiallyProblematicPrefixes != null && potentiallyProblematicPrefixes.Count > 0)
                {
                    _ = sb.Append($"Potentially problematic prefixes ({potentiallyProblematicPrefixes.Count}):\n  - ")
                         .Append(string.Join("\n  - ", potentiallyProblematicPrefixes.Select(m => $"{m.DeclaringType}:{m}")))
                         .Append('\n');
                }
                if ((warnKinds & PatchKinds.Transpiler) != 0 && potentiallyProblematicTranspilers != null && potentiallyProblematicTranspilers.Count > 0)
                {
                    _ = sb.Append($"Potentially problematic transpilers ({potentiallyProblematicTranspilers.Count}):\n  - ")
                         .Append(string.Join("\n  - ", potentiallyProblematicTranspilers.Select(m => $"{m.DeclaringType}:{m}")))
                         .Append('\n');
                }
                if ((warnKinds & PatchKinds.Postfix) != 0 && potentiallyProblematicPostfixes != null && potentiallyProblematicPostfixes.Count > 0)
                {
                    _ = sb.Append($"Potentially problematic postfixes ({potentiallyProblematicPostfixes.Count}):\n  - ")
                         .Append(string.Join("\n  - ", potentiallyProblematicPostfixes.Select(m => $"{m.DeclaringType}:{m}")))
                         .Append('\n');
                }
                if ((warnKinds & PatchKinds.Finalizer) != 0 && potentiallyProblematicFinalizers != null && potentiallyProblematicFinalizers.Count > 0)
                {
                    _ = sb.Append($"Potentially problematic finalizers ({potentiallyProblematicFinalizers.Count}):\n  - ")
                         .Append(string.Join("\n  - ", potentiallyProblematicFinalizers.Select(m => $"{m.DeclaringType}:{m}")))
                         .Append('\n');
                }

                LoadingProgressMod.Warning(sb.ToString().TrimEnd());
            }
        }
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
        int visibleChars = 0;

        // forward pass to capture tag info
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '<')
            {
                int closing = text.IndexOf('>', i);
                if (closing == -1)
                {
                    break;
                }

                string tag = text.Substring(i, closing - i + 1);
                _ = output.Append(tag);

                if (!tag.StartsWith("</"))
                {
                    int spaceIdx = tag.IndexOf(' ');
                    int tagNameEnd = spaceIdx != -1 ? spaceIdx : tag.Length - 1;
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

    public static HashSet<MethodInfo> FindInTypeAndInnerTypeMethods(Type type, Func<MethodInfo, bool>? predicate = null)
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

public static class StableListHasher
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

        uint hash = seed;
        int length = data.Length;
        int remainder = length & 3;
        int blocks = length / 4;

        // Body
        for (int i = 0; i < blocks; i++)
        {
            int index = i * 4;
            uint k = BitConverter.ToUInt32(data, index);

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
                case 3: k1 ^= (uint)data[length - 3] << 16; goto case 2;
                case 2: k1 ^= (uint)data[length - 2] << 8; goto case 1;
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
