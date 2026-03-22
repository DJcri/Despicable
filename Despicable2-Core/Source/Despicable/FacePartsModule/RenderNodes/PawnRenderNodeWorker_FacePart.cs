using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderNodeWorker_FacePart : PawnRenderNodeWorker_FlipWhenCrawling
{
    protected static CompFaceParts ResolveCompFaceParts(PawnRenderNode node, Pawn pawn)
    {
        return FacePartRenderNodeContextCache.ResolveCompFaceParts(node, pawn);
    }

    protected static string GetDebugLabel(PawnRenderNode node)
    {
        return FacePartRenderNodeContextCache.GetDebugLabel(node) ?? node?.Props?.debugLabel;
    }

    protected static bool IsRightCounterpartNode(PawnRenderNode node)
    {
        return FacePartRenderNodeContextCache.IsRightCounterpartNode(node);
    }

    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        CompFaceParts facePartsComp = ResolveCompFaceParts(node, parms.pawn);
        if (facePartsComp?.IsRenderActiveNow() != true)
            return false;

        if (base.CanDrawNow(node, parms))
        {
            // Don't render face parts if dessicated
            if (parms.pawn.IsDessicated())
                return false;

            return true;
        }

        return false;
    }

    public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
    {
        CompFaceParts facePartsComp = ResolveCompFaceParts(node, parms.pawn);
        Vector3 baseOffset = base.OffsetFor(node, parms, out pivot);
        ExpressionDef expressionDef = facePartsComp?.GetRenderExpressionForParms(parms);
        Vector3? offset = FacePartRenderNodeContextCache.GetExpressionOffset(expressionDef, node);
        if (offset != null)
            baseOffset += offset.Value;

        return baseOffset;
    }
}
