using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Despicable;
// Guardrail-Reason: Single render-tree Harmony seam kept together to minimize fragile patch choreography.
[HarmonyPatch(typeof(PawnRenderTree), "TryGetMatrix")]
public static partial class HarmonyPatch_PawnRenderTree_TryGetMatrix
{
    // Re-entrancy guard: when we query ancestor matrices to find correct pivot points,
    // we must not re-apply workshop deltas or we'll double-transform.
    [ThreadStatic] private static bool skipWorkshopDeltas;

    public static void ResetRuntimeState()
    {
        skipWorkshopDeltas = false;
        keyframeScaleGettersByType.Clear();
    }

    public static bool Prefix(PawnRenderTree __instance, PawnRenderNode node, ref PawnDrawParms parms, ref Matrix4x4 matrix, ref bool __result)
    {
        // Keep vanilla portraits untouched, except for the animation workshop preview where
        // we intentionally opt into keyframe-driven node facing.
        if (parms.Portrait && !WorkshopRenderContext.Active)
        {
            return true;
        }

        bool workshop = WorkshopRenderContext.Active;

        CompExtendedAnimator animator = null;
        bool isAnimating = workshop;
        bool usesExtendedFeatures = workshop;

        if (!workshop)
        {
            if (!VisualActivityTracker.AnyExtendedAnimatorsActive)
            {
                if (!(node.Props is PawnRenderNodeProperties_GraphicVariants gvFastProps) || !gvFastProps.absoluteTransform)
                {
                    return true;
                }
            }
            else
            {
                Pawn pawn = __instance.pawn;
                if (pawn != null && pawn.RaceProps?.Humanlike == true)
                {
                    animator = pawn.TryGetComp<CompExtendedAnimator>();
                    isAnimating = animator?.hasAnimPlaying == true;
                    usesExtendedFeatures = animator?.UsesExtendedAnimationFeatures == true;
                }
            }
        }

        // Fast path: if not animating, we only care about absoluteTransform nodes (rare).
        if (!isAnimating)
        {
            var gvProps = node.Props as PawnRenderNodeProperties_GraphicVariants;
            if (gvProps == null || !gvProps.absoluteTransform)
            {
                return true;
            }
        }

        // Facing offsets fix (extended feature clips only): force facing based on the nearest animating ancestor.
        if (usesExtendedFeatures)
        {
            PawnRenderNode animatingNode = node;
            while (animatingNode != null && !(animatingNode.AnimationWorker is AnimationWorker_ExtendedKeyframes))
            {
                animatingNode = animatingNode.parent;
            }

            if (animatingNode?.AnimationWorker is AnimationWorker_ExtendedKeyframes animWorker
                && animatingNode.tree.TryGetAnimationPartForNode(animatingNode, out AnimationPart animPart)
                && animPart != null)
            {
                parms.facing = animWorker.FacingAtTick(__instance.AnimationTick, animPart);
            }
        }

        // Absolute transformation for prop nodes (used to prevent parent transforms from leaking into props).
        if (node.Props is PawnRenderNodeProperties_GraphicVariants graphicVariantProp && graphicVariantProp.absoluteTransform)
        {
            matrix = parms.matrix;

            node.GetTransform(parms, out Vector3 offset, out Vector3 pivot, out Quaternion quaternion, out Vector3 scale);

            if (offset != Vector3.zero)
            {
                matrix *= Matrix4x4.Translate(offset);
            }

            if (pivot != Vector3.zero)
            {
                matrix *= Matrix4x4.Translate(pivot);
            }

            if (quaternion != Quaternion.identity)
            {
                matrix *= Matrix4x4.Rotate(quaternion);
            }

            if (scale != Vector3.one)
            {
                matrix *= Matrix4x4.Scale(scale);
            }

            if (pivot != Vector3.zero)
            {
                matrix *= Matrix4x4.Translate(pivot).inverse;
            }

            float num = node.Worker.AltitudeFor(node, parms);
            if (num != 0f)
            {
                matrix *= Matrix4x4.Translate(Vector3.up * num);
            }

            __result = true;
            return false;
        }

        return true;
    }

    /// <summary>
    /// Workshop portrait preview: vanilla portraits apply the global portrait transform (including the workshop's
    /// root sliders), but extended keyframe offset/angle are not reliably reflected for non-root nodes.
    ///
    /// To match runtime appearance while scrubbing, we sample the node's KeyframeAnimationPart at the current
    /// AnimationTick (overridden to the workshop scrubber tick) and compose the interpolated offset/angle onto
    /// the matrix that vanilla already computed.
    /// </summary>
    // Cached reflection handles for Verse.Keyframe scale.
    // RimWorld/Verse versions differ: the member may be a field or property, may be named
    // scale/Scale/drawScale/DrawScale, and may live on a derived keyframe type.
    private static readonly Dictionary<Type, Func<Verse.Keyframe, Vector3>> keyframeScaleGettersByType
        = new Dictionary<Type, Func<Verse.Keyframe, Vector3>>(32);

