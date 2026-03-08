using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Primitive-only recent change entry for UI/debug.
/// </summary>
public class LocalRepChangeEntry : IExposable
{
    public string heroId;
    public bool isFaction;
    public string targetId; // pawnId or factionId string ("F#")
    public int delta;
    public int tick;
    public string reason;
    public string eventKey;
    public int baseDelta;
    public string affectedByLabel;

    public void ExposeData()
    {
        Scribe_Values.Look(ref heroId, "h", null);
        Scribe_Values.Look(ref isFaction, "f", false);
        Scribe_Values.Look(ref targetId, "tid", null);
        Scribe_Values.Look(ref delta, "d", 0);
        Scribe_Values.Look(ref tick, "t", 0);
        Scribe_Values.Look(ref reason, "r", null);
        Scribe_Values.Look(ref eventKey, "e", null);
        Scribe_Values.Look(ref baseDelta, "bd", 0);
        Scribe_Values.Look(ref affectedByLabel, "ab", null);
    }
}
