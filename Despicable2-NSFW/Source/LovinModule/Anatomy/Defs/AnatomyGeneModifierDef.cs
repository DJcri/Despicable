using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
public class AnatomyGeneModifierDef : Def
{
    public List<GeneDef> geneDefs;
    public List<ThingDef> raceDefs;
    public List<PawnKindDef> pawnKindDefs;
    public List<BodyTypeDef> bodyTypes;
    public List<Gender> genders;
    public List<LifeStageDef> lifeStages;
    public List<AnatomyGenePartModifier> parts;
    public int priority;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
            yield return error;

        if (geneDefs == null || geneDefs.Count == 0)
            yield return $"{defName} must define at least one geneDefs entry.";

        if (parts == null || parts.Count == 0)
            yield return $"{defName} must define at least one anatomy gene part modifier.";

        if (parts == null)
            yield break;

        for (int i = 0; i < parts.Count; i++)
        {
            AnatomyGenePartModifier partModifier = parts[i];
            if (partModifier == null)
            {
                yield return $"{defName} has a null parts entry.";
                continue;
            }

            if (partModifier.part == null)
                yield return $"{defName} part modifier {i} must define a part.";

            if (partModifier.sizeMultiplier < 0f)
                yield return $"{defName} part modifier {i} has sizeMultiplier < 0.";

            if (partModifier.fluids == null)
                continue;

            for (int j = 0; j < partModifier.fluids.Count; j++)
            {
                AnatomyGeneFluidModifier fluidModifier = partModifier.fluids[j];
                if (fluidModifier == null)
                {
                    yield return $"{defName} part modifier {i} has a null fluids entry.";
                    continue;
                }

                if (fluidModifier.fluid == null)
                    yield return $"{defName} part modifier {i} fluid modifier {j} must define a fluid.";

                if (fluidModifier.capacityMultiplier < 0f)
                    yield return $"{defName} part modifier {i} fluid modifier {j} has capacityMultiplier < 0.";

                if (fluidModifier.initialAmountMultiplier < 0f)
                    yield return $"{defName} part modifier {i} fluid modifier {j} has initialAmountMultiplier < 0.";

                if (fluidModifier.refillRateMultiplier < 0f)
                    yield return $"{defName} part modifier {i} fluid modifier {j} has refillRateMultiplier < 0.";
            }
        }
    }
}

public class AnatomyGenePartModifier
{
    public AnatomyPartDef part;
    public float sizeMultiplier = 1f;
    public float sizeOffset;
    public List<AnatomyGeneFluidModifier> fluids;
}

public class AnatomyGeneFluidModifier
{
    public FluidDef fluid;
    public float capacityMultiplier = 1f;
    public float capacityOffset;
    public float initialAmountMultiplier = 1f;
    public float initialAmountOffset;
    public float refillRateMultiplier = 1f;
    public float refillRateOffset;
}
