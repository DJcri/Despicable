using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
internal static class AnatomyGeneModifierResolver
{
    internal static void ResolveSizeGeneration(Pawn pawn, AnatomyPartDef part, out float baseSize, out float minSize, out float maxSize)
    {
        baseSize = part?.baseSize ?? 1f;
        minSize = part?.minSize ?? baseSize;
        maxSize = part?.maxSize ?? baseSize;
        ApplySizeGenerationModifiers(pawn, part, ref baseSize, ref minSize, ref maxSize);
    }

    internal static void ApplySizeGenerationModifiers(Pawn pawn, AnatomyPartDef part, ref float baseSize, ref float minSize, ref float maxSize)
    {
        if (part == null)
            return;

        float multiplier = 1f;
        float offset = 0f;

        List<AnatomyGeneModifierDef> defs = DefDatabase<AnatomyGeneModifierDef>.AllDefsListForReading;
        if (defs == null)
        {
            ApplyLinear(ref baseSize, ref minSize, ref maxSize, multiplier, offset);
            return;
        }

        for (int i = 0; i < defs.Count; i++)
        {
            AnatomyGeneModifierDef def = defs[i];
            if (!Matches(pawn, def))
                continue;

            List<AnatomyGenePartModifier> partModifiers = def.parts;
            if (partModifiers == null)
                continue;

            for (int j = 0; j < partModifiers.Count; j++)
            {
                AnatomyGenePartModifier modifier = partModifiers[j];
                if (modifier?.part != part)
                    continue;

                multiplier *= Mathf.Max(0f, modifier.sizeMultiplier);
                offset += modifier.sizeOffset;
            }
        }

        ApplyLinear(ref baseSize, ref minSize, ref maxSize, multiplier, offset);
    }

    internal static void ResolveFluidGeneration(Pawn pawn, AnatomyPartDef part, AnatomyFluidTemplate template, out float baseCapacity, out float minCapacity, out float maxCapacity, out float initialAmountMultiplier, out float initialAmountOffset)
    {
        baseCapacity = template?.baseCapacity ?? 0f;
        minCapacity = template?.minCapacity ?? 0f;
        maxCapacity = template?.maxCapacity ?? 0f;
        initialAmountMultiplier = 1f;
        initialAmountOffset = 0f;
        ApplyFluidGenerationModifiers(pawn, part, template, ref baseCapacity, ref minCapacity, ref maxCapacity, ref initialAmountMultiplier, ref initialAmountOffset);
    }

    internal static void ApplyFluidGenerationModifiers(Pawn pawn, AnatomyPartDef part, AnatomyFluidTemplate template, ref float baseCapacity, ref float minCapacity, ref float maxCapacity, ref float initialAmountMultiplier, ref float initialAmountOffset)
    {
        if (part == null || template?.fluid == null)
        {
            ApplyLinear(ref baseCapacity, ref minCapacity, ref maxCapacity, 1f, 0f);
            return;
        }

        float capacityMultiplier = 1f;
        float capacityOffset = 0f;

        List<AnatomyGeneModifierDef> defs = DefDatabase<AnatomyGeneModifierDef>.AllDefsListForReading;
        if (defs == null)
        {
            ApplyLinear(ref baseCapacity, ref minCapacity, ref maxCapacity, capacityMultiplier, capacityOffset);
            return;
        }

        for (int i = 0; i < defs.Count; i++)
        {
            AnatomyGeneModifierDef def = defs[i];
            if (!Matches(pawn, def))
                continue;

            List<AnatomyGenePartModifier> partModifiers = def.parts;
            if (partModifiers == null)
                continue;

            for (int j = 0; j < partModifiers.Count; j++)
            {
                AnatomyGenePartModifier partModifier = partModifiers[j];
                if (partModifier?.part != part || partModifier.fluids == null)
                    continue;

                for (int k = 0; k < partModifier.fluids.Count; k++)
                {
                    AnatomyGeneFluidModifier fluidModifier = partModifier.fluids[k];
                    if (fluidModifier?.fluid != template.fluid)
                        continue;

                    capacityMultiplier *= Mathf.Max(0f, fluidModifier.capacityMultiplier);
                    capacityOffset += fluidModifier.capacityOffset;
                    initialAmountMultiplier *= Mathf.Max(0f, fluidModifier.initialAmountMultiplier);
                    initialAmountOffset += fluidModifier.initialAmountOffset;
                }
            }
        }

        ApplyLinear(ref baseCapacity, ref minCapacity, ref maxCapacity, capacityMultiplier, capacityOffset);
    }

    internal static float ResolveFluidRefillPerDay(Pawn pawn, AnatomyPartDef part, AnatomyFluidTemplate template)
    {
        float refillPerDay = template?.refillPerDay ?? 0f;
        return ApplyFluidRefillModifiers(pawn, part, template, refillPerDay);
    }

    internal static float ApplyFluidRefillModifiers(Pawn pawn, AnatomyPartDef part, AnatomyFluidTemplate template, float refillPerDay)
    {
        if (part == null || template?.fluid == null)
            return Mathf.Max(0f, refillPerDay);

        float multiplier = 1f;
        float offset = 0f;

        List<AnatomyGeneModifierDef> defs = DefDatabase<AnatomyGeneModifierDef>.AllDefsListForReading;
        if (defs == null)
            return Mathf.Max(0f, (refillPerDay * multiplier) + offset);

        for (int i = 0; i < defs.Count; i++)
        {
            AnatomyGeneModifierDef def = defs[i];
            if (!Matches(pawn, def))
                continue;

            List<AnatomyGenePartModifier> partModifiers = def.parts;
            if (partModifiers == null)
                continue;

            for (int j = 0; j < partModifiers.Count; j++)
            {
                AnatomyGenePartModifier partModifier = partModifiers[j];
                if (partModifier?.part != part || partModifier.fluids == null)
                    continue;

                for (int k = 0; k < partModifier.fluids.Count; k++)
                {
                    AnatomyGeneFluidModifier fluidModifier = partModifier.fluids[k];
                    if (fluidModifier?.fluid != template.fluid)
                        continue;

                    multiplier *= Mathf.Max(0f, fluidModifier.refillRateMultiplier);
                    offset += fluidModifier.refillRateOffset;
                }
            }
        }

        return Mathf.Max(0f, (refillPerDay * multiplier) + offset);
    }

    internal static bool Matches(Pawn pawn, AnatomyGeneModifierDef def)
    {
        if (pawn == null || def == null)
            return false;

        return AnatomyResolver.MatchesGenes(def.geneDefs, pawn)
            && AnatomyResolver.MatchesThingDef(def.raceDefs, pawn.def)
            && AnatomyResolver.MatchesPawnKind(def.pawnKindDefs, pawn.kindDef)
            && AnatomyResolver.MatchesBodyType(def.bodyTypes, pawn.story?.bodyType)
            && AnatomyResolver.MatchesGender(def.genders, pawn.gender)
            && AnatomyResolver.MatchesLifeStage(def.lifeStages, pawn.ageTracker?.CurLifeStage);
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
