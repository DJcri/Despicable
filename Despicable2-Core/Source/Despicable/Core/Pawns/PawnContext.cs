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
}
