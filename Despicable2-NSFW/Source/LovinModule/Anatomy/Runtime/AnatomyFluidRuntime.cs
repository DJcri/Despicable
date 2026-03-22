using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable;
internal static class AnatomyFluidRuntime
{
    private const float RareTicksPerDay = 240f;

    internal static void TickRare(Pawn pawn, CompAnatomyBootstrap tracker)
    {
        if (pawn == null || tracker == null || pawn.Dead)
            return;

        if (!tracker.TryGetResolvedPartInstances(out List<AnatomyPartInstance> instances) || instances == null || instances.Count == 0)
            return;

        for (int i = 0; i < instances.Count; i++)
            TickRareForPart(pawn, instances[i]);
    }

    internal static float GetFluidRefillPerDay(Pawn pawn, AnatomyPartDef part, FluidDef fluid)
    {
        AnatomyFluidTemplate template = FindTemplate(part, fluid);
        if (template == null)
            return 0f;

        AnatomyPartVariantDef variant = AnatomyQuery.GetInstalledVariant(pawn, part);
        float refillPerDay = AnatomyVariantResolver.ApplyFluidRefillPerDay(variant, template, template.refillPerDay);
        return AnatomyGeneModifierResolver.ApplyFluidRefillModifiers(pawn, part, template, refillPerDay);
    }

    private static void TickRareForPart(Pawn pawn, AnatomyPartInstance instance)
    {
        AnatomyPartDef part = instance?.partDef;
        if (part == null || instance.fluids == null || instance.fluids.Count == 0)
            return;

        for (int i = 0; i < instance.fluids.Count; i++)
        {
            AnatomyFluidInstance fluid = instance.fluids[i];
            if (fluid?.fluidDef == null)
                continue;

            AnatomyFluidTemplate template = FindTemplate(part, fluid.fluidDef);
            if (template == null)
                continue;

            AnatomyPartVariantDef variant = instance.installedVariant ?? AnatomyVariantResolver.ResolveInstalledVariant(pawn, part);
            float refillPerDay = AnatomyVariantResolver.ApplyFluidRefillPerDay(variant, template, template.refillPerDay);
            refillPerDay = AnatomyGeneModifierResolver.ApplyFluidRefillModifiers(pawn, part, template, refillPerDay);
            if (refillPerDay <= 0f)
                continue;

            float delta = refillPerDay / RareTicksPerDay;
            if (delta <= 0f)
                continue;

            fluid.amount = Mathf.Clamp(fluid.amount + delta, 0f, fluid.capacity);
        }
    }

    private static AnatomyFluidTemplate FindTemplate(AnatomyPartDef part, FluidDef fluid)
    {
        if (part?.fluidTemplates == null || fluid == null)
            return null;

        for (int i = 0; i < part.fluidTemplates.Count; i++)
        {
            AnatomyFluidTemplate template = part.fluidTemplates[i];
            if (template?.fluid == fluid)
                return template;
        }

        return null;
    }
}
