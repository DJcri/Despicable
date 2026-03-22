using Verse;

namespace Despicable;
public class AnatomyFluidInstance : IExposable
{
    public FluidDef fluidDef;
    public float capacity;
    public float amount;

    public AnatomyFluidInstance()
    {
    }

    public AnatomyFluidInstance(FluidDef fluidDef, float capacity, float amount)
    {
        this.fluidDef = fluidDef;
        this.capacity = capacity;
        this.amount = amount;
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref fluidDef, "fluidDef");
        Scribe_Values.Look(ref capacity, "capacity", 0f);
        Scribe_Values.Look(ref amount, "amount", 0f);
    }
}
