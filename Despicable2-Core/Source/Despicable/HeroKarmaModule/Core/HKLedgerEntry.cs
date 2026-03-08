using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Save-safe primitive-only ledger entry (matches the plan).
/// No Pawn/Thing/Faction references.
/// </summary>
public class HKLedgerEntry : IExposable
{
    public string eventKey;
    public int delta; // karma delta
    public int standingDelta;

    public string label;
    public string detail;
    public string reason; // karma reason
    public string standingReason;

    public int tick;
    public int day;

    public string heroPawnId;

    // Optional targets as IDs
    public string targetPawnId;
    public int targetFactionId;

    public void ExposeData()
    {
        Scribe_Values.Look(ref eventKey, "k", null);
        Scribe_Values.Look(ref delta, "d", 0);
        Scribe_Values.Look(ref standingDelta, "sd", 0);

        Scribe_Values.Look(ref label, "l", null);
        Scribe_Values.Look(ref detail, "dt", null);
        Scribe_Values.Look(ref reason, "r", null);
        Scribe_Values.Look(ref standingReason, "sr", null);

        Scribe_Values.Look(ref tick, "t", 0);
        Scribe_Values.Look(ref day, "day", 0);

        Scribe_Values.Look(ref heroPawnId, "h", null);
        Scribe_Values.Look(ref targetPawnId, "tp", null);
        Scribe_Values.Look(ref targetFactionId, "tf", 0);
    }
}
