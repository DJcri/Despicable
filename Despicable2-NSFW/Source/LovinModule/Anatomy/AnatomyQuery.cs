using System.Collections.Generic;
using RimWorld;
using Verse;
using Despicable.NSFW.Integrations;
using Despicable.NSFW.Integrations.GenderWorks;

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
        return TryGetLogicalAnatomy(pawn, out _, out _);
    }

    internal static bool HasDespicableExternalGenitalAnatomy(Pawn pawn)
    {
        return HasLegacyPartHediff(pawn, LovinModule_AnatomyDefOf.D2_Genital_Penis)
            || HasLegacyPartHediff(pawn, LovinModule_AnatomyDefOf.D2_Genital_Vagina);
    }

    internal static bool HasPenis(Pawn pawn)
    {
        return TryGetLogicalAnatomy(pawn, out bool hasPenis, out _) && hasPenis;
    }

    internal static bool HasVagina(Pawn pawn)
    {
        return TryGetLogicalAnatomy(pawn, out _, out bool hasVagina) && hasVagina;
    }

    internal static bool TryGetLogicalAnatomy(Pawn pawn, out bool hasPenis, out bool hasVagina)
    {
        hasPenis = false;
        hasVagina = false;

        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return false;

        if (IsExternalGenitalsMissing(pawn))
            return true;

        CompAnatomyBootstrap tracker = pawn.TryGetComp<CompAnatomyBootstrap>();
        if (tracker != null)
        {
            if (tracker.TryGetResolvedAnatomy(out hasPenis, out hasVagina))
                return true;

            AnatomyBootstrapper.TryResolveAndApply(pawn);
            if (tracker.TryGetResolvedAnatomy(out hasPenis, out hasVagina))
                return true;
        }

        if (TryGetFallbackLogicalAnatomy(pawn, out hasPenis, out hasVagina))
            return true;

        hasPenis = false;
        hasVagina = false;
        return false;
    }

    private static bool TryGetFallbackLogicalAnatomy(Pawn pawn, out bool hasPenis, out bool hasVagina)
    {
        hasPenis = false;
        hasVagina = false;

        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return false;

        bool legacyPenis = HasLegacyPartHediff(pawn, LovinModule_AnatomyDefOf.D2_Genital_Penis);
        bool legacyVagina = HasLegacyPartHediff(pawn, LovinModule_AnatomyDefOf.D2_Genital_Vagina);
        if (legacyPenis || legacyVagina)
        {
            hasPenis = legacyPenis;
            hasVagina = legacyVagina;
            return true;
        }

        if (IntegrationGuards.IsGenderWorksLoaded())
        {
            if (GenderWorksUtil.TryResolveForDespicable(pawn, out hasPenis, out hasVagina))
                return true;
        }

        hasPenis = pawn.gender == Gender.Male;
        hasVagina = pawn.gender == Gender.Female;
        return true;
    }

    private static bool HasLegacyPartHediff(Pawn pawn, HediffDef def)
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
