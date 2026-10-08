namespace ilyvion.LoadingProgress.StartupImpact.Dialog;

/// <summary>
/// One loading stage of a stored session: how long it ran, and how much of that some category
/// accounted for.
/// </summary>
internal sealed class StartupImpactStageData : IExposable
{
#pragma warning disable IDE0032 // Use auto property -- Scribe needs fields to take by ref
    private string stage = "";
    private float wallMs;
    private float attributedMs;
#pragma warning restore IDE0032 // Use auto property

    public StartupImpactStageData() { }

    public StartupImpactStageData(string stage, float wallMs, float attributedMs)
    {
        this.stage = stage;
        this.wallMs = wallMs;
        this.attributedMs = attributedMs;
    }

    public string Stage => stage;
    public float WallMs => wallMs;
    public float AttributedMs => attributedMs;
    public float RemainingMs => Math.Max(0f, wallMs - attributedMs);

    public static StartupImpactStageData FromLedgerEntry(StageLedgerEntry entry) =>
        new(entry.Stage, entry.WallMs, entry.AttributedMs);

    public void ExposeData()
    {
        Scribe_Values.Look(ref stage!, "stage");
        Scribe_Values.Look(ref wallMs, "wallMs");
        Scribe_Values.Look(ref attributedMs, "attributedMs");
    }
}
