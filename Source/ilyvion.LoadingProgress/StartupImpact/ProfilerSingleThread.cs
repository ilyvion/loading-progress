namespace ilyvion.LoadingProgress.StartupImpact;

internal abstract class SingleThreadedProfiler(string measurementTarget)
{
    private readonly object _modificationLock = new();
    private readonly string _measurementTarget = measurementTarget;
    private readonly List<string> _categories = [];

    public float Total { get; private set; } = 0;

    public abstract void Start();
    public abstract float Stop();
    public virtual float StopAndStart()
    {
        float res = Stop();
        Start();
        return res;
    }

    private string ProfilerName => _measurementTarget == null ? "" : $"{_measurementTarget} profiler";

    public void Start(string category)
    {
        if (string.IsNullOrEmpty(category))
        {
            return;
        }

        lock (_modificationLock)
        {
            if (_categories.Count > 0)
            {
                float ms = StopAndStart();
                Total += ms;
            }
            else
            {
                Start();
            }

            _categories.Insert(0, category);
        }
    }

    public float Stop(string category)
    {
        return Stop(category, out var _);
    }

    public float Stop(string category, out string actualCategory)
    {
        lock (_modificationLock)
        {
            if (_categories.Count == 0)
            {
                if (category != null)
                {
                    Log.Error("Stopping " + ProfilerName + " for [" + category + "] while it's already inactive. Current categories: [" + string.Join(", ", _categories) + "]");
                }

                actualCategory = "none";
                return 0;
            }

            if (category != null && category != _categories[0])
            {
                Log.Error($"Stopping {ProfilerName} for [expected: {category}] but currently timing [actual: {_categories[0]}]");
            }

            actualCategory = _categories[0];
            _categories.RemoveAt(0);

            float ms = _categories.Count > 0 ? StopAndStart() : Stop();
            Total += ms;
            return ms;
        }
    }
}
