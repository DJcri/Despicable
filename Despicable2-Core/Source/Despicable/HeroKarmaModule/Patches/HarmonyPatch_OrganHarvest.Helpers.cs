using System;
using RimWorld;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;
public static partial class HarmonyPatch_OrganHarvest
{
    private static bool TryGetPawnPair(object[] args, out Pawn pawn, out Pawn billDoer)
    {
        pawn = null;
        billDoer = null;
        if (args == null)
        {
            return false;
        }

        foreach (var arg in args)
        {
            if (!(arg is Pawn currentPawn))
            {
                continue;
            }

            if (pawn == null)
            {
                pawn = currentPawn;
                continue;
            }

            billDoer = currentPawn;
            return true;
        }

        return false;
    }

    private static T FindFirstArg<T>(object[] args)
        where T : class
    {
        if (args == null)
        {
            return null;
        }

        foreach (var arg in args)
        {
            if (arg is T value)
            {
                return value;
            }
        }

        return null;
    }

    private static bool LooksLikeHarvestableOrgan(BodyPartRecord part)
    {
        try
        {
            if (part?.def == null)
            {
                return false;
            }

            var defName = part.def.defName;
            if (defName.NullOrEmpty())
            {
                return false;
            }

            return defName.IndexOf("Heart", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Lung", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Kidney", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Liver", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Stomach", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_OrganHarvest:101",
                "HarmonyPatch_OrganHarvest suppressed an exception.",
                ex);
            return false;
        }
    }
}
