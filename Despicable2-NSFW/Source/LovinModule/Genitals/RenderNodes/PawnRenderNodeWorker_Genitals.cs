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

        if (!handledByWorkshopPortraitInheritance && animationDef != null)
        {
            int animationTick = node?.tree?.AnimationTick ?? 0;

            if (TryGetInheritedAnimationAngle(node, parms, animationDef, animationTick, out float inheritedAngle))
            {
                num += inheritedAngle;
            }

            // In the animation studio preview we want genitals to keep inheriting the
            // parent node's angle while still responding to direct edits on the
            // Genitals track itself. Treat the node's own keyed angle as a local
            // additive tweak on top of the inherited pose, but only in workshop
            // preview so runtime behavior stays parent-driven.
            if (WorkshopRenderContext.Active
                && !parms.flags.FlagSet(PawnRenderFlags.Portrait)
                && TryGetLocalAnimationAngle(node, parms, animationDef, animationTick, out float localAngle))
            {
                num += localAngle;
            }
        }

        if (node?.hediff?.Part?.flipGraphic ?? false)
        {
            num *= -1f;
        }

        return Quaternion.AngleAxis(num, Vector3.up);
    }

    private static bool TryGetInheritedAnimationAngle(
        PawnRenderNode node,
        PawnDrawParms parms,
        AnimationDef animationDef,
        int animationTick,
        out float angle)
    {
        angle = 0f;

        AnimationPart animPart = null;
        bool hasAnimPart = node?.tree != null
            && node.tree.TryGetAnimationPartForNode(node, out animPart)
            && animPart != null;

        if (node?.parent?.AnimationWorker == null
            || animationDef == null
            || !hasAnimPart
            || !node.parent.AnimationWorker.Enabled(animationDef, node, animPart, parms))
        {
            return false;
        }

        angle = node.parent.AnimationWorker.AngleAtTick(
            animationTick,
            animationDef,
            node,
            animPart,
            parms
        );

        return true;
    }

    private static bool TryGetLocalAnimationAngle(
        PawnRenderNode node,
        PawnDrawParms parms,
        AnimationDef animationDef,
        int animationTick,
        out float angle)
    {
        angle = 0f;

        AnimationPart animPart = null;
        bool hasAnimPart = node?.tree != null
            && node.tree.TryGetAnimationPartForNode(node, out animPart)
            && animPart != null;

        if (node?.AnimationWorker == null
            || animationDef == null
            || !hasAnimPart
            || !node.AnimationWorker.Enabled(animationDef, node, animPart, parms))
        {
            return false;
        }

        angle = node.AnimationWorker.AngleAtTick(
            animationTick,
            animationDef,
            node,
            animPart,
            parms
        );

        return true;
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
