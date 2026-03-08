using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderNodeWorker_EyeAddon : PawnRenderNodeWorker_FacePart
{
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (base.CanDrawNow(node, parms))
        {
            // Don't render expression texPath if portrait, use face style instead
            Pawn pawn = node.tree.pawn;

            // Don't render right-counterpart when facing west, as the mirrored textures already flip automatically!
            if (!node.Props.debugLabel.NullOrEmpty()
                && node.Props.debugLabel.EndsWith("_R", StringComparison.OrdinalIgnoreCase)
                && parms.facing != Rot4.South)
            {
                return false;
            }

            // Don't render face parts if facing north
            if (parms.facing == Rot4.North)
            {
                return false;
            }

            // Check for eye-shaping genes when the gene tracker exists.
            // Without Biotech, or on pawns that simply do not have genes initialized,
            // treat this as "no interfering genes" rather than a hard failure.
            if (pawn?.TryGetComp<CompFaceParts>()?.HasBlockingEyeGeneThisTick() == true)
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
            eyeOffset = headType?.eyeOffsetEastWest.Value.x ?? eyeOffset;

        Vector3 side = Vector3.zero;
        if (parms.facing == Rot4.East)
        {
            side = Vector3.right;
        }
        else if (parms.facing == Rot4.West)
        {
            side = Vector3.left;
        }

        if (parms.facing == Rot4.South)
        {
            if (node.Props.flipGraphic)
            {
                vector.x += 0.09f * (parms.pawn.ageTracker.CurLifeStage.eyeSizeFactor ?? 1f);
            }
            else
            {
                vector.x -= 0.09f * (parms.pawn.ageTracker.CurLifeStage.eyeSizeFactor ?? 1f);
            }
        }
        else
        {
            vector += side * (eyeOffset * (parms.pawn.ageTracker.CurLifeStage.eyeSizeFactor ?? 1f));
        }

        vector *= parms.pawn.ageTracker.CurLifeStage.eyeSizeFactor ?? 1f;
        return vector;
    }

    public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
    {
        return base.ScaleFor(node, parms);
    }
}
