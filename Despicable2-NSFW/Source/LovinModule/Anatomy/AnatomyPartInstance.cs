using System.Collections.Generic;
using Verse;

namespace Despicable;
public class AnatomyPartInstance : IExposable
{
    public AnatomyPartDef partDef;
    public AnatomyPartVariantDef installedVariant;
    public float size = 1f;
    public List<AnatomyFluidInstance> fluids;

    private float legacyFluidCapacity;

    public AnatomyPartInstance()
    {
    }

    public AnatomyPartInstance(AnatomyPartDef partDef, AnatomyPartVariantDef installedVariant, float size, List<AnatomyFluidInstance> fluids)
    {
        this.partDef = partDef;
        this.installedVariant = installedVariant;
        this.size = size;
        this.fluids = fluids ?? new List<AnatomyFluidInstance>();
    }

    internal float LegacyFluidCapacity => legacyFluidCapacity;

    internal void ClearLegacyFluidCapacity()
    {
        legacyFluidCapacity = 0f;
    }

    public bool TryGetFluid(FluidDef fluid, out AnatomyFluidInstance instance)
    {
        instance = null;
        if (fluid == null || fluids == null)
            return false;

        for (int i = 0; i < fluids.Count; i++)
        {
            AnatomyFluidInstance current = fluids[i];
            if (current?.fluidDef == fluid)
            {
                instance = current;
                return true;
            }
        }

        return false;
    }

    public float GetTotalFluidCapacity()
    {
        if (fluids == null || fluids.Count == 0)
            return 0f;

        float total = 0f;
        for (int i = 0; i < fluids.Count; i++)
        {
            AnatomyFluidInstance fluid = fluids[i];
            if (fluid != null)
                total += fluid.capacity;
        }

        return total;
    }

    public float GetTotalFluidAmount()
    {
        if (fluids == null || fluids.Count == 0)
            return 0f;

        float total = 0f;
        for (int i = 0; i < fluids.Count; i++)
        {
            AnatomyFluidInstance fluid = fluids[i];
            if (fluid != null)
                total += fluid.amount;
        }

        return total;
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref partDef, "partDef");
        Scribe_Defs.Look(ref installedVariant, "installedVariant");
        Scribe_Values.Look(ref size, "size", 1f);
        Scribe_Collections.Look(ref fluids, "fluids", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
            Scribe_Values.Look(ref legacyFluidCapacity, "fluidCapacity", 0f);
    }
}
