using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace Despicable;
[DefOf]
public class LovinModule_GenitalDefOf
{
    public static GenitalDef Genital_Penis;

    static LovinModule_GenitalDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(LovinModule_GenitalDefOf));
    }
}
