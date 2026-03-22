using System;
using RimWorld;
using Verse;

namespace Despicable;
public static class PawnQuery
{
    public static bool IsAsleep(Pawn pawn)
    {
        if (pawn == null) return false;
        return pawn.CurJobDef == JobDefOf.LayDownResting || pawn.CurJobDef == JobDefOf.LayDown;
    }

    public static bool IsInfant(Pawn pawn)
    {
        if (pawn?.ageTracker == null) return false;

        int curLifeStage = pawn.ageTracker.CurLifeStageIndex;
        return curLifeStage == 0 || curLifeStage == 1;
    }

    public static bool IsAdult(Pawn pawn)
    {
        return pawn != null && !IsInfant(pawn);
    }

    public static bool CompareGenderToByte(Pawn pawn, byte otherGender)
    {
        if (pawn == null) return false;

        byte pawnGender = (byte)pawn.gender;
        return pawnGender == otherGender;
    }

    public static bool IsAnimal(Pawn pawn)
    {
        try { return pawn != null && pawn.RaceProps != null && pawn.RaceProps.Animal; }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnQuery.IsAnimal", "PawnQuery failed to read animal race state.", ex); return false; }
    }

    public static bool IsHumanlike(Pawn pawn)
    {
        try { return pawn != null && pawn.RaceProps != null && pawn.RaceProps.Humanlike; }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnQuery.IsHumanlike", "PawnQuery failed to read humanlike race state.", ex); return false; }
    }

    public static bool IsMechanoid(Pawn pawn)
    {
        try { return pawn != null && pawn.RaceProps != null && pawn.RaceProps.IsMechanoid; }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnQuery.IsMechanoid", "PawnQuery failed to read mechanoid race state.", ex); return false; }
    }

    public static bool IsSpawned(Pawn pawn)
    {
        try { return pawn != null && pawn.Spawned; }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnQuery.IsSpawned", "PawnQuery failed to read spawned state.", ex); return false; }
    }

    public static bool IsVisibleToPlayer(Pawn pawn)
    {
        try { return pawn != null && !pawn.IsHiddenFromPlayer(); }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnQuery.IsVisibleToPlayer", "PawnQuery failed to read player visibility state.", ex); return false; }
    }

    public static bool IsVisibleHumanlikeSpawned(Pawn pawn)
    {
        return IsHumanlike(pawn) && IsSpawned(pawn) && IsVisibleToPlayer(pawn);
    }

    public static bool IsCurrentlyGuilty(Pawn pawn)
    {
        try { return pawn != null && pawn.guilt != null && pawn.guilt.IsGuilty; }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnQuery.IsCurrentlyGuilty", "PawnQuery failed to read guilt state.", ex); return false; }
    }

    public static bool IsPermanentManhunterOrBerserk(Pawn pawn)
    {
        try
        {
            if (pawn == null || pawn.mindState == null || pawn.mindState.mentalStateHandler == null) return false;
            var mentalState = pawn.mindState.mentalStateHandler.CurState;
            string defName = mentalState?.def?.defName;
            if (defName.NullOrEmpty()) return false;

            if (defName.IndexOf("ManhunterPermanent", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (defName.IndexOf("Berserk", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnQuery.IsPermanentManhunterOrBerserk", "PawnQuery failed to read mental state classification.", ex); return false; }
    }
}
