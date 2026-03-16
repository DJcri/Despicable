using RimWorld;
using Verse;

namespace Despicable;
[DefOf]
public class LovinModule_GenitalDefOf
{
    public static GenitalDef Genital_Penis;
    public static GenitalDef Genital_Vagina;

    static LovinModule_GenitalDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(LovinModule_GenitalDefOf));
    }
}
