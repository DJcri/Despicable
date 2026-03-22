using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderNodeWorker_Mouth : PawnRenderNodeWorker_FacePart
{
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (base.CanDrawNow(node, parms))
        {
            Pawn pawn = parms.pawn;

            if (pawn.style?.beardDef != BeardDefOf.NoBeard)
            {
                return false;
            }

            // Don't render north for performance
            if (!(parms.facing != Rot4.North))
            {
                return parms.flipHead;
            }

            CompFaceParts compFaceParts = ResolveCompFaceParts(node, pawn);
            if (compFaceParts?.HasBlockingNoseGeneThisTick() == true)
                return false;

            return true;
        }
        return false;
    }

    public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
    {
        Pawn pawn = parms.pawn;
        HeadTypeDef headType = pawn.story.headType;
        Vector3 vector = base.OffsetFor(node, parms, out pivot);

        float eyeOffset = 0.13f;
        if (headType.eyeOffsetEastWest.HasValue)
            eyeOffset = headType.eyeOffsetEastWest.Value.x;

        Vector3 side = Vector3.zero;
        if (parms.facing == Rot4.East)
        {
            side = Vector3.right;
        }
        else if (parms.facing == Rot4.West)
        {
            side = Vector3.left;
        }

        float eyeSizeFactor = pawn.ageTracker.CurLifeStage.eyeSizeFactor ?? 1f;
        vector += side * (eyeOffset * eyeSizeFactor);
        vector *= eyeSizeFactor;

        return vector;
    }
}
