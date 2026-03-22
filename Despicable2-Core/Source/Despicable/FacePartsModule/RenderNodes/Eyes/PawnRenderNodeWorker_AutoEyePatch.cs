using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderNodeWorker_AutoEyePatch : PawnRenderNodeWorker_FacePart
{
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (!base.CanDrawNow(node, parms))
            return false;

        Pawn pawn = node?.tree?.pawn;
        if (pawn == null)
            return false;

        if (IsRightCounterpartNode(node)
            && parms.facing != Rot4.South)
        {
            return false;
        }

        if (parms.facing == Rot4.North)
            return false;

        CompFaceParts compFaceParts = ResolveCompFaceParts(node, pawn);
        if (compFaceParts?.HasBlockingEyeGeneThisTick() == true)
            return false;

        if (compFaceParts?.IsForeignEyeVisualBlockedForNodeThisTick(node, parms.facing) == true)
            return false;

        if (!AutoEyePatchRuntime.TryResolveEyeBaseReplacement(pawn, node, parms.facing, out AutoEyePatchRenderSelection selection))
            return false;

        return selection.SuppressLegacy && selection.RuntimeTexture != null;
    }

    public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
    {
        return base.OffsetFor(node, parms, out pivot);
    }

    public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
    {
        return base.ScaleFor(node, parms);
    }
}