    public static void Postfix(PawnRenderTree __instance, PawnRenderNode node, ref PawnDrawParms parms, ref Matrix4x4 matrix, bool __result)
    {
        if (!__result || !WorkshopRenderContext.Active || __instance == null || node == null || skipWorkshopDeltas)
        {
            return;
        }

        // In workshop preview, AnimationTick is driven by the scrubber. Cache it once so we don't
        // redeclare the same local name in multiple blocks (older compilers can be picky).
        int animTick = __instance.AnimationTick;

        // Absolute-transform props already rebuild their matrix in Prefix (and should remain isolated).
        if (node.Props is PawnRenderNodeProperties_GraphicVariants gv && gv.absoluteTransform)
        {
            return;
        }

        // --- Prop scale ---
        // Keyframed scale is expected to be handled by the engine's keyframe pipeline.
        // (We still force recache in workshop preview so changes apply immediately.)

        // The rest of this Postfix is the portrait-only offset/angle inheritance fix.
        if (!parms.Portrait)
        {
            return;
        }

        // We want workshop offsets to be in pawn-space axes (stable), not node-local axes.
        // Also, children should inherit keyed transforms from ancestors even if the child has no explicit track.
        // To make this robust against portrait caching/order, we explicitly accumulate keyed ancestor deltas.
        int tick = animTick;

        // Build a chain from the nearest absoluteTransform ancestor (or root) down to this node.
        // AbsoluteTransform nodes intentionally reset their basis to parms.matrix, so ancestors above them
        // should not leak into their subtree.
        var chain = new List<PawnRenderNode>(8);
        PawnRenderNode cur = node;
        while (cur != null)
        {
            chain.Add(cur);
            if (cur.Props is PawnRenderNodeProperties_GraphicVariants gvp && gvp.absoluteTransform)
            {
                break;
            }

            cur = cur.parent;
        }

        // Reverse so we apply parent->child.
        chain.Reverse();

        // Workshop offset axes:
        // - In runtime, offsets are effectively in pawn-space.
        // - In the animation workshop *portrait preview*, offsets should be in screen-space so X/Y always moves
        //   left/right/up/down on the screen regardless of pawn facing.
        // parms.matrix may include scale; use its rotation (orthonormal) and (for portraits) remove yaw.
        Quaternion basisRot = parms.matrix.rotation;

        // In portrait preview, pawn facing is mostly a yaw around 'up'. Remove that yaw so basis is screen-stable.
        if (parms.Portrait)
        {
            Vector3 fwd = basisRot * Vector3.forward;
            Vector3 fwdProj = Vector3.ProjectOnPlane(fwd, Vector3.up);
            if (fwdProj.sqrMagnitude > 1e-6f)
            {
                Quaternion yaw = Quaternion.FromToRotation(Vector3.forward, fwdProj.normalized);
                basisRot = Quaternion.Inverse(yaw) * basisRot;
            }
        }

        Vector3 basisRight = basisRot * Vector3.right;
        Vector3 basisUp = basisRot * Vector3.up;
        Vector3 basisForward = basisRot * Vector3.forward;

        // Compose deltas so translations/rotations stay in pawn-space axes.
        // IMPORTANT: rotations must happen around the correct node pivot. When we enabled inheritance,
        // rotating around the wrong pivot makes child nodes "orbit" around a distant point.
        var sampledOffset = new Vector3[chain.Count];
        var sampledAngle = new float[chain.Count];
        var hasDelta = new bool[chain.Count];

        for (int i = 0; i < chain.Count; i++)
        {
            PawnRenderNode n = chain[i];
            if (n == null)
            {
                continue;
            }

            if (!(n.AnimationWorker is AnimationWorker_ExtendedKeyframes))
            {
                continue;
            }

            if (!n.tree.TryGetAnimationPartForNode(n, out AnimationPart part) || part == null)
            {
                continue;
            }

            if (!(part is KeyframeAnimationPart kap) || kap.keyframes.NullOrEmpty())
            {
                continue;
            }

            if (!TrySampleOffsetAngleInterpolated(kap, tick, out Vector3 localOffset, out float localAngle))
            {
                continue;
            }

            sampledOffset[i] = basisRight * localOffset.x + basisUp * localOffset.y + basisForward * localOffset.z;
            sampledAngle[i] = localAngle;
            hasDelta[i] = sampledOffset[i] != Vector3.zero || !Mathf.Approximately(localAngle, 0f);
        }

        // Fetch vanilla matrices (without workshop deltas) for pivot computation.
        // We only need matrices for nodes that are actually used as pivots (i.e. have angle).
        var vanillaMatrix = new Matrix4x4[chain.Count];
        var hasVanilla = new bool[chain.Count];
        for (int i = 0; i < chain.Count; i++)
        {
            if (!hasDelta[i])
            {
                continue;
            }

            PawnRenderNode n = chain[i];
            if (n == null)
            {
                continue;
            }

            if (TryGetVanillaMatrix(__instance, n, ref parms, out Matrix4x4 m))
            {
                vanillaMatrix[i] = m;
                hasVanilla[i] = true;
            }
        }

        // Determine each node's local pivot point (texture center / graphic pivot), independent of children.
        // This is the point that vanilla rotations pivot around: (T(p) * R * T(-p)) leaves p invariant.
        var pivotLocalByIndex = new Vector3[chain.Count];
        for (int i = 0; i < chain.Count; i++)
        {
            PawnRenderNode n = chain[i];
            if (n == null)
            {
                continue;
            }

            n.GetTransform(parms, out _, out Vector3 pivotLocal, out _, out _);
            pivotLocalByIndex[i] = pivotLocal;
        }

        // Compute pivot points in world-space in the presence of inherited transforms.
        // We do this iteratively: pivot[i] is the origin of node i after applying deltas of ancestors < i.
        var pivotWorldByIndex = new Vector3[chain.Count];
        for (int i = 0; i < chain.Count; i++)
        {
            Matrix4x4 m = hasVanilla[i] ? vanillaMatrix[i] : parms.matrix;

            for (int j = 0; j < i; j++)
            {
                if (!hasDelta[j])
                {
                    continue;
                }

                if (sampledOffset[j] != Vector3.zero)
                {
                    m = Matrix4x4.Translate(sampledOffset[j]) * m;
                }

                if (!Mathf.Approximately(sampledAngle[j], 0f))
                {
                    Quaternion qj = Quaternion.AngleAxis(sampledAngle[j], basisUp);
                    Vector3 pj = pivotWorldByIndex[j];
                    m = Matrix4x4.Translate(pj) * Matrix4x4.Rotate(qj) * Matrix4x4.Translate(-pj) * m;
                }
            }

            pivotWorldByIndex[i] = m.MultiplyPoint3x4(pivotLocalByIndex[i]);
        }

        // Finally, apply all deltas (ancestor -> this node) to the current node matrix.
        for (int i = 0; i < chain.Count; i++)
        {
            if (!hasDelta[i])
            {
                continue;
            }

            if (sampledOffset[i] != Vector3.zero)
            {
                matrix = Matrix4x4.Translate(sampledOffset[i]) * matrix;
            }

            if (!Mathf.Approximately(sampledAngle[i], 0f))
            {
                Quaternion q = Quaternion.AngleAxis(sampledAngle[i], basisUp);
                Vector3 pivotWorld = pivotWorldByIndex[i];
                matrix = Matrix4x4.Translate(pivotWorld) * Matrix4x4.Rotate(q) * Matrix4x4.Translate(-pivotWorld) * matrix;
            }
        }
    }
}

