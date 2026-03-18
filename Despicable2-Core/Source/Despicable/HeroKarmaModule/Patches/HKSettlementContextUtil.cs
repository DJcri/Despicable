using System;
using RimWorld.Planet;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;
internal static class HKSettlementContextUtil
{
    public static bool TryAssignFromSettlement(KarmaEvent karmaEvent, Settlement settlement)
    {
        if (karmaEvent == null || !IsForeignSettlement(settlement))
            return false;

        return AssignSettlementContext(karmaEvent, settlement);
    }

    public static bool TryAssignFromPawns(KarmaEvent karmaEvent, Pawn primaryPawn, Pawn fallbackPawn = null)
    {
        if (karmaEvent == null)
            return false;

        return TryResolveSettlementFromPawn(primaryPawn, out Settlement settlement)
            ? AssignSettlementContext(karmaEvent, settlement)
            : TryResolveSettlementFromPawn(fallbackPawn, out Settlement fallbackSettlement) && AssignSettlementContext(karmaEvent, fallbackSettlement);
    }

    public static bool TryResolveSettlementFromPawn(Pawn pawn, out Settlement settlement)
    {
        settlement = null;
        if (!global::Despicable.PawnContext.TryResolveWordOfMouthSettlement(pawn, out Settlement resolved) || resolved == null)
            return false;

        settlement = resolved;
        return true;
    }

    public static string TryResolveSettlementUniqueId(GlobalTargetInfo lookTarget, string warnKey = null, string warnMessage = null)
    {
        return TryResolveSettlementFromLookTarget(lookTarget, out Settlement settlement, warnKey, warnMessage)
            ? settlement.GetUniqueLoadID()
            : null;
    }

    public static string TryResolveSettlementLabel(GlobalTargetInfo lookTarget, string warnKey = null, string warnMessage = null)
    {
        return TryResolveSettlementFromLookTarget(lookTarget, out Settlement settlement, warnKey, warnMessage)
            ? settlement.LabelCap
            : null;
    }

    private static bool TryResolveSettlementFromLookTarget(GlobalTargetInfo lookTarget, out Settlement settlement, string warnKey, string warnMessage)
    {
        settlement = null;
        try
        {
            if (lookTarget.WorldObject is not Settlement candidate || !IsForeignSettlement(candidate))
                return false;

            settlement = candidate;
            return true;
        }
        catch (Exception ex)
        {
            if (!warnKey.NullOrEmpty() && !warnMessage.NullOrEmpty())
                Despicable.Core.DebugLogger.WarnExceptionOnce(warnKey, warnMessage, ex);

            settlement = null;
            return false;
        }
    }

    private static bool AssignSettlementContext(KarmaEvent karmaEvent, Settlement settlement)
    {
        if (karmaEvent == null || settlement == null)
            return false;

        karmaEvent.settlementUniqueId = settlement.GetUniqueLoadID();
        karmaEvent.settlementLabel = settlement.LabelCap;
        return true;
    }

    private static bool IsForeignSettlement(Settlement settlement)
    {
        return settlement != null && settlement.Faction != null && !settlement.Faction.IsPlayer;
    }
}
