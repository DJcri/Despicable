using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Per-target local reputation record.
/// Includes anti-farm bookkeeping (daily caps + cooldown).
/// </summary>
public class RepRecord : IExposable
{
    public int score;
    public int lastChangedTick;
    public string lastReason;
    public string lastEventKey;
    public int lastDelta;
    public int lastBaseDelta;
    public string lastAffectedByLabel;

    // Anti-farm fields
    public int lastDay;
    public int dayAbsApplied;
    public int dayCount;
    public int lastAppliedTick;

    public void ExposeData()
    {
        Scribe_Values.Look(ref score, "s", 0);
        Scribe_Values.Look(ref lastChangedTick, "t", 0);
        Scribe_Values.Look(ref lastReason, "r", null);
        Scribe_Values.Look(ref lastEventKey, "e", null);
        Scribe_Values.Look(ref lastDelta, "ld", 0);
        Scribe_Values.Look(ref lastBaseDelta, "lbd", 0);
        Scribe_Values.Look(ref lastAffectedByLabel, "lab", null);
        Scribe_Values.Look(ref lastDay, "d", -1);
        Scribe_Values.Look(ref dayAbsApplied, "da", 0);
        Scribe_Values.Look(ref dayCount, "dc", 0);
        Scribe_Values.Look(ref lastAppliedTick, "lat", 0);
    }
}
