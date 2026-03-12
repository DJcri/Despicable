using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Verse.AI;
using System.Reflection;

namespace Despicable.HeroKarma.Patches.HeroKarma;

/// <summary>
/// Tracks deliberate slave emancipation without taking a hard dependency on any DLC-specific class.
/// We patch whichever job driver exists in the current environment.
/// </summary>
[HarmonyPatch]
public static class HarmonyPatch_FreeSlave
{
    private const string PatchId = "HKPatch.FreeSlave";

    private static readonly string[] CandidateTypeNames =
    {
        "Verse.AI.JobDriver_EmancipateSlave",
        "Verse.AI.JobDriver_ReleaseSlave",
        "Verse.AI.JobDriver_FreeSlave",
        "RimWorld.JobDriver_EmancipateSlave",
        "RimWorld.JobDriver_ReleaseSlave",
        "RimWorld.JobDriver_FreeSlave"
    };

    // Guardrail-Allow-Static: Cached Harmony target list for this patch class; populated during Prepare and reused for patch registration within the current load.
    private static List<MethodBase> _targets;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return HKPatchGuard.PrepareMany(
            PatchId,
            "Free slave (job driver)",
            featureKey: "CoreKarma",
            required: false,
            candidates: FindTargets(),
            cached: out _targets);
    }

    [HarmonyTargetMethods]
    static IEnumerable<MethodBase> TargetMethods()
    {
        return _targets ?? (IEnumerable<MethodBase>)Array.Empty<MethodBase>();
    }

    private static IEnumerable<MethodBase> FindTargets()
    {
        return HKPatchTargetUtil.FindFirstMethods(CandidateTypeNames, "TryMakePreToilReservations");
    }

    public static void Postfix(object __instance, bool __result)
    {
        if (!__result || !HKSettingsUtil.HookEnabled("FreeSlave")) return;

        try
        {
            if (__instance == null) return;

            if (!HKJobDriverUtil.TryGetActorAndJob(__instance, out Pawn initiator, out Job job)) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;

            Pawn target = HKJobDriverUtil.TryGetPawnTarget(job, TargetIndex.A, TargetIndex.B);
            if (target == null) return;
            if (!HKHookUtil.IsSlaveLike(target)) return;

            int factionId = HKHookUtil.GetFactionIdSafe(target);

            
            using (HKGoodwillContext.Enter(initiator))
            {
                var ev = KarmaEvent.Create("FreeSlave", initiator, target, factionId);

                HKSettlementContextUtil.TryAssignFromPawns(ev, initiator);

                HKKarmaProcessor.Process(ev);
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_FreeSlave:1", "HarmonyPatch_FreeSlave suppressed an exception.", ex);
        }
    }
}
