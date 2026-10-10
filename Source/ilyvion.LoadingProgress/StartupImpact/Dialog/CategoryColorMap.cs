using System.Diagnostics.CodeAnalysis;

namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

/// <summary>
/// The colours of profiler categories: a category's own curated colour, else its phase's, else
/// one derived from its phase. Time another mod ran in a category's place has the colour of
/// the category it replaced. Every category has a colour.
/// </summary>
internal sealed class CategoryColorMap(IReadOnlyDictionary<string, Color> curated)
    : IReadOnlyDictionary<string, Color>
{
    public Color this[string key] => ColorFor(key);

    public IEnumerable<string> Keys => curated.Keys;
    public IEnumerable<Color> Values => curated.Values;
    public int Count => curated.Count;

    public bool ContainsKey(string key) => true;

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out Color value)
    {
        value = ColorFor(key);
        return true;
    }

    public IEnumerator<KeyValuePair<string, Color>> GetEnumerator() => curated.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private Color ColorFor(string category)
    {
        if (StartupImpactProfilerUtil.TrySplitReplaced(category, out var replaced, out _))
        {
            return ColorFor(replaced);
        }
        if (curated.TryGetValue(category, out var color))
        {
            return color;
        }

        var phase = StartupImpactPhaseViewData.PhaseKey(category);
        return curated.TryGetValue(phase, out var phaseColor)
            ? phaseColor
            : StartupImpactProfilerUtil.HashColor(phase);
    }
}
