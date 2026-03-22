using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
internal static class AnatomyVariantResolver
{
    internal static AnatomyPartVariantDef ResolveInstalledVariant(Pawn pawn, AnatomyPartDef part)
    {
        if (pawn == null || part == null)
            return null;

        List<AnatomyPartVariantDef> defs = DefDatabase<AnatomyPartVariantDef>.AllDefsListForReading;
        if (defs == null || defs.Count == 0)
            return null;

        AnatomyPartVariantDef best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < defs.Count; i++)
        {
            AnatomyPartVariantDef def = defs[i];
            if (!Matches(pawn, part, def))
                continue;

            int score = (def.priority * 100) + GetSpecificityScore(def.hediffDefs, def.geneDefs, def.raceDefs, def.pawnKindDefs, def.bodyTypes, def.genders, def.lifeStages);
            if (score <= bestScore)
                continue;

            bestScore = score;
            best = def;
        }

        return best;
    }

    internal static bool Matches(Pawn pawn, AnatomyPartDef part, AnatomyPartVariantDef def)
    {
        if (pawn == null || part == null || def == null || def.basePart != part)
            return false;

        return AnatomyResolver.MatchesHediffs(def.hediffDefs, pawn)
            && AnatomyResolver.MatchesGenes(def.geneDefs, pawn)
            && AnatomyResolver.MatchesThingDef(def.raceDefs, pawn.def)
            && AnatomyResolver.MatchesPawnKind(def.pawnKindDefs, pawn.kindDef)
            && AnatomyResolver.MatchesBodyType(def.bodyTypes, pawn.story?.bodyType)
            && AnatomyResolver.MatchesGender(def.genders, pawn.gender)
            && AnatomyResolver.MatchesLifeStage(def.lifeStages, pawn.ageTracker?.CurLifeStage);
    }

    internal static void ApplySizeGeneration(AnatomyPartVariantDef variant, ref float baseSize, ref float minSize, ref float maxSize)
    {
        if (variant == null)
            return;

        ApplyLinear(ref baseSize, ref minSize, ref maxSize, Mathf.Max(0f, variant.sizeMultiplier), variant.sizeOffset);
    }

    internal static void ApplyFluidGeneration(AnatomyPartVariantDef variant, AnatomyFluidTemplate template, ref float baseCapacity, ref float minCapacity, ref float maxCapacity, ref float initialAmountMultiplier, ref float initialAmountOffset)
    {
        if (variant?.fluids == null || template?.fluid == null)
            return;

        for (int i = 0; i < variant.fluids.Count; i++)
        {
            AnatomyVariantFluidModifier modifier = variant.fluids[i];
            if (modifier?.fluid != template.fluid)
                continue;

            ApplyLinear(ref baseCapacity, ref minCapacity, ref maxCapacity, Mathf.Max(0f, modifier.capacityMultiplier), modifier.capacityOffset);
            initialAmountMultiplier *= Mathf.Max(0f, modifier.initialAmountMultiplier);
            initialAmountOffset += modifier.initialAmountOffset;
        }
    }

    internal static float ApplyFluidRefillPerDay(AnatomyPartVariantDef variant, AnatomyFluidTemplate template, float refillPerDay)
    {
        if (variant?.fluids == null || template?.fluid == null)
            return Mathf.Max(0f, refillPerDay);

        float multiplier = 1f;
        float offset = 0f;
        for (int i = 0; i < variant.fluids.Count; i++)
        {
            AnatomyVariantFluidModifier modifier = variant.fluids[i];
            if (modifier?.fluid != template.fluid)
                continue;

            multiplier *= Mathf.Max(0f, modifier.refillRateMultiplier);
            offset += modifier.refillRateOffset;
        }

        return Mathf.Max(0f, (refillPerDay * multiplier) + offset);
    }

    private static int GetSpecificityScore(List<HediffDef> hediffDefs, List<GeneDef> geneDefs, List<ThingDef> raceDefs, List<PawnKindDef> pawnKindDefs, List<BodyTypeDef> bodyTypes, List<Gender> genders, List<LifeStageDef> lifeStages)
    {
        int score = 0;
        if (hediffDefs != null && hediffDefs.Count > 0)
            score += 128;
        if (geneDefs != null && geneDefs.Count > 0)
            score += 64;
        if (pawnKindDefs != null && pawnKindDefs.Count > 0)
            score += 32;
        if (raceDefs != null && raceDefs.Count > 0)
            score += 16;
        if (lifeStages != null && lifeStages.Count > 0)
            score += 8;
        if (bodyTypes != null && bodyTypes.Count > 0)
            score += 4;
        if (genders != null && genders.Count > 0)
            score += 2;
        return score;
    }

    private static void ApplyLinear(ref float baseValue, ref float minValue, ref float maxValue, float multiplier, float offset)
    {
        baseValue = (baseValue * multiplier) + offset;
        minValue = (minValue * multiplier) + offset;
        maxValue = (maxValue * multiplier) + offset;

        if (maxValue < minValue)
        {
            float swap = minValue;
            minValue = maxValue;
            maxValue = swap;
        }

        baseValue = Mathf.Clamp(baseValue, minValue, maxValue);
    }
}
