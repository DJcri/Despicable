using RimWorld;
using Verse;

namespace Despicable;
[DefOf]
public static class LovinModule_AnatomyDefOf
{
    public static BodyPartDef D2_ExternalGenitals;
    public static BodyPartGroupDef D2_Genitals;

    public static HediffDef D2_Genital_Penis;
    public static HediffDef D2_Genital_Vagina;

    static LovinModule_AnatomyDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(LovinModule_AnatomyDefOf));
    }
}
