using RimWorld;
using Verse;

namespace Despicable;
[DefOf]
public static class LovinModule_ThoughtDefOf
{
    public static ThoughtDef D2N_Thought_SelfLovin;

    static LovinModule_ThoughtDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(LovinModule_ThoughtDefOf));
    }
}
