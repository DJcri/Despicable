using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
internal static class AnatomyQuery
{
    private const string GenderWorksReproductionPartDefName = "SEX_Reproduction";
    private const string PelvisPartDefName = "Pelvis";
    private const string TorsoPartDefName = "Torso";
    private const string ChestPartDefName = "Chest";

    internal static bool TryGetExternalGenitals(Pawn pawn, out BodyPartRecord part)
    {
        return TryGetStableAnatomyAnchor(pawn, out part);
    }

    internal static bool TryGetAnchor(Pawn pawn, AnatomySlotDef slot, out BodyPartRecord part)
    {
        return AnatomyAnchorResolver.TryGetAnchor(pawn, slot, out part);
    }

    internal static bool TryGetResolvedPartInstances(Pawn pawn, out List<AnatomyPartInstance> instances)
    {
        instances = null;

        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return false;

        CompAnatomyBootstrap tracker = pawn.TryGetComp<CompAnatomyBootstrap>();
        if (tracker != null)
        {
            if (tracker.TryGetResolvedPartInstances(out instances))
            {
                RemoveMissingResolvedPartInstances(pawn, instances);
                return true;
            }

            AnatomyBootstrapper.TryResolveAndApply(pawn);
            if (tracker.TryGetResolvedPartInstances(out instances))
            {
                RemoveMissingResolvedPartInstances(pawn, instances);
                return true;
            }
        }

        if (TryGetFallbackResolvedPartInstances(pawn, out instances))
            return true;

        instances = null;
        return false;
    }

    internal static bool TryGetResolvedParts(Pawn pawn, out List<AnatomyPartDef> parts)
    {
        parts = null;
        if (!TryGetResolvedPartInstances(pawn, out List<AnatomyPartInstance> instances) || instances == null)
            return false;

        parts = new List<AnatomyPartDef>(instances.Count);
        for (int i = 0; i < instances.Count; i++)
        {
            AnatomyPartDef part = instances[i]?.partDef;
            if (part != null)
                parts.Add(part);
        }

        return true;
    }

    internal static bool TryGetPartInstance(Pawn pawn, AnatomyPartDef part, out AnatomyPartInstance instance)
    {
        instance = null;
        if (part == null)
            return false;

        if (!TryGetResolvedPartInstances(pawn, out List<AnatomyPartInstance> instances) || instances == null)
            return false;

        for (int i = 0; i < instances.Count; i++)
        {
            AnatomyPartInstance current = instances[i];
            if (current?.partDef == part)
            {
                instance = current;
                return true;
            }
        }

        return false;
    }

    internal static AnatomyPartVariantDef GetInstalledVariant(Pawn pawn, AnatomyPartDef part)
    {
        if (part == null)
            return null;

        if (TryGetPartInstance(pawn, part, out AnatomyPartInstance instance) && instance != null)
            return instance.installedVariant;

        return AnatomyVariantResolver.ResolveInstalledVariant(pawn, part);
    }

    internal static float GetPartSize(Pawn pawn, AnatomyPartDef part, float fallback = 1f)
    {
        if (TryGetPartInstance(pawn, part, out AnatomyPartInstance instance) && instance != null)
            return instance.size;

        if (part != null)
            return AnatomyPartInstanceFactory.GenerateInitialSize(pawn, part);

        return fallback;
    }

    internal static bool TryGetFluidInstance(Pawn pawn, AnatomyPartDef part, FluidDef fluid, out AnatomyFluidInstance fluidInstance)
    {
        fluidInstance = null;
        if (!TryGetPartInstance(pawn, part, out AnatomyPartInstance instance) || instance == null)
            return false;

        return instance.TryGetFluid(fluid, out fluidInstance);
    }

    internal static float GetPartFluidCapacity(Pawn pawn, AnatomyPartDef part, float fallback = 0f)
    {
        if (TryGetPartInstance(pawn, part, out AnatomyPartInstance instance) && instance != null)
            return instance.GetTotalFluidCapacity();

        if (part?.fluidTemplates != null && part.fluidTemplates.Count > 0)
        {
            float total = 0f;
            for (int i = 0; i < part.fluidTemplates.Count; i++)
            {
                AnatomyFluidTemplate template = part.fluidTemplates[i];
                AnatomyFluidInstance generated = AnatomyPartInstanceFactory.GenerateInitialFluid(pawn, part, template);
                if (generated != null)
                    total += generated.capacity;
            }

            return total;
        }

        return fallback;
    }

