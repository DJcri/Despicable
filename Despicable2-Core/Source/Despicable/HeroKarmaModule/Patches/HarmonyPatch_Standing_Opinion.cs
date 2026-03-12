using System;
using HarmonyLib;
using UnityEngine;
using Verse;
using System.Reflection;

namespace Despicable.HeroKarma.Patches.HeroKarma;

/// <summary>
/// Standing effect: same-ideology pawns respect/shun the Hero.
/// Implemented as a small additive modifier to OpinionOf(hero).
/// </summary>
[HarmonyPatch]
internal static class HarmonyPatch_Standing_Opinion
{
    private const string PatchId = "HKPatch.StandingOpinion";

    // Guardrail-Allow-Static: Cached Harmony target for this patch/helper; resolved during Prepare and reused for the current load.
    private static MethodBase _target;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        if (!HKIdeologyCompat.IsAvailable)
        {
            HKPatchGuard.MarkSkipped(PatchId,
                "Standing opinion (same-ideoligion respect/shun)",
                HKPatchGuard.FeatureStandingEffects,
                required: true,
                reason: "Ideology is not active.");
            return false;
        }

        return HKPatchGuard.PrepareSingle(
            PatchId,
            "Standing opinion (same-ideoligion respect/shun)",
            HKPatchGuard.FeatureStandingEffects,
            required: true,
            target: FindTarget(),
            cached: out _target);
    }

    private static MethodBase TargetMethod()
    {
        return _target;
    }

    private static MethodBase FindTarget()
    {
        // RimWorld 1.6: Pawn_RelationsTracker lives in the RimWorld namespace.
        // Keep a fallback to Verse for extra resilience.
        var t = AccessTools.TypeByName("RimWorld.Pawn_RelationsTracker")
                ?? AccessTools.TypeByName("Verse.Pawn_RelationsTracker");
        if (t == null) return null;

        // Preferred signature.
        var m = AccessTools.Method(t, "OpinionOf", new[] { typeof(Pawn) });
        if (m != null) return m;

        // Fallback: find any overload where first arg is Pawn and return type is int.
        foreach (var mi in t.GetMethods(AccessTools.all))
        {
            if (mi == null) continue;
            if (mi.Name != "OpinionOf") continue;
            if (mi.ReturnType != typeof(int)) continue;
            var ps = mi.GetParameters();
            if (ps == null || ps.Length < 1) continue;
            if (ps[0].ParameterType != typeof(Pawn)) continue;
            return mi;
        }

        return null;
    }

    private static void Postfix(object __instance, Pawn other, ref int __result)
    {
        try
        {
            if (other == null) return;
            if (!HKIdeologyCompat.IsStandingEffectsEnabled) return;

            // Only adjust opinions of the hero.
            if (!HKHookUtilSafe.ActorIsHero(other)) return;

            Pawn pawn = PawnOwnerReflectionUtil.TryGetPawn(__instance);
            if (pawn == null) return;
            if (pawn == other) return;

            // Same ideoligion only.
            if (pawn.Ideo == null || other.Ideo == null) return;
            if (pawn.Ideo != other.Ideo) return;

            int standing = HKRuntime.GetGlobalStanding(other);
            if (standing == 0) return;

            float i = HKRuntime.GetInfluenceIndex(standing);
            int delta = Mathf.Clamp(Mathf.RoundToInt(HKBalanceTuning.StandingOpinionMaxDelta * i), -HKBalanceTuning.StandingOpinionMaxDelta, HKBalanceTuning.StandingOpinionMaxDelta);
            if (delta == 0) return;

            __result = Mathf.Clamp(__result + delta, -100, 100);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_Standing_Opinion",
                "Standing opinion patch suppressed an exception.",
                ex);
        }
    }

}
