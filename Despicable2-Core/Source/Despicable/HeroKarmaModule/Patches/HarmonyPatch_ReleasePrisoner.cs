using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

// Guardrail-Reason: Release-prisoner hooks stay co-located because release tracking, prisoner social influence, and goodwill attribution share one gameplay seam.
namespace Despicable.HeroKarma.Patches.HeroKarma;

/// <summary>
/// RimWorld 1.6 performs prisoner release through JobDriver_ReleasePrisoner; the pawn typically walks off the map later.
/// Patching the reservation step gives us both the warden and the prisoner on a stable, version-resilient method.
/// This tracks an intentional release start rather than the exact final toil, which is a practical compromise.
/// </summary>
[HarmonyPatch(typeof(JobDriver_ReleasePrisoner), nameof(JobDriver_ReleasePrisoner.TryMakePreToilReservations))]
public static class HarmonyPatch_ReleasePrisoner
{
    private const string PatchId = "HKPatch.ReleasePrisoner";
    // Guardrail-Allow-Static: Cached Harmony target for this patch class; resolved during Prepare and reused for version-safe patch diagnostics during load.
    private static MethodBase _guardTarget;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        MethodBase m = AccessTools.Method(typeof(JobDriver_ReleasePrisoner), nameof(JobDriver_ReleasePrisoner.TryMakePreToilReservations));
        return HKPatchGuard.PrepareSingle(
            PatchId,
            "Release prisoner (TryMakePreToilReservations)",
            featureKey: "CoreKarma",
            required: true,
            target: m,
            cached: out _guardTarget);
    }

    public static void Postfix(JobDriver_ReleasePrisoner __instance, bool __result)
    {
        if (!__result || !HKSettingsUtil.HookEnabled("ReleasePrisoner")) return;

        try
        {
            Pawn initiator = __instance?.pawn;
            if (__instance?.job?.GetTarget(TargetIndex.A).Thing is not Pawn recipient || initiator == null) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;

            // Remember who initiated the release so goodwill gained on exit can be attributed.
            HKReleaseContext.RememberRelease(initiator, recipient);

            int factionId = HKHookUtil.GetFactionIdSafe(recipient);
            var ev = KarmaEvent.Create("ReleasePrisoner", initiator, recipient, factionId);

            HKSettlementContextUtil.TryAssignFromPawns(ev, initiator);

            HKKarmaProcessor.Process(ev);

            if (HKPerkEffects.HasMercyMagnet(initiator))
            {
                HKHookUtil.TryAwardBonusGoodwill(initiator, recipient, HKBalanceTuning.PerkBehavior.MercyMagnetReleaseBonusGoodwill, "PerkMercyMagnet_Release", HKBalanceTuning.PerkBehavior.MercyMagnetReleaseCooldownTicks);
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_ReleasePrisoner:1", "HarmonyPatch_ReleasePrisoner suppressed an exception.", ex);
        }
    }
}

/// <summary>
/// Local Rep hooks for prisoner recruit/convert interactions.
/// </summary>
[HarmonyPatch]
public static class HarmonyPatch_PrisonerSocialInfluence
{
    private const string PatchId = "HKPatch.PrisonerSocialInfluence";
    // Guardrail-Allow-Static: Cached Harmony target list for this patch class; populated during Prepare and reused for patch registration within the current load.
    private static List<MethodBase> _targets;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return HKPatchGuard.PrepareMany(
            PatchId,
            "Prisoner social influence (Local Rep)",
            HKPatchGuard.FeatureLocalRepPrisoners,
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
        string[] names =
        {
            "RimWorld.InteractionWorker_RecruitAttempt",
            "RimWorld.InteractionWorker_ConvertIdeoAttempt"
        };

        return HKPatchTargetUtil.FindFirstMethods(names, "Interacted");
    }

