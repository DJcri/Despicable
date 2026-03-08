using HarmonyLib;
using UnityEngine;
using Verse;

namespace Despicable;
[HarmonyPatch(typeof(PawnRenderer), "BodyAngle")]
public static class HarmonyPatch_PawnRenderer_BodyAngle
{
    public static bool Prefix(Pawn ___pawn, ref float __result)
    {
        if (!VisualActivityTracker.AnyExtendedAnimatorsActive) return true;
        if (___pawn == null) return true;
        if (___pawn.RaceProps?.Humanlike != true) return true;

        var animator = ___pawn.TryGetComp<CompExtendedAnimator>();
        if (animator?.hasAnimPlaying == true && animator.UsesExtendedAnimationFeatures == true)
        {
            // Runtime body-angle override disabled: it caused live pawn rotation to diverge from studio preview.
            return true;
        }

        return true;
    }
}

[HarmonyPatch(typeof(PawnRenderer), "GetBodyPos")]
public static class HarmonyPatch_PawnRenderer_GetBodyPos
{
    public static void Postfix(PawnRenderer __instance, Pawn ___pawn, ref Vector3 __result)
    {
        if (!VisualActivityTracker.AnyExtendedAnimatorsActive) return;
        if (___pawn == null) return;
        if (___pawn.RaceProps?.Humanlike != true) return;

        var animator = ___pawn.TryGetComp<CompExtendedAnimator>();
        if (animator?.hasAnimPlaying != true) return;
        if (animator.UsesExtendedAnimationFeatures != true) return;

        // Runtime altitude override disabled: it caused live layering to diverge from studio preview.
    }
}
