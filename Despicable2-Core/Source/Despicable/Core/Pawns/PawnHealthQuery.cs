using System;
using Verse;

namespace Despicable;
public static class PawnHealthQuery
{
    public const float EmergencyBleedRateThreshold = 0.10f;

    public static PawnHealthSnapshot Snapshot(Pawn pawn)
    {
        bool isDead = pawn?.Dead ?? false;
        bool isDowned = pawn?.Downed ?? false;
        float bleedRate = GetBleedRate(pawn);
        bool isInfant = PawnQuery.IsInfant(pawn);
        bool isAdult = pawn != null && !isInfant;
        return new PawnHealthSnapshot(isDead, isDowned, bleedRate, isInfant, isAdult);
    }

    public static float GetBleedRate(Pawn pawn)
    {
        try
        {
            if (pawn?.health?.hediffSet == null) return 0f;
            return pawn.health.hediffSet.BleedRateTotal;
        }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnHealthQuery.GetBleedRate", "PawnHealthQuery failed to read bleed rate.", ex); return 0f; }
    }

    public static bool IsEmergencyTendTarget(Pawn pawn, float bleedRateThreshold = EmergencyBleedRateThreshold)
    {
        return Snapshot(pawn).IsEmergencyTendTarget(bleedRateThreshold);
    }
}
