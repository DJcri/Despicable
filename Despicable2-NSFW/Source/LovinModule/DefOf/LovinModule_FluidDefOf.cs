using RimWorld;

namespace Despicable
{
    [DefOf]
    public static class LovinModule_FluidDefOf
    {
        public static FluidDef Fluid_Milk;
        public static FluidDef Fluid_Semen;

        static LovinModule_FluidDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(LovinModule_FluidDefOf));
        }
    }
}
