using System;
using System.Collections;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Despicable;
public static class PawnAffiliation
{
    public static int GetNonPlayerFactionIdSafe(Pawn pawn)
    {
        try
        {
            if (pawn == null) return 0;
            Faction faction = pawn.Faction;
            if (faction == null || faction.IsPlayer) return 0;
            return faction.loadID;
        }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnAffiliation.GetNonPlayerFactionIdSafe", "PawnAffiliation failed to resolve faction identity.", ex); return 0; }
    }

    public static bool IsGuestLike(Pawn pawn)
    {
        try
        {
            if (pawn == null) return false;
            if (!PawnQuery.IsHumanlike(pawn) || PawnQuery.IsMechanoid(pawn)) return false;
            if (pawn.guest == null) return false;
            if (IsPrisonerLike(pawn) || IsSlaveLike(pawn)) return false;

            return pawn.HostFaction != null || pawn.IsQuestLodger();
        }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnAffiliation.IsGuestLike", "PawnAffiliation failed to read guest state.", ex); return false; }
    }

    public static bool IsSlaveLike(Pawn pawn)
    {
        try
        {
            if (pawn == null) return false;

            var slaveProp = AccessTools.Property(pawn.GetType(), "IsSlave");
            if (slaveProp != null && slaveProp.PropertyType == typeof(bool))
                return (bool)slaveProp.GetValue(pawn, null);

            if (pawn.guest != null)
            {
                object guestStatus = AccessTools.Property(pawn.guest.GetType(), "GuestStatus")?.GetValue(pawn.guest, null);
                if (guestStatus != null && guestStatus.ToString().IndexOf("Slave", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnAffiliation.IsSlaveLike", "PawnAffiliation failed to classify slave state.", ex); return false; }
    }

    public static bool IsPrisonerLike(Pawn pawn)
    {
        try
        {
            if (pawn == null) return false;

            var colonyProp = AccessTools.Property(pawn.GetType(), "IsPrisonerOfColony");
            if (colonyProp != null && colonyProp.PropertyType == typeof(bool) && (bool)colonyProp.GetValue(pawn, null))
                return true;

            var prisonerProp = AccessTools.Property(pawn.GetType(), "IsPrisoner");
            if (prisonerProp != null && prisonerProp.PropertyType == typeof(bool) && (bool)prisonerProp.GetValue(pawn, null))
                return true;

            if (pawn.guest != null)
            {
                object guestStatus = AccessTools.Property(pawn.guest.GetType(), "GuestStatus")?.GetValue(pawn.guest, null);
                if (guestStatus != null && guestStatus.ToString().IndexOf("Prison", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnAffiliation.IsPrisonerLike", "PawnAffiliation failed to classify prisoner state.", ex); return false; }
    }

    public static bool IsLikelyBeggarsQuestPawn(Pawn pawn)
    {
        try
        {
            if (pawn == null) return false;
            if (pawn.Faction != null && pawn.Faction.IsPlayer) return false;
            if (!IsGuestLike(pawn)) return false;

            var field = AccessTools.Field(pawn.GetType(), "questTags");
            IEnumerable tags = field != null ? field.GetValue(pawn) as IEnumerable : null;
            if (tags == null) return false;

            foreach (object raw in tags)
            {
                string tag = raw as string;
                if (tag.NullOrEmpty()) continue;

                if (tag.IndexOf("beggar", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (tag.IndexOf("charity", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                if (tag.IndexOf("alms", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }

            return false;
        }
        catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("PawnAffiliation.IsLikelyBeggarsQuestPawn", "PawnAffiliation failed to inspect quest-style affiliation tags.", ex); return false; }
    }
}
