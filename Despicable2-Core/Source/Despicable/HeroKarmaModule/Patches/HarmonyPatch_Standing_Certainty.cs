using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

/// <summary>
/// Standing effect: modifies certainty changes for the Hero.
/// This biases faith stability without creating a new perk tree.
/// </summary>
[HarmonyPatch]
internal static class HarmonyPatch_Standing_Certainty
{
    private const string PatchId = "HKPatch.StandingCertainty";

    // Guardrail-Allow-Static: Cached reflected pawn field for this standing patch; lazily re-resolved and reused as a reflection cache during the current load.
    private static FieldInfo _pawnField;
    // Guardrail-Allow-Static: Cached Harmony target for this patch/helper; resolved during Prepare and reused for the current load.
    private static MethodBase _target;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        if (!HKIdeologyCompat.IsAvailable)
        {
            HKPatchGuard.MarkSkipped(PatchId,
                "Standing certainty (faith stability)",
                HKPatchGuard.FeatureStandingEffects,
                required: true,
                reason: "Ideology is not active.");
            return false;
        }

        return HKPatchGuard.PrepareSingle(
            PatchId,
            "Standing certainty (faith stability)",
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
        var t = AccessTools.TypeByName("RimWorld.Pawn_IdeoTracker");
        if (t == null) return null;

        // RimWorld 1.6: method name is typically OffsetCertainty(float).
        return AccessTools.Method(t, "OffsetCertainty", new[] { typeof(float) });
    }

    private static void Prefix(object __instance, ref float offset)
    {
        try
        {
            if (offset == 0f) return;
            if (!HKIdeologyCompat.IsStandingEffectsEnabled) return;

            Pawn pawn = TryGetPawn(__instance);
            if (pawn == null) return;
            if (!HKHookUtilSafe.ActorIsHero(pawn)) return;

            int standing = HKRuntime.GetGlobalStanding(pawn);
            if (standing == 0) return;

            float i = HKRuntime.GetInfluenceIndex(standing);

            // ±15% swing at the ceiling.
            if (offset > 0f)
            {
                float mult = Mathf.Clamp(1f + (HKBalanceTuning.StandingCertaintySwing * i), HKBalanceTuning.StandingCertaintyClampMin, HKBalanceTuning.StandingCertaintyClampMax);
                offset *= mult;
            }
            else
            {
                float mult = Mathf.Clamp(1f - (HKBalanceTuning.StandingCertaintySwing * i), HKBalanceTuning.StandingCertaintyClampMin, HKBalanceTuning.StandingCertaintyClampMax);
                offset *= mult;
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_Standing_Certainty",
                "Standing certainty patch suppressed an exception.",
                ex);
        }
    }

    private static Pawn TryGetPawn(object tracker)
    {
        if (tracker == null) return null;

        try
        {
            if (_pawnField == null)
            {
                // Find the first instance field assignable to Pawn.
                var fields = tracker.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                for (int i = 0; i < fields.Length; i++)
                {
                    var f = fields[i];
                    if (f != null && typeof(Pawn).IsAssignableFrom(f.FieldType))
                    {
                        _pawnField = f;
                        break;
                    }
                }
            }

            return _pawnField?.GetValue(tracker) as Pawn;
        }
        catch
        {
            return null;
        }
    }
}
