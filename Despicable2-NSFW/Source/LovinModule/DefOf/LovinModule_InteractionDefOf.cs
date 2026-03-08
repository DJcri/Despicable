using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Despicable;
[DefOf]
public class LovinModule_InteractionDefOf
{
    public static InteractionDef Interaction_VaginalSex;
    public static InteractionDef Interaction_OralSex;
    public static InteractionDef Interaction_AnalSex;

    static LovinModule_InteractionDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(LovinModule_InteractionDefOf));
    }
}
