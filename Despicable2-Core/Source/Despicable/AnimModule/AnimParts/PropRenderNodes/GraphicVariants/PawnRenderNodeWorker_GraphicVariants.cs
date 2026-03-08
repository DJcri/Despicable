using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderNodeWorker_GraphicVariants : PawnRenderNodeWorker
{
    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (!base.CanDrawNow(node, parms)) return false;
        // Prop nodes are normally disabled in portraits to avoid cluttering
        // vanilla UI rendering. The animation workshop preview, however, is
        // portrait-based, so allow portrait drawing only within a workshop
        // render scope.
        if (parms.Portrait && !WorkshopRenderContext.Active) return false;

        // Don't draw at all if the pawn isn't animating.
        // Workshop preview uses SetAnimation() without flipping hasAnimPlaying, so allow it in workshop scope.
        if (node?.tree?.pawn?.TryGetComp<CompExtendedAnimator>()?.hasAnimPlaying != true && !WorkshopRenderContext.Active)
            return false;

        bool visibleByLegacyWorker = true;
        bool haveLegacyAnimPart = false;

        // If using legacy extended keyframes, respect VisibleAtTick.
        // (But do not early-return, because prop nodes also need "hide until driven" behavior.)
        if (node.AnimationWorker is AnimationWorker_ExtendedKeyframes extendedAnimator)
        {
            if (node.tree.TryGetAnimationPartForNode(node, out AnimationPart animPart) && animPart != null)
            {
                haveLegacyAnimPart = true;
                visibleByLegacyWorker = extendedAnimator.VisibleAtTick(node.tree.AnimationTick, animPart);
            }
        }

        if (!visibleByLegacyWorker)
            return false;

        // Prop nodes should not pop into view by default; require an explicitly driven state/variant.
        if (node is PawnRenderNode_GraphicVariants gv && gv.isAnimPropNode && gv.hideUntilDrivenState)
        {
            // Legacy path: require a state id or a variant at this tick.
            if (node.AnimationWorker is AnimationWorker_ExtendedKeyframes legacy)
            {
                if (node.tree.TryGetAnimationPartForNode(node, out AnimationPart animPart) && animPart != null)
                {
                    string stateId = legacy.GraphicStateAtTick(node.tree.AnimationTick, animPart);
                    if (!stateId.NullOrEmpty())
                        return !IsHiddenState(stateId);

                    int? variant = legacy.VariantTexPathOnTick(node.tree.AnimationTick, animPart);
                    if (variant != null)
                        return true;

                    // Legacy-only visibility tracks should be allowed to drive prop display
                    // even if no explicit state/variant is provided.
                    return haveLegacyAnimPart && visibleByLegacyWorker;
                }
                return false;
            }

            // Vanilla path: require a GraphicStateDef at this tick.
            if (node.TryGetAnimationGraphicState(parms, out GraphicStateDef stateDef) && stateDef != null)
                return !IsHiddenState(stateDef.defName);

            return false;
        }

        return true;
    }

    protected override Material GetMaterial(PawnRenderNode node, PawnDrawParms parms)
    {
        if (node?.tree?.pawn?.TryGetComp<CompExtendedAnimator>()?.hasAnimPlaying != true && !WorkshopRenderContext.Active)
            return base.GetMaterial(node, parms);

        if (node is PawnRenderNode_GraphicVariants nodeWithStates
            && node.AnimationWorker is AnimationWorker_ExtendedKeyframes extendedAnimWorker
            && node.tree.TryGetAnimationPartForNode(node, out AnimationPart animPart)
            && animPart != null)
        {
            string stateId = extendedAnimWorker.GraphicStateAtTick(node.tree.AnimationTick, animPart);
            if (!stateId.NullOrEmpty())
            {
                Material mat = GetMaterialState(nodeWithStates, parms, stateId);
                if (mat != null) return mat;
            }
            else
            {
                int? variant = extendedAnimWorker.VariantTexPathOnTick(node.tree.AnimationTick, animPart);
                if (variant != null)
                {
                    Material mat = GetMaterialVariant(nodeWithStates, parms, variant.Value);
                    if (mat != null) return mat;
                }
            }
        }

        // Vanilla path: map GraphicStateDef.defName -> our per-node state graphics.
        if (node is PawnRenderNode_GraphicVariants nodeWithStates2
            && !(node.AnimationWorker is AnimationWorker_ExtendedKeyframes)
            && node.TryGetAnimationGraphicState(parms, out GraphicStateDef stateDef)
            && stateDef != null)
        {
            Material mat = GetMaterialState(nodeWithStates2, parms, stateDef.defName);
            if (mat != null) return mat;
        }

        return base.GetMaterial(node, parms);
    }

    public virtual Material GetMaterialState(PawnRenderNode_GraphicVariants node, PawnDrawParms parms, string stateId)
    {
        if (IsHiddenState(stateId))
            return null;

        Material material = node.GetGraphicState(stateId)?.NodeGetMat(parms);
        if (material == null) return null;

        if (!parms.Portrait && parms.flags.FlagSet(PawnRenderFlags.Invisible))
            material = InvisibilityMatPool.GetInvisibleMat(material);

        return material;
    }

    private static bool IsHiddenState(string stateId)
    {
        return !stateId.NullOrEmpty() && stateId.Equals("Hidden", System.StringComparison.OrdinalIgnoreCase);
    }

    // Back-compat (1-based)
    public virtual Material GetMaterialVariant(PawnRenderNode_GraphicVariants node, PawnDrawParms parms, int variant)
    {
        Material material = node.getGraphicVariant(variant)?.NodeGetMat(parms);
        if (material == null) return null;

        if (!parms.Portrait && parms.flags.FlagSet(PawnRenderFlags.Invisible))
            material = InvisibilityMatPool.GetInvisibleMat(material);

        return material;
    }

    public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
    {
        Vector3 regularOffsets = base.OffsetFor(node, parms, out pivot);

        if ((node.Props as PawnRenderNodeProperties_GraphicVariants)?.propOffsetDef?.offsets is System.Collections.Generic.List<BaseAnimationOffset> offsets)
        {
            foreach (BaseAnimationOffset offset in offsets)
            {
                if (offset.appliesToPawn(node.tree.pawn))
                {
                    regularOffsets += offset.getOffset(node.tree.pawn) ?? Vector3.zero;
                    return regularOffsets;
                }
            }
        }

        return regularOffsets;
    }

    public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
    {
        Vector3 regularScale = base.ScaleFor(node, parms);

        if ((node.Props as PawnRenderNodeProperties_GraphicVariants)?.propOffsetDef?.offsets is System.Collections.Generic.List<BaseAnimationOffset> offsets)
        {
            foreach (BaseAnimationOffset offset in offsets)
            {
                if (offset.appliesToPawn(node.tree.pawn))
                {
                    regularScale = regularScale.MultipliedBy(offset.getScale(node.tree.pawn) ?? Vector3.one);
                    return regularScale;
                }
            }
        }

        return regularScale;
    }

    public override Quaternion RotationFor(PawnRenderNode node, PawnDrawParms parms)
    {
        Quaternion rotation = base.RotationFor(node, parms);

        if ((node.Props as PawnRenderNodeProperties_GraphicVariants)?.propOffsetDef?.offsets is System.Collections.Generic.List<BaseAnimationOffset> offsets)
        {
            foreach (BaseAnimationOffset offset in offsets)
            {
                if (offset.appliesToPawn(node.tree.pawn))
                {
                    rotation *= Quaternion.AngleAxis(offset.getRotation(node.tree.pawn) ?? 0, Vector3.up);
                    return rotation;
                }
            }
        }

        return rotation;
    }
}