    internal static float GetFluidCapacity(Pawn pawn, AnatomyPartDef part, FluidDef fluid, float fallback = 0f)
    {
        if (TryGetFluidInstance(pawn, part, fluid, out AnatomyFluidInstance fluidInstance) && fluidInstance != null)
            return fluidInstance.capacity;

        if (part?.fluidTemplates != null && fluid != null)
        {
            for (int i = 0; i < part.fluidTemplates.Count; i++)
            {
                AnatomyFluidTemplate template = part.fluidTemplates[i];
                if (template?.fluid != fluid)
                    continue;

                AnatomyFluidInstance generated = AnatomyPartInstanceFactory.GenerateInitialFluid(pawn, part, template);
                if (generated != null)
                    return generated.capacity;
            }
        }

        return fallback;
    }

    internal static float GetFluidAmount(Pawn pawn, AnatomyPartDef part, FluidDef fluid, float fallback = 0f)
    {
        if (TryGetFluidInstance(pawn, part, fluid, out AnatomyFluidInstance fluidInstance) && fluidInstance != null)
            return fluidInstance.amount;

        if (part?.fluidTemplates != null && fluid != null)
        {
            for (int i = 0; i < part.fluidTemplates.Count; i++)
            {
                AnatomyFluidTemplate template = part.fluidTemplates[i];
                if (template?.fluid != fluid)
                    continue;

                AnatomyFluidInstance generated = AnatomyPartInstanceFactory.GenerateInitialFluid(pawn, part, template);
                if (generated != null)
                    return generated.amount;
            }
        }

        return fallback;
    }

    internal static float GetFluidRefillPerDay(Pawn pawn, AnatomyPartDef part, FluidDef fluid, float fallback = 0f)
    {
        if (part == null || fluid == null)
            return fallback;

        float refillPerDay = AnatomyFluidRuntime.GetFluidRefillPerDay(pawn, part, fluid);
        return refillPerDay > 0f ? refillPerDay : fallback;
    }

    internal static bool HasFluidType(Pawn pawn, AnatomyPartDef part, FluidDef fluid)
    {
        return TryGetFluidInstance(pawn, part, fluid, out _);
    }

