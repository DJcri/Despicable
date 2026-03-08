using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderNodeWorker_Genitals : PawnRenderNodeWorker_FlipWhenCrawling
{
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (base.CanDrawNow(node, parms))
        {
            if (!(parms.facing != Rot4.North))
            {
                return parms.flipHead;
            }
            return true;
        }
        return false;
    }

    public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
    {
        AnimationDef animationDef = parms.pawn.Drawer.renderer.renderTree.currentAnimation;
        AnimationPart animPart;
        node.tree.TryGetAnimationPartForNode(node, out animPart);

        float num = node.DebugAngleOffset;
        if (node.Props.drawData != null)
        {
            num += node.parent.Props.drawData.RotationOffsetForRot(parms.facing);
        }
        if (node.parent.AnimationWorker != null && node.parent.AnimationWorker.Enabled(animationDef, node, animPart, parms) && !parms.flags.FlagSet(PawnRenderFlags.Portrait))
        {
            num += node.parent.AnimationWorker.AngleAtTick(node.tree.AnimationTick, animationDef, node, animPart, parms);
        }
        if (node.hediff?.Part?.flipGraphic ?? false)
        {
            num *= -1f;
        }
        return Quaternion.AngleAxis(num, Vector3.up);
    }

    public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
    {
        Vector3 vector = base.OffsetFor(node, parms, out pivot);
        string bodyType = parms.pawn.story.bodyType.defName;
        BodyTypeGenitalsOffsetDef bodyTypeAppendageOffsetDef = DefDatabase<BodyTypeGenitalsOffsetDef>.GetNamedSilentFail(bodyType);
        Vector3? bodyTypeAppendageOffset = null;
        if (bodyTypeAppendageOffsetDef != null)
            bodyTypeAppendageOffset = bodyTypeAppendageOffsetDef.offset;

        if (bodyTypeAppendageOffset != null )
        {
            if (parms.facing == Rot4.East)
            {
                vector += Vector3.right * bodyTypeAppendageOffset.Value.x;
            }
            else if (parms.facing == Rot4.West)
            {
                vector += Vector3.left * bodyTypeAppendageOffset.Value.x;
            }

            vector += new Vector3(0, 1, 0) * bodyTypeAppendageOffset.Value.y;
            vector += new Vector3(0, 0, 1) * bodyTypeAppendageOffset.Value.z;
        }

        return vector;
    }
}
