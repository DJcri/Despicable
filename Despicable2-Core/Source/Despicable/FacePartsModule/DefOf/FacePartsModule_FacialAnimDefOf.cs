using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;

namespace Despicable;
[DefOf]
public class FacePartsModule_FacialAnimDefOf
{
    public static FacialAnimDef FacialAnim_Blink;
    public static FacialAnimDef FacialAnim_Drool;

    static FacePartsModule_FacialAnimDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(FacePartsModule_FacialAnimDefOf));
    }
}
