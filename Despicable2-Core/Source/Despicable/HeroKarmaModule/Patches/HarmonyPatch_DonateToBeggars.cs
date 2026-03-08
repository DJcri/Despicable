using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using Verse;
using Verse.AI;

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
        if (!__result || !HKSettingsUtil.HookEnabled("DonateToBeggars")) return;

        try
        {
            if (__instance == null) return;

            Pawn initiator = AccessTools.Field(__instance.GetType(), "pawn")?.GetValue(__instance) as Pawn;
            Job job = AccessTools.Field(__instance.GetType(), "job")?.GetValue(__instance) as Job;

            if (initiator == null || job == null) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;

            Pawn recipient = TryGetRecipientPawn(job);
            if (!HKHookUtil.IsLikelyBeggarsQuestPawn(recipient)) return;

            int amount = TryGetDonationAmount(job);
            if (amount <= 0) return;

            int factionId = HKHookUtil.GetFactionIdSafe(recipient);

            using (HKGoodwillContext.Enter(initiator))
            {
                var ev = KarmaEvent.Create("DonateToBeggars", initiator, recipient, factionId, stage: amount, amount: amount);
                TryAssignSettlementContext(ev, recipient, initiator);
                HKKarmaProcessor.Process(ev);
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_DonateToBeggars:1", "HarmonyPatch_DonateToBeggars suppressed an exception.", ex);
        }
    }

    private static Pawn TryGetRecipientPawn(Job job)
    {
        if (job == null) return null;

        return job.GetTarget(TargetIndex.B).Thing as Pawn
            ?? job.GetTarget(TargetIndex.A).Thing as Pawn
            ?? job.GetTarget(TargetIndex.C).Thing as Pawn;
    }

    private static int TryGetDonationAmount(Job job)
    {
        if (job == null) return 0;

        Thing donationThing = ExtractDonationThing(job);
        if (donationThing == null) return 0;

        int count = donationThing.stackCount > 0 ? donationThing.stackCount : 1;
        if (job.count > 0 && job.count < count)
            count = job.count;

        float total = donationThing.MarketValue * count;
        if (total <= 0f) return 0;

        return (int)Math.Round(total, MidpointRounding.AwayFromZero);
    }


    private static void TryAssignSettlementContext(KarmaEvent ev, Pawn primaryPawn, Pawn fallbackPawn)
    {
        if (ev == null) return;

        Settlement settlement = TryGetSettlementFromPawn(primaryPawn) ?? TryGetSettlementFromPawn(fallbackPawn);
        if (settlement == null || settlement.Faction == null || settlement.Faction.IsPlayer) return;

        ev.settlementUniqueId = settlement.GetUniqueLoadID();
        ev.settlementLabel = settlement.LabelCap;
    }

    private static Settlement TryGetSettlementFromPawn(Pawn pawn)
    {
        Map map = pawn?.MapHeld ?? pawn?.Map;
        return map?.Parent as Settlement;
    }

    private static Thing ExtractDonationThing(Job job)
    {
        Thing a = job.GetTarget(TargetIndex.A).Thing;
        if (a != null && a is not Pawn) return a;

        Thing b = job.GetTarget(TargetIndex.B).Thing;
        if (b != null && b is not Pawn) return b;

        Thing c = job.GetTarget(TargetIndex.C).Thing;
        if (c != null && c is not Pawn) return c;

        return null;
    }
}