    public static void Postfix(Pawn initiator, Pawn recipient, MethodBase __originalMethod)
    {
        try
        {
            if (initiator == null || recipient == null) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;


            string actorId = initiator.GetUniqueLoadID();
            string targetId = recipient.GetUniqueLoadID();
            int factionId = HKHookUtil.GetFactionIdSafe(recipient);
            string workerName = __originalMethod?.DeclaringType?.Name ?? string.Empty;

            bool isRecruit = workerName.IndexOf("Recruit", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isConvert = workerName.IndexOf("Convert", StringComparison.OrdinalIgnoreCase) >= 0;

            bool hasSilverTongue = HKPerkEffects.HasSilverTongue(initiator);

            // Existing perk: Silver Tongue (strong, direct).
            if (isRecruit && hasSilverTongue
                && HKEventDebouncer.ShouldProcess("PerkSilverTongueRecruit", actorId, targetId, factionId, 0, 2500, 10000, 1))
            {
                HKHookUtil.TryOffsetPrisonerResistance(recipient, HKBalanceTuning.PerkBehavior.SilverTongueRecruitResistanceOffset);
            }

            if (isConvert && hasSilverTongue
                && HKEventDebouncer.ShouldProcess("PerkSilverTongueConvert", actorId, targetId, factionId, 0, 2500, 10000, 1))
            {
                HKHookUtil.TryOffsetIdeoCertainty(recipient, HKBalanceTuning.PerkBehavior.SilverTongueConvertCertaintyOffset);
            }

            // Local Rep: gentle bias based on this prisoner's lived memory of the Hero.
            if (!HKSettingsUtil.EnableLocalRep || !HKSettingsUtil.LocalRepInfluencePrisoners) return;

            if (!LocalReputationUtility.TryGetPawnInfluenceIndex(actorId, targetId, out float r))
                return;

            // Shared debouncer so recruit + convert calls in the same tick don't stack twice.
            if (!HKEventDebouncer.ShouldProcess("LocalRep_PrisonerInfluence", actorId, targetId, factionId, 0, 2500, 10000, 1))
                return;

            float mult = hasSilverTongue ? LocalRepTuning.PrisonerSilverTongueMult : 1f;

            if (isRecruit)
            {
                float deltaRes = Mathf.Clamp((LocalRepTuning.RecruitCoeff * r) * mult, LocalRepTuning.RecruitClampMin, LocalRepTuning.RecruitClampMax);
                if (Math.Abs(deltaRes) > 0.0001f)
                    HKHookUtil.TryOffsetPrisonerResistance(recipient, deltaRes);
            }
            else if (isConvert)
            {
                float deltaCert = Mathf.Clamp((LocalRepTuning.ConvertCoeff * r) * mult, LocalRepTuning.ConvertClampMin, LocalRepTuning.ConvertClampMax);
                if (Math.Abs(deltaCert) > 0.00001f)
                    HKHookUtil.TryOffsetIdeoCertainty(recipient, deltaCert);
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_PrisonerSocialInfluence:1", "HarmonyPatch_PrisonerSocialInfluence suppressed an exception.", ex);
        }
    }
}


/// <summary>
/// Short-lived mapping to attribute goodwill gains on exit to the pawn who initiated the release.
/// JobDriver_ReleasePrisoner has both pawns available early; the actual goodwill change often occurs later.
/// </summary>
internal static class HKReleaseContext
{
    // Keep short to avoid stale attribution if something else triggers PrisonerRelease later.
    private const int ExpireTicks = 6000;

    private sealed class Entry
    {
        public string InstigatorPawnId;
        public int SetTick;
    }

    private static readonly Dictionary<string, Entry> InstigatorByPrisonerId = new();

    public static void RememberRelease(Pawn instigator, Pawn prisoner)
    {
        if (instigator == null || prisoner == null) return;

        try
        {
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            InstigatorByPrisonerId[prisoner.GetUniqueLoadID()] = new Entry
            {
                InstigatorPawnId = instigator.GetUniqueLoadID(),
                SetTick = now
            };
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HKReleaseContext:1",
                "HKReleaseContext suppressed an exception.",
                ex);
        }
    }

    public static Pawn TryConsumeInstigator(Pawn prisoner)
    {
        if (prisoner == null) return null;

        try
        {
            string key = prisoner.GetUniqueLoadID();
            if (key.NullOrEmpty()) return null;

            if (!InstigatorByPrisonerId.TryGetValue(key, out Entry entry) || entry == null)
                return null;

            InstigatorByPrisonerId.Remove(key);

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (now - entry.SetTick > ExpireTicks)
                return null;

            return HKResolve.TryResolvePawnById(entry.InstigatorPawnId);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HKReleaseContext:2",
                "HKReleaseContext suppressed an exception.",
                ex);
            return null;
        }
    }

    public static void ResetRuntimeState()
    {
        InstigatorByPrisonerId.Clear();
    }
}

/// <summary>
/// Attributes goodwill rewards granted when a released prisoner (or freed slave) actually exits the map
/// to the pawn who initiated the release.
/// 
/// RimWorld 1.6 does NOT immediately call ExitMap from GenGuest.PrisonerRelease; the pawn typically walks off
/// the map later. The goodwill gain is awarded from Faction.Notify_MemberExitedMap when the pawn leaves.
/// </summary>
[HarmonyPatch]
internal static class HarmonyPatch_Faction_Notify_MemberExitedMap_GoodwillContext
{
    private const string PatchId = "HKPatch.Faction.Notify_MemberExitedMap.ReleaseGoodwillContext";
    // Guardrail-Allow-Static: Cached Harmony target for this patch/helper; resolved during Prepare and reused for the current load.
    private static MethodBase _target;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        MethodBase m = null;
        try
        {
            m = AccessTools.Method(typeof(Faction), nameof(Faction.Notify_MemberExitedMap), new[] { typeof(Pawn), typeof(bool) })
                ?? AccessTools.Method(typeof(Faction), nameof(Faction.Notify_MemberExitedMap));
        }
        catch { /* handled by PatchGuard */ }

        return HKPatchGuard.PrepareSingle(
            PatchId,
            "Prisoner release goodwill attribution (Faction.Notify_MemberExitedMap)",
            featureKey: "CoreKarma",
            required: false,
            target: m,
            cached: out _target);
    }

    private static MethodBase TargetMethod() => _target;

    private static void Prefix(Pawn __0, bool __1, ref HKGoodwillContext.Scope __state)
    {
        __state = default;

        try
        {
            // Only act on "freed" exits (released prisoner/slave).
            if (!__1) return;

            Pawn member = __0;
            if (member == null) return;

            Pawn instigator = HKReleaseContext.TryConsumeInstigator(member);
            if (instigator == null) return;
            if (!HKHookUtilSafe.ActorIsHero(instigator)) return;

            __state = HKGoodwillContext.Enter(instigator);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_Faction_Notify_MemberExitedMap:1",
                "HarmonyPatch_Faction_Notify_MemberExitedMap suppressed an exception.",
                ex);
        }
    }

    private static void Finalizer(Exception __exception, HKGoodwillContext.Scope __state)
    {
        try { __state.Dispose(); }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_Faction_Notify_MemberExitedMap:2",
                "HarmonyPatch_Faction_Notify_MemberExitedMap suppressed an exception.",
                ex);
        }
    }
}

