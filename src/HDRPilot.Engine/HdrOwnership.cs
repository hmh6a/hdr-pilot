namespace HDRPilot.Engine;

// Memory-only lease. A new process never trusts a previous process's ownership.
public sealed class HdrOwnership(bool initialState)
{
    public bool Baseline { get; private set; } = initialState;
    public bool LastObserved { get; private set; } = initialState;
    public bool Owned { get; private set; }
    public bool ManualOverride { get; private set; }
    public bool Observe(bool actual)
    {
        if (actual == LastObserved) return false;
        LastObserved = actual; Baseline = actual; Owned = false; ManualOverride = true;
        return true;
    }
    public void RecordSuccessfulWrite(bool actual)
    { LastObserved = actual; Owned = actual != Baseline; }
    public void Yield(bool actual)
    { Baseline = actual; LastObserved = actual; Owned = false; ManualOverride = true; }
    public void Rearm(bool actual)
    { if (actual != LastObserved) { Owned = false; Baseline = actual; } LastObserved = actual; ManualOverride = false; }
    public bool? Target(bool? request)
    {
        if (ManualOverride) return null;
        if (request == true) return true;
        // Force SDR can release our HDR lease, but cannot turn off pre-existing HDR.
        return Owned ? Baseline : null;
    }
}