    internal static bool HasPart(Pawn pawn, AnatomyPartDef part)
    {
        if (part == null)
            return false;

        if (!TryGetResolvedPartInstances(pawn, out List<AnatomyPartInstance> instances) || instances == null)
            return false;

        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i]?.partDef == part)
                return true;
        }

        return false;
    }

    internal static bool HasPartTag(Pawn pawn, string tag)
    {
        if (tag.NullOrEmpty())
            return false;

        if (!TryGetResolvedPartInstances(pawn, out List<AnatomyPartInstance> instances) || instances == null)
            return false;

        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i]?.partDef?.HasTag(tag) == true)
                return true;
        }

        return false;
    }

    internal static bool HasCapability(Pawn pawn, string capability)
    {
        if (capability.NullOrEmpty())
            return false;

        if (!TryGetResolvedPartInstances(pawn, out List<AnatomyPartInstance> instances) || instances == null)
            return false;

        for (int i = 0; i < instances.Count; i++)
        {
            if (instances[i]?.partDef?.HasCapability(capability) == true)
                return true;
        }

        return false;
    }

    internal static bool TryGetLegacyExternalGenitals(Pawn pawn, out BodyPartRecord part)
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

    internal static bool TryGetStableAnatomyAnchor(Pawn pawn, out BodyPartRecord part)
    {
        if (TryGetGenderWorksReproductionPart(pawn, out part))
            return true;

        if (TryGetPelvis(pawn, out part))
            return true;

        return TryGetLegacyExternalGenitals(pawn, out part);
    }

    internal static bool TryGetGenderWorksReproductionPart(Pawn pawn, out BodyPartRecord part)
    {
        return TryGetBodyPartByDefName(pawn, GenderWorksReproductionPartDefName, out part);
    }

    internal static bool TryGetPelvis(Pawn pawn, out BodyPartRecord part)
    {
        return TryGetBodyPartByDefName(pawn, PelvisPartDefName, out part);
    }

    internal static bool TryGetTorso(Pawn pawn, out BodyPartRecord part)
    {
        if (TryGetBodyPartByDefName(pawn, ChestPartDefName, out part))
            return true;

        return TryGetBodyPartByDefName(pawn, TorsoPartDefName, out part);
    }

    internal static bool HasExternalGenitalsSlot(Pawn pawn)
    {
        return TryGetStableAnatomyAnchor(pawn, out _);
    }

    internal static bool IsExternalGenitalsMissing(Pawn pawn)
    {
        if (pawn?.health?.hediffSet == null)
            return false;

        if (TryGetLegacyExternalGenitals(pawn, out BodyPartRecord legacyPart))
            return pawn.health.hediffSet.PartIsMissing(legacyPart);

        if (!TryGetStableAnatomyAnchor(pawn, out BodyPartRecord anchorPart))
            return false;

        return pawn.health.hediffSet.PartIsMissing(anchorPart);
    }

    internal static bool IsPartMissing(Pawn pawn, AnatomyPartDef part)
    {
        if (pawn?.health?.hediffSet == null || part?.slot == null)
            return false;

        if (part.slot.defName == "ExternalGenitals" && TryGetLegacyExternalGenitals(pawn, out BodyPartRecord legacyPart))
            return pawn.health.hediffSet.PartIsMissing(legacyPart);

        if (!TryGetAnchor(pawn, part.slot, out BodyPartRecord anchorPart))
            return false;

        return pawn.health.hediffSet.PartIsMissing(anchorPart);
    }

    internal static void RemoveMissingResolvedParts(Pawn pawn, List<AnatomyPartDef> parts)
    {
        if (parts == null || parts.Count == 0)
            return;

        for (int i = parts.Count - 1; i >= 0; i--)
        {
            if (IsPartMissing(pawn, parts[i]))
                parts.RemoveAt(i);
        }
    }

    internal static void RemoveMissingResolvedPartInstances(Pawn pawn, List<AnatomyPartInstance> instances)
    {
        if (instances == null || instances.Count == 0)
            return;

        for (int i = instances.Count - 1; i >= 0; i--)
        {
            AnatomyPartDef part = instances[i]?.partDef;
            if (part == null || IsPartMissing(pawn, part))
                instances.RemoveAt(i);
        }
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
        return HasPartTag(pawn, "Penis");
    }

    internal static bool HasVagina(Pawn pawn)
    {
        return HasPartTag(pawn, "Vagina");
    }

    internal static bool TryGetLogicalAnatomy(Pawn pawn, out bool hasPenis, out bool hasVagina)
    {
        hasPenis = false;
        hasVagina = false;

        if (!TryGetResolvedPartInstances(pawn, out List<AnatomyPartInstance> instances) || instances == null)
            return false;

        for (int i = 0; i < instances.Count; i++)
        {
            AnatomyPartDef part = instances[i]?.partDef;
            if (part?.HasTag("Penis") == true)
                hasPenis = true;

            if (part?.HasTag("Vagina") == true)
                hasVagina = true;
        }

        return true;
    }

    internal static bool HasLegacyPartHediff(Pawn pawn, HediffDef def)
    {
        if (pawn?.health?.hediffSet?.hediffs == null || def == null)
            return false;

        if (IsExternalGenitalsMissing(pawn))
            return false;

        if (!TryGetLegacyExternalGenitals(pawn, out BodyPartRecord part))
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

    private static bool TryGetFallbackResolvedParts(Pawn pawn, out List<AnatomyPartDef> parts)
    {
        parts = null;
        if (!AnatomyResolver.TryResolveDesiredParts(pawn, out List<AnatomyPartDef> resolved) || resolved == null)
            return false;

        parts = resolved;
        RemoveMissingResolvedParts(pawn, parts);
        return true;
    }

    private static bool TryGetFallbackResolvedPartInstances(Pawn pawn, out List<AnatomyPartInstance> instances)
    {
        instances = null;
        if (!TryGetFallbackResolvedParts(pawn, out List<AnatomyPartDef> parts) || parts == null)
            return false;

        instances = new List<AnatomyPartInstance>(parts.Count);
        for (int i = 0; i < parts.Count; i++)
        {
            AnatomyPartDef part = parts[i];
            if (part != null)
                instances.Add(AnatomyPartInstanceFactory.Create(pawn, part));
        }

        return true;
    }

    private static bool TryGetBodyPartByDefName(Pawn pawn, string defName, out BodyPartRecord part)
    {
        part = null;

        if (pawn?.RaceProps?.body?.AllParts == null || string.IsNullOrEmpty(defName))
            return false;

        List<BodyPartRecord> parts = pawn.RaceProps.body.AllParts;
        for (int i = 0; i < parts.Count; i++)
        {
            BodyPartRecord candidate = parts[i];
            if (candidate?.def?.defName == defName)
            {
                part = candidate;
                return true;
            }
        }

        return false;
    }
}
