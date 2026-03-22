using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
internal enum AnatomyBootstrapResult
{
    Pending,
    Resolved,
    Skipped
}

internal static class AnatomyBootstrapper
{
    internal static AnatomyBootstrapResult TryResolveAndApply(Pawn pawn, bool allowGenderWorksNoSignalResolution = false, bool forceResync = false)
    {
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return AnatomyBootstrapResult.Skipped;

        if (pawn.health?.hediffSet == null)
            return AnatomyBootstrapResult.Pending;

        CompAnatomyBootstrap tracker = pawn.TryGetComp<CompAnatomyBootstrap>();
        if (tracker == null)
            return AnatomyBootstrapResult.Skipped;

        bool hadLegacyState = HasLegacyNaturalAnatomyHediffs(pawn);
        if (!forceResync && tracker.HasResolvedAnatomy && !hadLegacyState)
            return AnatomyBootstrapResult.Resolved;

        if (!AnatomyResolver.TryResolveDesiredParts(pawn, out List<AnatomyPartDef> parts))
            return AnatomyBootstrapResult.Pending;

        AnatomyQuery.RemoveMissingResolvedParts(pawn, parts);
        tracker.SetResolvedParts(parts);
        RemoveLegacyNaturalAnatomyHediffs(pawn);
        return AnatomyBootstrapResult.Resolved;
    }

    internal static bool ForceSeedFromCurrentGender(Pawn pawn)
    {
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return false;

        if (pawn.health?.hediffSet == null)
            return false;

        CompAnatomyBootstrap tracker = pawn.TryGetComp<CompAnatomyBootstrap>();
        if (tracker == null)
            return false;

        if (!AnatomyResolver.TryResolveDesiredParts(pawn, out List<AnatomyPartDef> parts))
            return false;

        AnatomyQuery.RemoveMissingResolvedParts(pawn, parts);
        tracker.SetResolvedParts(parts);
        RemoveLegacyNaturalAnatomyHediffs(pawn);
        return true;
    }

    internal static bool ForcePreviewSeedFromCurrentGender(Pawn pawn)
    {
        return ForceSeedFromCurrentGender(pawn);
    }

    private static bool HasLegacyNaturalAnatomyHediffs(Pawn pawn)
    {
        return HasLegacyNaturalAnatomyHediff(pawn, LovinModule_AnatomyDefOf.D2_Genital_Penis)
            || HasLegacyNaturalAnatomyHediff(pawn, LovinModule_AnatomyDefOf.D2_Genital_Vagina);
    }

    private static bool HasLegacyNaturalAnatomyHediff(Pawn pawn, HediffDef def)
    {
        if (pawn?.health?.hediffSet?.hediffs == null || def == null)
            return false;

        if (AnatomyQuery.IsExternalGenitalsMissing(pawn))
            return false;

        if (!AnatomyQuery.TryGetLegacyExternalGenitals(pawn, out BodyPartRecord part))
            return false;

        var hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff hediff = hediffs[i];
            if (hediff?.def == def && hediff.Part == part)
                return true;
        }

        return false;
    }

    private static void RemoveLegacyNaturalAnatomyHediffs(Pawn pawn)
    {
        if (pawn?.health?.hediffSet?.hediffs == null)
            return;

        if (!AnatomyQuery.TryGetLegacyExternalGenitals(pawn, out BodyPartRecord part))
            return;

        RemoveLegacyNaturalAnatomyHediff(pawn, part, LovinModule_AnatomyDefOf.D2_Genital_Penis);
        RemoveLegacyNaturalAnatomyHediff(pawn, part, LovinModule_AnatomyDefOf.D2_Genital_Vagina);
    }

    private static void RemoveLegacyNaturalAnatomyHediff(Pawn pawn, BodyPartRecord part, HediffDef def)
    {
        if (def == null)
            return;

        Hediff hediff = GetPartHediff(pawn, part, def);
        if (hediff != null)
            pawn.health.RemoveHediff(hediff);
    }

    private static Hediff GetPartHediff(Pawn pawn, BodyPartRecord part, HediffDef def)
    {
        var hediffs = pawn?.health?.hediffSet?.hediffs;
        if (hediffs == null)
            return null;

        for (int i = 0; i < hediffs.Count; i++)
        {
            Hediff hediff = hediffs[i];
            if (hediff?.def == def && hediff.Part == part)
                return hediff;
        }

        return null;
    }
}
