using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Verse.AI;
using System.Reflection;

namespace Despicable.HeroKarma.Patches.HeroKarma;

/// <summary>
/// Best-effort beggar donation hook for 1.6.
/// Uses generic give-to-pawn job drivers, then filters to likely beggars by quest tags.
/// </summary>
[HarmonyPatch]
public static class HarmonyPatch_DonateToBeggars
{
    private const string PatchId = "HKPatch.DonateToBeggars";

    private static readonly string[] CandidateTypeNames =
    {
        "Verse.AI.JobDriver_GiveToPawn",
        "Verse.AI.JobDriver_DeliverToPawn",
        "Verse.AI.JobDriver_GiveToPackAnimal",
        "RimWorld.JobDriver_GiveToPawn",
        "RimWorld.JobDriver_DeliverToPawn",
        "RimWorld.JobDriver_GiveToPackAnimal"
    };

    // Guardrail-Allow-Static: Cached Harmony target list for this patch class; populated during Prepare and reused for patch registration within the current load.
    private static List<MethodBase> _targets;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return HKPatchGuard.PrepareMany(
            PatchId,
            "Donate to beggars",
            featureKey: "CoreKarma",
            required: false,
            candidates: FindTargets(),
            cached: out _targets);
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return _targets ?? (IEnumerable<MethodBase>)Array.Empty<MethodBase>();
    }

    private static IEnumerable<MethodBase> FindTargets()
    {
        return HKPatchTargetUtil.FindFirstMethods(CandidateTypeNames, "TryMakePreToilReservations");
    }

    public static void Postfix(object __instance, bool __result)
    {
        if (!__result || !HKSettingsUtil.HookEnabled("DonateToBeggars")) return;

        try
        {
            if (__instance == null) return;

            if (!HKJobDriverUtil.TryGetActorAndJob(__instance, out Pawn initiator, out Job job)) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;

            Pawn recipient = HKJobDriverUtil.TryGetPawnTarget(job, TargetIndex.B, TargetIndex.A, TargetIndex.C);
            if (!HKHookUtil.IsLikelyBeggarsQuestPawn(recipient)) return;

            int amount = TryGetDonationAmount(job);
            if (amount <= 0) return;

            int factionId = HKHookUtil.GetFactionIdSafe(recipient);

            using (HKGoodwillContext.Enter(initiator))
            {
                var ev = KarmaEvent.Create("DonateToBeggars", initiator, recipient, factionId, stage: amount, amount: amount);
                HKSettlementContextUtil.TryAssignFromPawns(ev, recipient, initiator);
                HKKarmaProcessor.Process(ev);
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_DonateToBeggars:1", "HarmonyPatch_DonateToBeggars suppressed an exception.", ex);
        }
    }


    private static int TryGetDonationAmount(Job job)
    {
        if (job == null) return 0;

        Thing donationThing = HKJobDriverUtil.TryGetNonPawnThingTarget(job, TargetIndex.A, TargetIndex.B, TargetIndex.C);
        if (donationThing == null) return 0;

        int count = donationThing.stackCount > 0 ? donationThing.stackCount : 1;
        if (job.count > 0 && job.count < count)
            count = job.count;

        float total = donationThing.MarketValue * count;
        if (total <= 0f) return 0;

        return (int)Math.Round(total, MidpointRounding.AwayFromZero);
    }




}
