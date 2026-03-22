using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
public class AnatomyPartVariantDef : Def
{
    public AnatomyPartDef basePart;
    public List<HediffDef> hediffDefs;
    public List<GeneDef> geneDefs;
    public List<ThingDef> raceDefs;
    public List<PawnKindDef> pawnKindDefs;
    public List<BodyTypeDef> bodyTypes;
    public List<Gender> genders;
    public List<LifeStageDef> lifeStages;
    public float sizeMultiplier = 1f;
    public float sizeOffset;
    public List<AnatomyVariantFluidModifier> fluids;
    public int priority;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
            yield return error;

        if (basePart == null)
            yield return $"{defName} must define basePart.";

        bool hasSelector = (hediffDefs != null && hediffDefs.Count > 0)
            || (geneDefs != null && geneDefs.Count > 0)
            || (raceDefs != null && raceDefs.Count > 0)
            || (pawnKindDefs != null && pawnKindDefs.Count > 0)
            || (bodyTypes != null && bodyTypes.Count > 0)
            || (genders != null && genders.Count > 0)
            || (lifeStages != null && lifeStages.Count > 0);
        if (!hasSelector)
            yield return $"{defName} must define at least one selector (hediffDefs, geneDefs, raceDefs, pawnKindDefs, bodyTypes, genders, or lifeStages).";

        if (sizeMultiplier < 0f)
            yield return $"{defName} has sizeMultiplier < 0.";

        if (fluids == null)
            yield break;

        for (int i = 0; i < fluids.Count; i++)
        {
            AnatomyVariantFluidModifier fluidModifier = fluids[i];
            if (fluidModifier == null)
            {
                yield return $"{defName} has a null fluids entry.";
                continue;
            }

            if (fluidModifier.fluid == null)
                yield return $"{defName} fluids entry {i} must define a fluid.";

            if (fluidModifier.capacityMultiplier < 0f)
                yield return $"{defName} fluids entry {i} has capacityMultiplier < 0.";

            if (fluidModifier.initialAmountMultiplier < 0f)
                yield return $"{defName} fluids entry {i} has initialAmountMultiplier < 0.";

            if (fluidModifier.refillRateMultiplier < 0f)
                yield return $"{defName} fluids entry {i} has refillRateMultiplier < 0.";
        }
    }
}

public class AnatomyVariantFluidModifier
{
    public FluidDef fluid;
    public float capacityMultiplier = 1f;
    public float capacityOffset;
    public float initialAmountMultiplier = 1f;
    public float initialAmountOffset;
    public float refillRateMultiplier = 1f;
    public float refillRateOffset;
}
