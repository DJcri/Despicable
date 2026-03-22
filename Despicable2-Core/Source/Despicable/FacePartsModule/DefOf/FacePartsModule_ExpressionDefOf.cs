using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace Despicable;
[DefOf]
public class FacePartsModule_ExpressionDefOf
{
    public static ExpressionDef FacialExpression_EyesClosed;
    public static ExpressionDef FacialExpression_Smirk;
    public static ExpressionDef FacialExpression_Smile;
    public static ExpressionDef FacialExpression_Distressed;
    public static ExpressionDef FacialExpression_Berserk;
    public static ExpressionDef FacialExpression_Drool;
    public static ExpressionDef FacialExpression_Tired;
    public static ExpressionDef FacialExpression_Drunk;
    public static ExpressionDef FacialExpression_Infant;
    public static ExpressionDef FacialExpression_Drafted;

    static FacePartsModule_ExpressionDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(FacePartsModule_ExpressionDefOf));
    }
}
