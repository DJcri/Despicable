using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace Despicable;
[DefOf]
public class LovinModule_JobDefOf
{
    public static JobDef Job_GetLovin;
    public static JobDef Job_GetBedLovin;
    public static JobDef Job_GiveLovin;
    public static JobDef Job_SelfLovin;

    static LovinModule_JobDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(LovinModule_JobDefOf));
    }
}
