using System;
using Verse;

namespace Despicable.HeroKarma;
public sealed class KarmaEvent
{
    public string eventKey;
    public Pawn actor;
    public string actorPawnId;
    public Pawn targetPawn;
    public string targetPawnId;
    public int targetFactionId;
    public int stage;
    public int amount;
    public int tick;
    public string settlementUniqueId;
    public string settlementLabel;
    public HKIdeologyEvaluationTrace ideologyTrace;

    public static KarmaEvent Create(string eventKey, Pawn actor, Pawn targetPawn = null, int targetFactionId = 0, int stage = 0, int amount = 0)
    {
        return new KarmaEvent
        {
            eventKey = eventKey,
            actor = actor,
            actorPawnId = actor != null ? actor.GetUniqueLoadID() : null,
            targetPawn = targetPawn,
            targetPawnId = targetPawn != null ? targetPawn.GetUniqueLoadID() : null,
            targetFactionId = targetFactionId,
            stage = stage,
            amount = amount,
            tick = Find.TickManager != null ? Find.TickManager.TicksGame : 0
        };
    }
}