// Recache requests: usesExtendedFeatures-only. Vanilla animations should rely on the engine's own dirtying.
[HarmonyPatch(typeof(PawnRenderTree), "AdjustParms")]
public static partial class HarmonyPatch_PawnRenderTree_AdjustParms
{
    public static void Prefix(PawnRenderTree __instance, ref PawnDrawParms parms)
    {
        // Keep vanilla portraits untouched, except for the animation workshop preview where
        // we intentionally opt into keyframe-driven node facing + recache.
        if (parms.Portrait && !WorkshopRenderContext.Active)
        {
            return;
        }

        // Workshop preview applies the compiled animation directly to the pawn renderer.
        // We need requests to be rebuilt even when the pawn isn't "playing" via CompExtendedAnimator.
        //
        // Portraits: recache every render (keeps scrubbing perfectly in sync).
        // Non-portraits (the AGS authoring preview uses portrait:false): recache periodically so
        // keyframed scale/visibility/variants cannot get "stuck" until a manual UI toggle.
        if (WorkshopRenderContext.Active)
        {
            if (__instance.rootNode == null)
            {
                return;
            }

            if (parms.Portrait)
            {
                __instance.rootNode.requestRecache = true;
                return;
            }

            const int WorkshopNonPortraitRecacheInterval = 4; // ticks (60 ticks/sec). Small but not every single call.
            if (WorkshopRenderContext.Tick <= 0 || (WorkshopRenderContext.Tick % WorkshopNonPortraitRecacheInterval) == 0)
            {
                __instance.rootNode.requestRecache = true;
            }
            // Don't return: let normal runtime animators still mark recache if needed.
        }

        CompExtendedAnimator animator = __instance.pawn?.TryGetComp<CompExtendedAnimator>();
        if (animator?.hasAnimPlaying != true)
        {
            return;
        }

        if (animator.UsesExtendedAnimationFeatures != true)
        {
            return;
        }

        PawnRenderNode root = __instance.rootNode;
        if (root == null)
        {
            return;
        }

        int tick = animator.animationTicks;

        if (root.AnimationWorker is AnimationWorker_ExtendedKeyframes rootWorker
            && root.tree.TryGetAnimationPartForNode(root, out AnimationPart rootPart)
            && rootPart != null
            && rootWorker.ShouldRecache(tick, rootPart))
        {
            root.requestRecache = true;
            return;
        }

        // Descendant recache (visibility/rotation/state changes)
        if (TryMarkRecacheRecursive(__instance, root, tick))
        {
            return;
        }
    }
}
