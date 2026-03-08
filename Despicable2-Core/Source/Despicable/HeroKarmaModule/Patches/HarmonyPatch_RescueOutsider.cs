using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Despicable.HeroKarma.Patches.HeroKarma;

/// <summary>
/// Tracks intentional rescue starts of downed outsiders.
/// In RimWorld 1.6 rescue runs through JobDriver_TakeToBed, so we gate on JobDefOf.Rescue.
/// </summary>
[HarmonyPatch(typeof(JobDriver_TakeToBed), nameof(JobDriver_TakeToBed.TryMakePreToilReservations))]
public static class HarmonyPatch_RescueOutsider
{
    private const string PatchId = "HKPatch.RescueOutsider";
    // Guardrail-Allow-Static: Cached Harmony target for this patch class; resolved during Prepare and reused for version-safe patch diagnostics during load.
    private static MethodBase _guardTarget;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        MethodBase m = AccessTools.Method(typeof(JobDriver_TakeToBed), nameof(JobDriver_TakeToBed.TryMakePreToilReservations));
        return HKPatchGuard.PrepareSingle(
            PatchId,
            "Rescue outsider (TryMakePreToilReservations)",
            featureKey: "CoreKarma",
            required: true,
            target: m,
            cached: out _guardTarget);
    }

    public static void Postfix(object __instance, bool __result)
    {
        if (!__result || !HKSettingsUtil.HookEnabled("RescueOutsider")) return;

        try
        {
            if (__instance == null) return;

            Pawn initiator = AccessTools.Field(__instance.GetType(), "pawn")?.GetValue(__instance) as Pawn;
            Job job = AccessTools.Field(__instance.GetType(), "job")?.GetValue(__instance) as Job;

            if (initiator == null || job == null) return;
            if (job.def != JobDefOf.Rescue) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;

            Pawn target = job.GetTarget(TargetIndex.A).Thing as Pawn;
            if (target == null) return;

            if (HKHookUtil.IsAnimal(target)) return;
            if (target.Faction != null && target.Faction.IsPlayer) return;
            if (target.Dead) return;
            if (!target.Downed) return;

            int factionId = HKHookUtil.GetFactionIdSafe(target);

            
            var ev = KarmaEvent.Create("RescueOutsider", initiator, target, factionId);
            HKKarmaProcessor.Process(ev);

            if (HKPerkEffects.HasMercyMagnet(initiator))
            {
                HKHookUtil.TryAwardBonusGoodwill(initiator, target, HKBalanceTuning.PerkBehavior.MercyMagnetRescueBonusGoodwill, "PerkMercyMagnet_Rescue", HKBalanceTuning.PerkBehavior.MercyMagnetRescueCooldownTicks);
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_RescueOutsider:1", "HarmonyPatch_RescueOutsider suppressed an exception.", ex);
        }
    }
}
