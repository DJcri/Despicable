using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;

namespace Despicable;
[DefOf]
public class LovinModule_FacialAnimDefOf
{
    public static FacialAnimDef FacialAnim_Gasp;
    public static FacialAnimDef FacialAnim_Moan;
    public static FacialAnimDef FacialAnim_Orgasm;
    public static FacialAnimDef FacialAnim_LipBite;
    public static FacialAnimDef FacialAnim_Cheekful;

    static LovinModule_FacialAnimDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(LovinModule_FacialAnimDefOf));
    }
}
