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
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        CompFaceParts facePartsComp = parms.pawn.TryGetComp<CompFaceParts>();
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
        CompFaceParts facePartsComp = parms.pawn.TryGetComp<CompFaceParts>();
        ExpressionDef expressionDef = facePartsComp.animExpression ?? facePartsComp.baseExpression;
        Vector3? offset = expressionDef?.getOffset(node.Props.debugLabel);

        if (offset != null)
        {
            return (Vector3)(base.OffsetFor(node, parms, out pivot) + offset);
        }

        return base.OffsetFor(node, parms, out pivot);
    }
}
