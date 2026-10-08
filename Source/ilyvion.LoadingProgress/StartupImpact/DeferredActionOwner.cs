using System.Collections.Concurrent;

namespace ilyvion.LoadingProgress.StartupImpact;

/// <summary>
/// Finds the mod a deferred initialization action belongs to by what it works on rather than
/// by where its code lives.
/// </summary>
/// <remarks>
/// The engine queues one <c>ExecuteWhenFinished</c> action per def for its graphic and
/// reference setup (<c>ThingDef.PostLoad</c>, <c>TerrainDef.PostLoad</c>,
/// <c>DesignationCategoryDef.ResolveReferences</c> and the like), every one from its own
/// assembly, so going by the declaring assembly files all of that under the base game. The
/// action's target says whose def it is: the def itself when the lambda captured only
/// <c>this</c>, or a compiler-generated closure holding it, as when a def passes itself into
/// a property's <c>PostLoadSpecial</c>. The same holds for a mod's own per-def actions: the
/// def a framework sets up belongs to the mod that declared it. The rule reads only the
/// action's target, so a mod's single action whose lambda captures a local holding some def
/// is credited to that def's mod as well, since nothing tells it apart from a per-def one.
/// </remarks>
internal static class DeferredActionOwner
{
    private const int MaxClosureDepth = 2;

    private static readonly ConcurrentDictionary<Type, FieldInfo[]> _closureFieldsByType = new();

    /// <summary>
    /// The content pack that owns the def the action works on, or null when the action is not
    /// tied to a def it can be traced to.
    /// </summary>
    internal static ModContentPack? OwningContentPack(Delegate action) =>
        OwnerOfTarget(action.Target, 0);

    internal static ModContentPack? OwnerOfTarget(object? target, int depth)
    {
        if (target == null || depth > MaxClosureDepth)
        {
            return null;
        }

        if (target is Def def)
        {
            return def.modContentPack;
        }

        if (target is ModContentPack pack)
        {
            return pack;
        }

        var type = target.GetType();
        if (!CompilerGenerated.Is(type))
        {
            return null;
        }

        foreach (var field in ClosureFields(type))
        {
            object? value;
            try
            {
                value = field.GetValue(target);
            }
            catch (Exception)
            {
                continue;
            }

            var owner = OwnerOfTarget(value, depth + 1);
            if (owner != null)
            {
                return owner;
            }
        }

        return null;
    }

    // The fields of a closure that can lead to a def, kept per type as CompilerGenerated keeps
    // its answer.
    private static FieldInfo[] ClosureFields(Type type) =>
        _closureFieldsByType.GetOrAdd(
            type,
            static closure =>
                [
                    .. closure
                        .GetFields(
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                        )
                        .Where(field =>
                            typeof(Def).IsAssignableFrom(field.FieldType)
                            || typeof(ModContentPack).IsAssignableFrom(field.FieldType)
                            || CompilerGenerated.Is(field.FieldType)
                        ),
                ]
        );
}
