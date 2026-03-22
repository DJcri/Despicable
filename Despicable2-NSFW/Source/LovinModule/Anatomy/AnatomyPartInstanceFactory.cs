using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable;
internal static class AnatomyPartInstanceFactory
{
    internal static AnatomyPartInstance Create(Pawn pawn, AnatomyPartDef part)
    {
        if (part == null)
            return null;

        AnatomyPartVariantDef variant = AnatomyVariantResolver.ResolveInstalledVariant(pawn, part);
        return new AnatomyPartInstance(part, variant, GenerateInitialSize(pawn, part, variant), GenerateInitialFluids(pawn, part, variant));
    }

    internal static float GenerateInitialSize(Pawn pawn, AnatomyPartDef part)
    {
        return GenerateInitialSize(pawn, part, AnatomyVariantResolver.ResolveInstalledVariant(pawn, part));
    }

    internal static float GenerateInitialSize(Pawn pawn, AnatomyPartDef part, AnatomyPartVariantDef variant)
    {
        if (part == null)
            return 1f;

        float baseValue = part.baseSize;
        float min = part.minSize;
        float max = part.maxSize;
        AnatomyVariantResolver.ApplySizeGeneration(variant, ref baseValue, ref min, ref max);
        AnatomyGeneModifierResolver.ApplySizeGenerationModifiers(pawn, part, ref baseValue, ref min, ref max);
        if (max < min)
        {
            float swap = min;
            min = max;
            max = swap;
        }

        if (Mathf.Approximately(min, max) || pawn == null)
            return Mathf.Clamp(baseValue, min, max);

        int seed = CombineStableHash(pawn.thingIDNumber, StableHash(part.defName));
        Rand.PushState(seed);
        float generated = Rand.Range(min, max);
        Rand.PopState();
        return Mathf.Clamp(generated, min, max);
    }

    internal static List<AnatomyFluidInstance> GenerateInitialFluids(Pawn pawn, AnatomyPartDef part)
    {
        return GenerateInitialFluids(pawn, part, AnatomyVariantResolver.ResolveInstalledVariant(pawn, part));
    }

    internal static List<AnatomyFluidInstance> GenerateInitialFluids(Pawn pawn, AnatomyPartDef part, AnatomyPartVariantDef variant)
    {
        List<AnatomyFluidInstance> result = new List<AnatomyFluidInstance>();
        if (part?.fluidTemplates == null || part.fluidTemplates.Count == 0)
            return result;

        for (int i = 0; i < part.fluidTemplates.Count; i++)
        {
            AnatomyFluidTemplate template = part.fluidTemplates[i];
            AnatomyFluidInstance generated = GenerateInitialFluid(pawn, part, template, variant);
            if (generated != null)
                result.Add(generated);
        }

        return result;
    }

    internal static AnatomyFluidInstance GenerateInitialFluid(Pawn pawn, AnatomyPartDef part, AnatomyFluidTemplate template)
    {
        return GenerateInitialFluid(pawn, part, template, AnatomyVariantResolver.ResolveInstalledVariant(pawn, part));
    }

    internal static AnatomyFluidInstance GenerateInitialFluid(Pawn pawn, AnatomyPartDef part, AnatomyFluidTemplate template, AnatomyPartVariantDef variant)
    {
        if (part == null || template?.fluid == null)
            return null;

        float baseCapacity = template.baseCapacity;
        float minCapacity = template.minCapacity;
        float maxCapacity = template.maxCapacity;
        float initialAmountMultiplier = 1f;
        float initialAmountOffset = 0f;
        AnatomyVariantResolver.ApplyFluidGeneration(variant, template, ref baseCapacity, ref minCapacity, ref maxCapacity, ref initialAmountMultiplier, ref initialAmountOffset);
        AnatomyGeneModifierResolver.ApplyFluidGenerationModifiers(pawn, part, template, ref baseCapacity, ref minCapacity, ref maxCapacity, ref initialAmountMultiplier, ref initialAmountOffset);
        float capacity = GenerateInitialCapacity(pawn, part, template, variant);
        float fillPercent = Mathf.Clamp01(template.initialFillPercent);
        float amount = Mathf.Clamp((capacity * fillPercent * initialAmountMultiplier) + initialAmountOffset, 0f, capacity);
        return new AnatomyFluidInstance(template.fluid, capacity, amount);
    }

    internal static float GenerateInitialCapacity(Pawn pawn, AnatomyPartDef part, AnatomyFluidTemplate template)
    {
        return GenerateInitialCapacity(pawn, part, template, AnatomyVariantResolver.ResolveInstalledVariant(pawn, part));
    }

    internal static float GenerateInitialCapacity(Pawn pawn, AnatomyPartDef part, AnatomyFluidTemplate template, AnatomyPartVariantDef variant)
    {
        if (part == null || template == null)
            return 0f;

        float baseValue = template.baseCapacity;
        float min = template.minCapacity;
        float max = template.maxCapacity;
        float initialAmountMultiplier = 1f;
        float initialAmountOffset = 0f;
        AnatomyVariantResolver.ApplyFluidGeneration(variant, template, ref baseValue, ref min, ref max, ref initialAmountMultiplier, ref initialAmountOffset);
        AnatomyGeneModifierResolver.ApplyFluidGenerationModifiers(pawn, part, template, ref baseValue, ref min, ref max, ref initialAmountMultiplier, ref initialAmountOffset);
        if (max < min)
        {
            float swap = min;
            min = max;
            max = swap;
        }

        if (Mathf.Approximately(min, max) || pawn == null)
            return Mathf.Clamp(baseValue, min, max);

        int seed = CombineStableHash(CombineStableHash(pawn.thingIDNumber, StableHash(part.defName)), StableHash(template.fluid.defName));
        Rand.PushState(seed);
        float generated = Rand.Range(min, max);
        Rand.PopState();
        return Mathf.Clamp(generated, min, max);
    }

    internal static int CombineStableHash(int left, int right)
    {
        unchecked
        {
            return (left * 397) ^ right;
        }
    }

    internal static int StableHash(string value)
    {
        if (value.NullOrEmpty())
            return 0;

        unchecked
        {
            int hash = 23;
            for (int i = 0; i < value.Length; i++)
                hash = (hash * 31) + value[i];
            return hash;
        }
    }
}
