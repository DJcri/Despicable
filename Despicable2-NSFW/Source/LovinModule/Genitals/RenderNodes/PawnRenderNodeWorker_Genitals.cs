using RimWorld;

using UnityEngine;

using Verse;

namespace Despicable;

public class PawnRenderNodeWorker_Genitals : PawnRenderNodeWorker_FlipWhenCrawling
{
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (!base.CanDrawNow(node, parms))
        {
            return false;
        }

        if (parms.facing != Rot4.North)
        {
            return true;
        }

        return parms.flipHead;
    }

    public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
    {
        AnimationDef animationDef = parms.pawn?.Drawer?.renderer?.renderTree?.currentAnimation;

        AnimationPart animPart = null;
        bool hasAnimPart = node?.tree != null && node.tree.TryGetAnimationPartForNode(node, out animPart);

        float num = node?.DebugAngleOffset ?? 0f;

        if (node?.parent?.Props?.drawData != null)
        {
            num += node.parent.Props.drawData.RotationOffsetForRot(parms.facing);
        }

        bool canUseAnimationAngle =
            node?.parent?.AnimationWorker != null &&
            animationDef != null &&
            hasAnimPart &&
            animPart != null &&
            !WorkshopRenderContext.Active &&
            !parms.flags.FlagSet(PawnRenderFlags.Portrait) &&
            node.parent.AnimationWorker.Enabled(animationDef, node, animPart, parms);

        if (canUseAnimationAngle)
        {
            num += node.parent.AnimationWorker.AngleAtTick(
                node.tree.AnimationTick,
                animationDef,
                node,
                animPart,
                parms
            );
        }

        if (node?.hediff?.Part?.flipGraphic ?? false)
        {
            num *= -1f;
        }

        return Quaternion.AngleAxis(num, Vector3.up);
    }

    public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
    {
        Vector3 vector = base.OffsetFor(node, parms, out pivot);

        string bodyType = parms.pawn?.story?.bodyType?.defName;
        if (string.IsNullOrEmpty(bodyType))
        {
            return vector;
        }

        BodyTypeGenitalsOffsetDef bodyTypeAppendageOffsetDef =
            DefDatabase<BodyTypeGenitalsOffsetDef>.GetNamedSilentFail(bodyType);

        Vector3? bodyTypeAppendageOffset = bodyTypeAppendageOffsetDef?.offset;

        if (bodyTypeAppendageOffset != null)
        {
            if (parms.facing == Rot4.East)
            {
                vector += Vector3.right * bodyTypeAppendageOffset.Value.x;
            }
            else if (parms.facing == Rot4.West)
            {
                vector += Vector3.left * bodyTypeAppendageOffset.Value.x;
            }

            vector += Vector3.up * bodyTypeAppendageOffset.Value.y;
            vector += Vector3.forward * bodyTypeAppendageOffset.Value.z;
        }

        return vector;
    }
}
