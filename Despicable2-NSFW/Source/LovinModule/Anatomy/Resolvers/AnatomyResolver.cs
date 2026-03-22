using System.Collections.Generic;
using RimWorld;
using Verse;
using Despicable.NSFW.Integrations;
using Despicable.NSFW.Integrations.GenderWorks;

namespace Despicable;
internal static class AnatomyResolver
{
    internal static bool TryResolveDesiredParts(Pawn pawn, out List<AnatomyPartDef> parts)
    {
        parts = null;

        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return false;

        HashSet<AnatomyPartDef> resolved = new HashSet<AnatomyPartDef>();

        if (TryResolveFromLegacyHediffs(pawn, resolved))
        {
            parts = ToOrderedList(resolved);
            return true;
        }

        if (IntegrationGuards.IsGenderWorksLoaded() && TryResolveFromGenderWorks(pawn, resolved))
        {
            parts = ToOrderedList(resolved);
            return true;
        }

        if (TryResolveFromProfiles(pawn, resolved))
        {
            parts = ToOrderedList(resolved);
            return true;
        }

        if (TryResolveFromGenderFallback(pawn, resolved))
        {
            parts = ToOrderedList(resolved);
            return true;
        }

        return false;
    }

    private static bool TryResolveFromLegacyHediffs(Pawn pawn, HashSet<AnatomyPartDef> resolved)
    {
        bool hasLegacy = false;
        if (AnatomyQuery.HasLegacyPartHediff(pawn, LovinModule_AnatomyDefOf.D2_Genital_Penis))
        {
            hasLegacy = true;
            AddIfNotNull(resolved, LovinModule_GenitalDefOf.Genital_Penis);
        }

        if (AnatomyQuery.HasLegacyPartHediff(pawn, LovinModule_AnatomyDefOf.D2_Genital_Vagina))
        {
            hasLegacy = true;
            AddIfNotNull(resolved, LovinModule_GenitalDefOf.Genital_Vagina);
        }

        return hasLegacy;
    }

    private static bool TryResolveFromGenderWorks(Pawn pawn, HashSet<AnatomyPartDef> resolved)
    {
        if (!GenderWorksUtil.TryResolveForDespicable(pawn, out bool hasPenis, out bool hasVagina))
            return false;

        if (hasPenis)
            AddIfNotNull(resolved, LovinModule_GenitalDefOf.Genital_Penis);

        if (hasVagina)
            AddIfNotNull(resolved, LovinModule_GenitalDefOf.Genital_Vagina);

        return resolved.Count > 0;
    }

    private static bool TryResolveFromProfiles(Pawn pawn, HashSet<AnatomyPartDef> resolved)
    {
        List<AnatomyProfileDef> defs = DefDatabase<AnatomyProfileDef>.AllDefsListForReading;
        if (defs == null || defs.Count == 0)
            return false;

        bool matchedAny = false;
        for (int i = 0; i < defs.Count; i++)
        {
            AnatomyProfileDef def = defs[i];
            if (!MatchesProfile(pawn, def))
                continue;

            matchedAny = true;
            AddParts(resolved, def.parts);
        }

        return matchedAny && resolved.Count > 0;
    }

    private static bool MatchesProfile(Pawn pawn, AnatomyProfileDef def)
    {
        if (pawn == null || def == null)
            return false;

        if (def.humanlikeOnly && pawn.RaceProps?.Humanlike != true)
            return false;

        if (!MatchesThingDef(def.raceDefs, pawn.def))
            return false;

        if (!MatchesGenes(def.geneDefs, pawn))
            return false;

        if (!MatchesPawnKind(def.pawnKindDefs, pawn.kindDef))
            return false;

        if (!MatchesBodyType(def.bodyTypes, pawn.story?.bodyType))
            return false;

        if (!MatchesGender(def.genders, pawn.gender))
            return false;

        if (!MatchesLifeStage(def.lifeStages, pawn.ageTracker?.CurLifeStage))
            return false;

        return true;
    }

    private static bool TryResolveFromGenderFallback(Pawn pawn, HashSet<AnatomyPartDef> resolved)
    {
        if (pawn.gender == Gender.Male)
            AddIfNotNull(resolved, LovinModule_GenitalDefOf.Genital_Penis);

        if (pawn.gender == Gender.Female)
            AddIfNotNull(resolved, LovinModule_GenitalDefOf.Genital_Vagina);

        return resolved.Count > 0;
    }

    internal static bool MatchesThingDef(List<ThingDef> targets, ThingDef current)
    {
        if (targets == null || targets.Count == 0)
            return true;

        if (current == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == current)
                return true;
        }

        return false;
    }

    internal static bool MatchesPawnKind(List<PawnKindDef> targets, PawnKindDef current)
    {
        if (targets == null || targets.Count == 0)
            return true;

        if (current == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == current)
                return true;
        }

        return false;
    }

    internal static bool MatchesBodyType(List<BodyTypeDef> targets, BodyTypeDef current)
    {
        if (targets == null || targets.Count == 0)
            return true;

        if (current == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == current)
                return true;
        }

        return false;
    }

    internal static bool MatchesGender(List<Gender> targets, Gender current)
    {
        if (targets == null || targets.Count == 0)
            return true;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == current)
                return true;
        }

        return false;
    }

    internal static bool MatchesLifeStage(List<LifeStageDef> targets, LifeStageDef current)
    {
        if (targets == null || targets.Count == 0)
            return true;

        if (current == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == current)
                return true;
        }

        return false;
    }

    internal static bool MatchesHediffs(List<HediffDef> targets, Pawn pawn)
    {
        if (targets == null || targets.Count == 0)
            return true;

        List<Hediff> hediffs = pawn?.health?.hediffSet?.hediffs;
        if (hediffs == null)
            return false;

        for (int i = 0; i < hediffs.Count; i++)
        {
            HediffDef current = hediffs[i]?.def;
            if (current == null)
                continue;

            for (int j = 0; j < targets.Count; j++)
            {
                if (targets[j] == current)
                    return true;
            }
        }

        return false;
    }

    internal static bool MatchesGenes(List<GeneDef> targets, Pawn pawn)
    {
        if (targets == null || targets.Count == 0)
            return true;

        if (pawn?.genes == null)
            return false;

        List<Gene> genes = pawn.genes.GenesListForReading;
        if (genes == null)
            return false;

        for (int i = 0; i < genes.Count; i++)
        {
            Gene gene = genes[i];
            GeneDef current = gene?.def;
            if (current == null || !gene.Active)
                continue;

            for (int j = 0; j < targets.Count; j++)
            {
                if (targets[j] == current)
                    return true;
            }
        }

        return false;
    }

    private static void AddParts(HashSet<AnatomyPartDef> resolved, List<AnatomyPartDef> parts)
    {
        if (parts == null)
            return;

        for (int i = 0; i < parts.Count; i++)
            AddIfNotNull(resolved, parts[i]);
    }

    private static void AddIfNotNull(HashSet<AnatomyPartDef> resolved, AnatomyPartDef part)
    {
        if (resolved == null || part == null)
            return;

        resolved.Add(part);
    }

    private static List<AnatomyPartDef> ToOrderedList(HashSet<AnatomyPartDef> parts)
    {
        List<AnatomyPartDef> result = new List<AnatomyPartDef>();
        if (parts == null)
            return result;

        foreach (AnatomyPartDef part in parts)
            result.Add(part);

        result.Sort((a, b) => string.CompareOrdinal(a?.defName, b?.defName));
        return result;
    }
}
