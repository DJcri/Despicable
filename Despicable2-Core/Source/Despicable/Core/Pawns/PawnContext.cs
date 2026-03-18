using System;
using RimWorld.Planet;
using Verse;

namespace Despicable;
public static class PawnContext
{
    public static Map GetHeldMap(Pawn pawn)
    {
        return pawn?.MapHeld ?? pawn?.Map;
    }

    public static bool TryResolveSettlement(Pawn pawn, out Settlement settlement)
    {
        settlement = null;
        if (pawn == null) return false;

        try
        {
            Map map = GetHeldMap(pawn);
            if (map?.Parent is not Settlement resolvedSettlement)
                return false;

            settlement = resolvedSettlement;
            return settlement != null;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "PawnContext.TryResolveSettlement",
                "PawnContext failed to resolve settlement context for a pawn.",
                ex);
            settlement = null;
            return false;
        }
    }

    public static bool TryResolveSettlement(Pawn pawn, out string settlementUniqueId, out string settlementLabel)
    {
        settlementUniqueId = null;
        settlementLabel = null;

        if (!TryResolveSettlement(pawn, out Settlement settlement) || settlement == null)
            return false;

        settlementUniqueId = settlement.GetUniqueLoadID();
        settlementLabel = settlement.LabelCap;
        return !settlementUniqueId.NullOrEmpty();
    }

    public static bool TryResolveWordOfMouthSettlement(Pawn pawn, out Settlement settlement)
    {
        settlement = null;
        if (pawn == null) return false;

        try
        {
            if (TryResolveSettlement(pawn, out Settlement currentSettlement) && currentSettlement != null)
            {
                settlement = currentSettlement;
                return true;
            }

            if (!ShouldUseOffMapPlayerSettlementFallback(pawn))
                return false;

            return TryResolvePrimaryPlayerSettlement(out settlement);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "PawnContext.TryResolveWordOfMouthSettlement",
                "PawnContext failed to resolve word-of-mouth settlement context for a pawn.",
                ex);
            settlement = null;
            return false;
        }
    }

    public static bool TryResolveWordOfMouthSettlement(Pawn pawn, out string settlementUniqueId, out string settlementLabel)
    {
        settlementUniqueId = null;
        settlementLabel = null;

        if (!TryResolveWordOfMouthSettlement(pawn, out Settlement settlement) || settlement == null)
            return false;

        settlementUniqueId = settlement.GetUniqueLoadID();
        settlementLabel = settlement.LabelCap;
        return !settlementUniqueId.NullOrEmpty();
    }

    private static bool ShouldUseOffMapPlayerSettlementFallback(Pawn pawn)
    {
        if (pawn == null) return false;
        if (pawn.Faction == null || !pawn.Faction.IsPlayer) return false;

        try
        {
            Settings settings = CommonUtil.GetSettings();
            return settings == null || settings.heroKarmaAllowOffMapPlayerFactionSettlementWordOfMouth;
        }
        catch
        {
            return true;
        }
    }

    private static bool TryResolvePrimaryPlayerSettlement(out Settlement settlement)
    {
        settlement = null;

        try
        {
            if (Find.Maps != null)
            {
                for (int i = 0; i < Find.Maps.Count; i++)
                {
                    Map map = Find.Maps[i];
                    if (map?.Parent is Settlement mapSettlement && mapSettlement.Faction != null && mapSettlement.Faction.IsPlayer)
                    {
                        settlement = mapSettlement;
                        return true;
                    }
                }
            }

            var worldObjects = Find.WorldObjects?.AllWorldObjects;
            if (worldObjects == null)
                return false;

            for (int i = 0; i < worldObjects.Count; i++)
            {
                if (worldObjects[i] is Settlement worldSettlement && worldSettlement.Faction != null && worldSettlement.Faction.IsPlayer)
                {
                    settlement = worldSettlement;
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "PawnContext.TryResolvePrimaryPlayerSettlement",
                "PawnContext failed to resolve a player settlement fallback for local word-of-mouth.",
                ex);
            settlement = null;
            return false;
        }
    }
}
