using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
[DefOf]
public class LovinModule_PawnRenderNodeTagDefOf
{
    public static PawnRenderNodeTagDef Genitals;

    static LovinModule_PawnRenderNodeTagDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(LovinModule_PawnRenderNodeTagDefOf));
    }
}
