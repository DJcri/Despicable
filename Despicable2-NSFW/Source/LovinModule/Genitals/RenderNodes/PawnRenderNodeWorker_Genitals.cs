using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;

public class PawnRenderNodeWorker_Genitals : PawnRenderNodeWorker_FlipWhenCrawling
{
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (!base.CanDrawNow(node, parms))
            return false;

        if (parms.facing != Rot4.North)
            return true;

        return parms.flipHead;
    }

    public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
    {
        AnimationDef animationDef = parms.pawn?.Drawer?.renderer?.CurAnimation
            ?? parms.pawn?.Drawer?.renderer?.renderTree?.currentAnimation;

        float num = node?.DebugAngleOffset ?? 0f;

        if (node?.parent?.Props?.drawData != null)
            num += node.parent.Props.drawData.RotationOffsetForRot(parms.facing);

        bool handledByWorkshopPortraitInheritance =
            WorkshopRenderContext.Active &&
            parms.flags.FlagSet(PawnRenderFlags.Portrait);

        if (!handledByWorkshopPortraitInheritance && animationDef != null)
        {
            int animationTick = node?.tree?.AnimationTick ?? 0;

            if (TryGetInheritedAnimationAngle(node, parms, animationDef, animationTick, out float inheritedAngle))
                num += inheritedAngle;

            if (WorkshopRenderContext.Active
                && !parms.flags.FlagSet(PawnRenderFlags.Portrait)
                && TryGetLocalAnimationAngle(node, parms, animationDef, animationTick, out float localAngle))
            {
                num += localAngle;
            }
        }

        if (node?.hediff?.Part?.flipGraphic ?? false)
            num *= -1f;

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
        AnatomyPartDef part = (node?.Props as AnatomyPartNodeProperties)?.anatomyPart;
        if (part == null)
            return vector;

        return vector + AnatomyPlacementResolver.ResolveOffset(parms.pawn, part, parms.facing);
    }
}
