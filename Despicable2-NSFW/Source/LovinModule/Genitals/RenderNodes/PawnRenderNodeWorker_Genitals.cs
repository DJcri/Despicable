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
        AnimationDef animationDef = parms.pawn?.Drawer?.renderer?.CurAnimation
            ?? parms.pawn?.Drawer?.renderer?.renderTree?.currentAnimation;

        float num = node?.DebugAngleOffset ?? 0f;

        if (node?.parent?.Props?.drawData != null)
        {
            num += node.parent.Props.drawData.RotationOffsetForRot(parms.facing);
        }

        bool handledByWorkshopPortraitInheritance =
            WorkshopRenderContext.Active &&
            parms.flags.FlagSet(PawnRenderFlags.Portrait);

        if (!handledByWorkshopPortraitInheritance
            && animationDef != null
            && TryResolveAnimationAngleSource(node, out PawnRenderNode angleNode, out AnimationPart animPart))
        {
            AnimationWorker worker = angleNode?.AnimationWorker;
            if (worker != null && worker.Enabled(animationDef, angleNode, animPart, parms))
            {
                num += worker.AngleAtTick(
                    node.tree.AnimationTick,
                    animationDef,
                    angleNode,
                    animPart,
                    parms
                );
            }
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

    private static bool TryResolveAnimationAngleSource(PawnRenderNode node, out PawnRenderNode angleNode, out AnimationPart animPart)
    {
        angleNode = null;
        animPart = null;

        PawnRenderNode current = node;
        while (current != null)
        {
            if (current.tree != null
                && current.AnimationWorker != null
                && current.tree.TryGetAnimationPartForNode(current, out animPart)
                && animPart != null)
            {
                angleNode = current;
                return true;
            }

            current = current.parent;
        }

        animPart = null;
        return false;
    }
}
