namespace Despicable.HeroKarma;
/// <summary>
/// Small owner for prune cadence state so patch helpers do not each carry loose tick globals.
/// </summary>
internal sealed class HKPruneTickTracker
{
    private readonly int _unsetTick;
    private int _lastPruneTick;

    internal HKPruneTickTracker(int unsetTick = -999999)
    {
        _unsetTick = unsetTick;
        _lastPruneTick = unsetTick;
    }

    internal bool ShouldSkipPrune(int entryCount, int maxEntries, int now, int minInterval)
    {
        return entryCount < maxEntries && now - _lastPruneTick < minInterval;
    }

    internal void MarkPruned(int now)
    {
        _lastPruneTick = now;
    }

    internal void Reset()
    {
        _lastPruneTick = _unsetTick;
    }
}
