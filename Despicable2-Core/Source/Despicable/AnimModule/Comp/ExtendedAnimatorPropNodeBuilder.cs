using System.Collections.Generic;
using Verse;

namespace Despicable;

/// <summary>
/// Builds runtime-injected animation prop render nodes for the active animation.
/// Keeps prop lookup caches and render-node discovery out of the main comp shell.
/// </summary>
public sealed class ExtendedAnimatorPropNodeBuilder
{
    private static readonly PropIndexCache PropIndex = new();

    public List<PawnRenderNode> BuildNodes(Pawn pawn, ExtendedAnimatorRuntimeState runtime)
    {
        // Only attach props while animating (perf).
        // Workshop preview uses SetAnimation() without flipping hasAnimPlaying, so allow it in workshop scope.
        if (!runtime.hasAnimPlaying && !WorkshopRenderContext.Active)
            return null;

        EnsurePropIndex();

        AnimationDef activeAnim = GetActiveAnimationForProps(pawn, runtime);
        if (activeAnim == null)
            return null;

        HashSet<PawnRenderNodeTagDef> usedTags = CollectUsedNodeTags(activeAnim);
        if (usedTags == null || usedTags.Count == 0)
            return null;

        var animRenderNodes = new List<PawnRenderNode>();
        foreach (PawnRenderNodeTagDef tag in usedTags)
        {
            if (!PropIndex.TryGetValue(tag, out List<AnimationPropDef> propDefs) || propDefs.NullOrEmpty())
                continue;

            for (int i = 0; i < propDefs.Count; i++)
            {
                AnimationPropDef animationProp = propDefs[i];
                if (animationProp == null)
                    continue;

                PawnRenderNodeProperties sourceProps = animationProp.animPropProperties;
                if (sourceProps == null)
                    continue;

                PawnRenderNodeProperties props = CommonUtil.CloneNodeProperties(sourceProps);
                if (props == null)
                    continue;

                if (props.texPath.NullOrEmpty())
                    props.texPath = "AnimationProps/MissingTexture/MissingTexture";

                PawnRenderNode animRenderNode = CommonUtil.CreateNodeFromOwnedProps(pawn, props);

                // Mark runtime-injected prop nodes so the worker can apply prop-specific defaults.
                if (animRenderNode is PawnRenderNode_GraphicVariants gv)
                {
                    gv.isAnimPropNode = true;
                    gv.hideUntilDrivenState = true;
                }

                animRenderNodes.Add(animRenderNode);
            }
        }

        return animRenderNodes;
    }

    public bool AnimationMakesUseOfProp(ExtendedAnimatorRuntimeState runtime, AnimationPropDef animationProp)
    {
        if (!runtime.hasAnimPlaying || animationProp == null)
            return false;

        List<AnimationDef> animQueue = runtime.animQueue;
        if (animQueue.NullOrEmpty())
            return false;

        for (int i = 0; i < animQueue.Count; i++)
        {
            AnimationDef animation = animQueue[i];
            if (animation?.keyframeParts == null)
                continue;

            foreach (PawnRenderNodeTagDef propTag in animation.keyframeParts.Keys)
            {
                if (propTag == animationProp.animPropProperties.tagDef)
                    return true;
            }
        }

        return false;
    }

    private static void EnsurePropIndex()
    {
        if (PropIndex.IsBuilt)
            return;

        var index = new Dictionary<PawnRenderNodeTagDef, List<AnimationPropDef>>();
        List<AnimationPropDef> allProps = DefDatabase<AnimationPropDef>.AllDefsListForReading;
        for (int i = 0; i < allProps.Count; i++)
        {
            AnimationPropDef p = allProps[i];
            PawnRenderNodeTagDef tag = p?.animPropProperties?.tagDef;
            if (tag == null)
                continue;

            if (!index.TryGetValue(tag, out List<AnimationPropDef> list))
            {
                list = new List<AnimationPropDef>();
                index.Add(tag, list);
            }

            list.Add(p);
        }

        PropIndex.Replace(index);
    }

    private static AnimationDef GetActiveAnimationForProps(Pawn pawn, ExtendedAnimatorRuntimeState runtime)
    {
        // Prefer the render tree's current animation (what is actually playing).
        AnimationDef cur = pawn?.Drawer?.renderer?.CurAnimation;
        if (cur != null)
            return cur;

        return runtime.animQueue.NullOrEmpty() ? null : runtime.animQueue[0];
    }

    private static HashSet<PawnRenderNodeTagDef> CollectUsedNodeTags(AnimationDef anim)
    {
        var set = new HashSet<PawnRenderNodeTagDef>();
        if (anim == null)
            return set;

        if (anim.keyframeParts != null)
        {
            foreach (var kv in anim.keyframeParts)
            {
                if (kv.Key != null)
                    set.Add(kv.Key);
            }
        }

        if (anim.curveParts != null)
        {
            foreach (var kv in anim.curveParts)
            {
                if (kv.Key != null)
                    set.Add(kv.Key);
            }
        }

        return set;
    }

    private sealed class PropIndexCache
    {
        private Dictionary<PawnRenderNodeTagDef, List<AnimationPropDef>> index;

        public bool IsBuilt => index != null;

        public bool TryGetValue(PawnRenderNodeTagDef tag, out List<AnimationPropDef> propDefs)
        {
            if (index == null)
            {
                propDefs = null;
                return false;
            }

            return index.TryGetValue(tag, out propDefs);
        }

        public void Replace(Dictionary<PawnRenderNodeTagDef, List<AnimationPropDef>> nextIndex)
        {
            index = nextIndex;
        }
    }
}
