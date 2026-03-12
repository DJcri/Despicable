using System.Collections.Generic;
using Verse;

namespace Despicable;
internal static class AnatomyQuery
{
    internal static bool TryGetExternalGenitals(Pawn pawn, out BodyPartRecord part)
    {
        part = null;

        List<BodyPartRecord> parts = pawn?.RaceProps?.body?.AllParts;
        if (parts == null || LovinModule_AnatomyDefOf.D2_ExternalGenitals == null)
            return false;

        for (int i = 0; i < parts.Count; i++)
        {
            if (parts[i].def == LovinModule_AnatomyDefOf.D2_ExternalGenitals)
            {
                part = parts[i];
                return true;
            }
        }

        return false;
    }

    internal static bool HasExternalGenitalsSlot(Pawn pawn)
    {
        return TryGetExternalGenitals(pawn, out _);
    }

    internal static bool IsExternalGenitalsMissing(Pawn pawn)
    {
        BodyPartRecord part;
        if (!TryGetExternalGenitals(pawn, out part))
            return false;

        return pawn?.health?.hediffSet?.PartIsMissing(part) == true;
    }

    internal static bool HasKnownExternalGenitalAnatomy(Pawn pawn)
    {
        return HasPenis(pawn) || HasVagina(pawn);
    }

    internal static bool HasPenis(Pawn pawn)
    {
        return HasPartHediff(pawn, LovinModule_AnatomyDefOf.D2_Genital_Penis);
    }

    internal static bool HasVagina(Pawn pawn)
    {
        return HasPartHediff(pawn, LovinModule_AnatomyDefOf.D2_Genital_Vagina);
    }

    private static bool HasPartHediff(Pawn pawn, HediffDef def)
    {
        if (pawn?.health?.hediffSet?.hediffs == null || def == null)
            return false;

        if (IsExternalGenitalsMissing(pawn))
            return false;

        BodyPartRecord part;
        if (!TryGetExternalGenitals(pawn, out part))
            return false;

        List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff hediff = hediffs[i];
            if (hediff?.def == def && hediff.Part == part)
                return true;
        }

        return false;
    }
}
