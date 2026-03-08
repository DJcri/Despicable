using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderSubWorker_ChangeOffset : PawnRenderSubWorker
{
    public override void TransformOffset(PawnRenderNode node, PawnDrawParms parms, ref Vector3 offset, ref Vector3 pivot)
    {
        if (parms.Portrait)
            return;

        if (node.AnimationWorker is AnimationWorker_ExtendedKeyframes
            && node.tree.pawn.TryGetComp<CompExtendedAnimator>(out CompExtendedAnimator extendedAnimator))
        {
            Vector3? pawnOffset = extendedAnimator.offset;
            if (pawnOffset != null)
            {
                offset += (Vector3)pawnOffset;
            }
        }
    }

    public override void TransformRotation(PawnRenderNode node, PawnDrawParms parms, ref Quaternion rotation)
    {
        if (node.AnimationWorker is AnimationWorker_ExtendedKeyframes
            && node.tree.pawn.TryGetComp<CompExtendedAnimator>(out CompExtendedAnimator extendedAnimator))
        {
            int? pawnRotation = extendedAnimator.rotation;
            if (pawnRotation != null)
            {
                Quaternion additionalRotation = Quaternion.AngleAxis((int)pawnRotation, Vector3.up);
                rotation *= additionalRotation;
            }
        }   

        base.TransformRotation(node, parms, ref rotation);
    }
}
