using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace Despicable;
[HarmonyPatch(typeof(Pawn_DrawTracker), "DrawPos", MethodType.Getter)]
public static class HarmonyPatch_DrawTracker_DrawPos
{
    private static readonly AccessTools.FieldRef<Pawn_DrawTracker, Pawn> PawnRef =
        AccessTools.FieldRefAccess<Pawn_DrawTracker, Pawn>("pawn");

    private static readonly Dictionary<int, Vector3> LastVanillaDrawPosByPawnId = new();

    /// <summary>
    /// Clears ephemeral draw-position cache state used to anchor animated pawns.
    /// </summary>
    public static void ResetRuntimeState()
    {
        LastVanillaDrawPosByPawnId.Clear();
        DrawTrackerSuppressionScope.ResetRuntimeState();
    }

    public static void ClearCache()
    {
        ResetRuntimeState();
    }

    public static void Postfix(Pawn_DrawTracker __instance, ref Vector3 __result)
    {
        if (!DrawTrackerSuppressionScope.Active && !VisualActivityTracker.AnyExtendedAnimatorsActive) return;

        Pawn pawn = PawnRef(__instance);
        if (pawn != null)
        {
            LastVanillaDrawPosByPawnId[pawn.thingIDNumber] = __result;
        }

        if (DrawTrackerSuppressionScope.Active) return;
        if (pawn == null) return;
        if (pawn.RaceProps?.Humanlike != true) return;

        CompExtendedAnimator animator = pawn.TryGetComp<CompExtendedAnimator>();
        if (animator?.hasAnimPlaying != true) return;

        Thing anchor = animator.anchor;
        if (anchor == null || anchor == pawn) return;

        Vector3 anchorPos;
        if (anchor is Pawn anchorPawn)
        {
            if (!LastVanillaDrawPosByPawnId.TryGetValue(anchorPawn.thingIDNumber, out anchorPos))
            {
                using (DrawTrackerSuppressionScope.Enter())
                {
                    anchorPos = anchorPawn.DrawPos;
                }
            }
        }
        else
        {
            anchorPos = anchor.DrawPos;
        }

        __result.x = anchorPos.x;
        __result.z = anchorPos.z;
    }
}
