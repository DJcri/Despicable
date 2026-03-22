using HarmonyLib;
using Verse;

namespace Despicable;
// Adjust facing + visibility during ExtendedKeyframe animations.
[HarmonyPatch(typeof(PawnRenderNode), "AppendRequests")]
public static class HarmonyPatch_PawnRenderNode_AppendRequests
{
    public static bool Prefix(PawnRenderNode __instance, ref PawnDrawParms parms)
    {
        Pawn pawn = parms.pawn ?? __instance?.tree?.pawn;
        if (pawn == null)
            return true;

        if (HarmonyPatch_ForeignEyeGeneGraphics.ShouldSuppressNode(__instance, pawn))
            return false;

        // Vanilla UI portraits should stay vanilla for animation behavior. Foreign-eye suppression
        // still needs to apply above so portraits remain visually correct.
        if (parms.Portrait && !WorkshopRenderContext.Active)
            return true;

        var extendedAnimWorker = __instance.AnimationWorker as AnimationWorker_ExtendedKeyframes;
        if (extendedAnimWorker == null)
            return true;

        // In-game playback is driven by CompExtendedAnimator state.
        // Workshop preview (portrait) applies a compiled animation directly to the pawn renderer,
        // so we allow keyframe-driven behavior even when the comp is not "playing".
        if (!WorkshopRenderContext.Active)
        {
            if (!VisualActivityTracker.AnyExtendedAnimatorsActive)
                return true;

            if (pawn.RaceProps?.Humanlike != true)
                return true;

            var animator = pawn.TryGetComp<CompExtendedAnimator>();
            if (animator?.hasAnimPlaying != true)
                return true;
            if (animator.UsesExtendedAnimationFeatures != true)
                return true;
        }

        if (!__instance.tree.TryGetAnimationPartForNode(__instance, out AnimationPart animPart) || animPart == null)
            return true;

        // Adjust facing so oriented textures/parts match keyframe-specified facing.
        parms.facing = extendedAnimWorker.FacingAtTick(__instance.tree.AnimationTick, animPart);

        // Optional visibility gating (allows hiding body/head nodes)
        return extendedAnimWorker.VisibleAtTick(__instance.tree.AnimationTick, animPart);
    }
}

// Apply keyed draw-order bias through the engine's render-layer scalar, not through spatial offsets.
// This keeps x/y/z animation semantics untouched and lets the studio's "Draw Order" knob behave as a true
// render ordering control.
[HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.LayerFor))]
public static class HarmonyPatch_PawnRenderNodeWorker_TransformLayer
{
    // Small enough to avoid stomping vanilla layer bands, large enough for a visible ordering nudge.
    private const float LayerBiasStep = 0.001f;

    public static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref float __result)
    {
        if (node == null)
            return;

        if (parms.Portrait && !WorkshopRenderContext.Active)
            return;

        var extendedAnimWorker = node.AnimationWorker as AnimationWorker_ExtendedKeyframes;
        if (extendedAnimWorker == null)
            return;

        if (!WorkshopRenderContext.Active)
        {
            if (!VisualActivityTracker.AnyExtendedAnimatorsActive)
                return;

            Pawn pawn = node.tree?.pawn;
            if (pawn == null)
                return;
            if (pawn.RaceProps?.Humanlike != true)
                return;

            var animator = pawn.TryGetComp<CompExtendedAnimator>();
            if (animator?.hasAnimPlaying != true)
                return;
            if (animator.UsesExtendedAnimationFeatures != true)
                return;
        }

        if (node.tree == null)
            return;

        if (!node.tree.TryGetAnimationPartForNode(node, out AnimationPart animPart) || animPart == null)
            return;

        int bias = extendedAnimWorker.LayerBiasAtTick(node.tree.AnimationTick, animPart);
        if (bias == 0)
            return;

        __result += bias * LayerBiasStep;
    }
}

