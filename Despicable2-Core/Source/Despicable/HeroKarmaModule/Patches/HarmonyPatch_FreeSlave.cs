using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using Verse;
using Verse.AI;

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
        var seen = new HashSet<MethodBase>();
        for (int i = 0; i < CandidateTypeNames.Length; i++)
        {
            Type t = AccessTools.TypeByName(CandidateTypeNames[i]);
            if (t == null) continue;

            MethodInfo m = AccessTools.Method(t, "TryMakePreToilReservations");
            if (m != null && seen.Add(m))
                yield return m;
        }
    }

    public static void Postfix(object __instance, bool __result)
    {
        if (!__result || !HKSettingsUtil.HookEnabled("FreeSlave")) return;

        try
        {
            if (__instance == null) return;

            Pawn initiator = AccessTools.Field(__instance.GetType(), "pawn")?.GetValue(__instance) as Pawn;
            Job job = AccessTools.Field(__instance.GetType(), "job")?.GetValue(__instance) as Job;

            if (initiator == null || job == null) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;

            Pawn target = job.GetTarget(TargetIndex.A).Thing as Pawn ?? job.GetTarget(TargetIndex.B).Thing as Pawn;
            if (target == null) return;
            if (!HKHookUtil.IsSlaveLike(target)) return;

            int factionId = HKHookUtil.GetFactionIdSafe(target);

            
            using (HKGoodwillContext.Enter(initiator))
            {
                var ev = KarmaEvent.Create("FreeSlave", initiator, target, factionId);

                if ((initiator.MapHeld ?? initiator.Map)?.Parent is Settlement settlement && settlement.Faction != null && !settlement.Faction.IsPlayer)
                {
                    ev.settlementUniqueId = settlement.GetUniqueLoadID();
                    ev.settlementLabel = settlement.LabelCap;
                }

                HKKarmaProcessor.Process(ev);
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_FreeSlave:1", "HarmonyPatch_FreeSlave suppressed an exception.", ex);
        }
    }
}
