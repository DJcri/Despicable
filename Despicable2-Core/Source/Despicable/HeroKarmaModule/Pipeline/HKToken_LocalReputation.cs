using RimWorld;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Pipeline token: apply local reputation deltas via LocalReputationUtility.
///
/// This is the "official" way for the Karma pipeline to modify local rep,
/// keeping the single-entry-point architecture intact.
/// </summary>
public abstract class HKToken_LocalReputation : IHKEffectToken
{
    public readonly int Delta;
    public readonly string EventKey;
    public readonly string Reason;

    protected HKToken_LocalReputation(int delta, string eventKey, string reason)
    {
        Delta = delta;
        EventKey = eventKey;
        Reason = reason;
    }

    public abstract void Apply(Pawn hero);
}

public sealed class HKToken_LocalRepPawn : HKToken_LocalReputation
{
    public readonly string TargetPawnId;
    public readonly int BaseDelta;
    public readonly string AffectedByLabel;

    public HKToken_LocalRepPawn(string targetPawnId, int delta, string eventKey, string reason, int baseDelta = 0, string affectedByLabel = null)
        : base(delta, eventKey, reason)
    {
        TargetPawnId = targetPawnId;
        BaseDelta = baseDelta;
        AffectedByLabel = affectedByLabel;
    }

    public override void Apply(Pawn hero)
    {
        if (hero == null) return;
        if (TargetPawnId.NullOrEmpty()) return;

        // Resolve pawn by ID safely.
        var target = HKResolve.TryResolvePawnById(TargetPawnId);
        if (target == null) return;

        LocalReputationUtility.TryApplyPawnDelta(hero, target, Delta, EventKey, Reason, BaseDelta, AffectedByLabel);
    }
}

public sealed class HKToken_LocalRepFaction : HKToken_LocalReputation
{
    public readonly int FactionId;

    public HKToken_LocalRepFaction(int factionId, int delta, string eventKey, string reason)
        : base(delta, eventKey, reason)
    {
        FactionId = factionId;
    }

    public override void Apply(Pawn hero)
    {
        if (hero == null) return;
        if (FactionId <= 0) return;

        var faction = HKResolve.TryResolveFactionById(FactionId);
        if (faction == null) return;

        LocalReputationUtility.TryApplyFactionDelta(hero, faction, Delta, EventKey, Reason);
    }
}

public sealed class HKToken_LocalRepSettlement : HKToken_LocalReputation
{
    public readonly string SettlementUniqueId;
    public readonly bool RecordRecent;

    public HKToken_LocalRepSettlement(string settlementUniqueId, int delta, string eventKey, string reason, bool recordRecent = false)
        : base(delta, eventKey, reason)
    {
        SettlementUniqueId = settlementUniqueId;
        RecordRecent = recordRecent;
    }

    public override void Apply(Pawn hero)
    {
        if (hero == null) return;
        if (SettlementUniqueId.NullOrEmpty()) return;

        LocalReputationUtility.TryApplySettlementDeltaByUniqueId(hero, SettlementUniqueId, Delta, EventKey, Reason, RecordRecent);
    }
}
