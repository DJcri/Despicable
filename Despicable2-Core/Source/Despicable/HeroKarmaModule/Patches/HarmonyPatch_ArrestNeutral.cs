using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

/// <summary>
/// Tracks intentional arrest attempts against non-hostile outsiders.
/// RimWorld 1.6 performs the actual acceptance check inside JobDriver_TakeToBed's
/// checkArrestResistance initAction, which calls Pawn.CheckAcceptArrest(...).
/// Patching CheckAcceptArrest gives us the real attempt moment without relying on a
/// separate arrest job driver type that does not exist in 1.6.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.CheckAcceptArrest))]
public static class HarmonyPatch_ArrestNeutral
{
    public static void Postfix(Pawn __instance, Pawn arrester, ref bool __result)
    {
        try
        {
            // Never turn successes into failures.
            if (__result) return;

            Pawn initiator = arrester;
            Pawn target = __instance;
            if (initiator == null || target == null) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;
            if (HKHookUtil.IsAnimal(target) || HKHookUtil.IsPermanentManhunterOrBerserk(target)) return;
            if (target.Faction != null && target.Faction.IsPlayer) return;
            if (target.HostileTo(initiator)) return;
            if (HKBalanceTuning.LocalRepEvents.SuppressArrestNeutralIfTargetGuilty && HKHookUtil.IsCurrentlyGuilty(target)) return;

            bool hasIntimidating = HKPerkEffects.HasIntimidatingPresence(initiator);
            bool hasTerror = HKPerkEffects.HasTerrorEffect(initiator);

            float salvageChance = 0f;

            // Existing perk salvage (kept).
            if (hasIntimidating) salvageChance = Math.Max(salvageChance, HKBalanceTuning.PerkBehavior.IntimidatingPresenceArrestSalvage);
            if (hasTerror) salvageChance = Math.Max(salvageChance, HKBalanceTuning.PerkBehavior.TerrorEffectArrestSalvage);

            // Local Rep salvage: trust can override, fear can synergize with fear perks.
            if (HKSettingsUtil.EnableLocalRep && HKSettingsUtil.LocalRepArrestCompliance)
            {
                string actorId = initiator.GetUniqueLoadID();
                string targetId = target.GetUniqueLoadID();

                if (LocalReputationUtility.TryGetPawnInfluenceIndex(actorId, targetId, out float r))
                {
                    salvageChance = Math.Max(salvageChance, LocalRepTuning.ArrestTrustChance(r));
                    salvageChance += LocalRepTuning.ArrestFearSynergyChance(r, hasIntimidating || hasTerror);
                    salvageChance = LocalRepTuning.ClampArrestChance(salvageChance);
                }
            }

            if (salvageChance <= 0f) return;

            int factionId = HKHookUtil.GetFactionIdSafe(target);
            
            if (Rand.Chance(salvageChance))
                __result = true;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_ArrestNeutral:2", "HarmonyPatch_ArrestNeutral suppressed an exception.", ex);
        }
    }

    public static void Prefix(Pawn __instance, Pawn arrester)
    {
        if (!HKSettingsUtil.HookEnabled("ArrestNeutral")) return;

        try
        {
            Pawn initiator = arrester;
            Pawn target = __instance;

            if (initiator == null || target == null) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;

            // Neutral outsider only.
            if (HKHookUtil.IsAnimal(target) || HKHookUtil.IsPermanentManhunterOrBerserk(target)) return;
            if (target.Faction != null && target.Faction.IsPlayer) return;
            if (target.HostileTo(initiator)) return;
            if (HKBalanceTuning.LocalRepEvents.SuppressArrestNeutralIfTargetGuilty && HKHookUtil.IsCurrentlyGuilty(target)) return;

            int factionId = HKHookUtil.GetFactionIdSafe(target);

            
            var ev = KarmaEvent.Create("ArrestNeutral", initiator, target, factionId);
            HKKarmaProcessor.Process(ev);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_ArrestNeutral:1", "HarmonyPatch_ArrestNeutral suppressed an exception.", ex);
        }
    }
}

/// <summary>
/// Perk helper patch: Community Buffer reduces the sting of insults/slights.
/// Uses dynamic targets so it can be skipped if interaction workers change.
/// </summary>
[HarmonyPatch]
public static class HarmonyPatch_CommunityBufferSocialRecovery
{
    private const string PatchId = "HKPatch.CommunityBufferSocialRecovery";
    // Guardrail-Allow-Static: Cached Harmony target list for this patch class; populated during Prepare and reused for patch registration within the current load.
    private static List<MethodBase> _targets;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return HKPatchGuard.PrepareMany(
            PatchId,
            "Perk: Community Buffer (social recovery)",
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
        string[] names =
        {
            "RimWorld.InteractionWorker_Insult",
            "RimWorld.InteractionWorker_Slight"
        };

        return HKPatchTargetUtil.FindFirstMethods(names, "Interacted");
    }

    public static void Postfix(Pawn initiator, Pawn recipient)
    {
        try
        {
            if (recipient == null) return;
            if (recipient.Faction == null || !recipient.Faction.IsPlayer) return;

            Pawn hero = HKHookUtil.FindNearbyHeroWithPerk(recipient, "HK_Hediff_CommunityBuffer", 12f);
            if (hero == null) return;

            int factionId = HKHookUtil.GetFactionIdSafe(initiator);
            if (!HKEventDebouncer.ShouldProcess("PerkCommunityBuffer", hero.GetUniqueLoadID(), recipient.GetUniqueLoadID(), factionId, 0, 600, 2500, 1))
                return;

            HKHookUtil.TryOffsetNeedMood(recipient, HKBalanceTuning.PerkBehavior.CommunityBufferMoodRecovery);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_CommunityBufferSocialRecovery:1", "HarmonyPatch_CommunityBufferSocialRecovery suppressed an exception.", ex);
        }
    }
}
