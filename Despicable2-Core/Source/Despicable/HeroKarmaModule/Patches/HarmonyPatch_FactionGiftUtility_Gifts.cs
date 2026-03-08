using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

// Guardrail-Reason: Charity gift hooks stay co-located because negotiator capture, goodwill attribution, and post-gift processing share one gameplay seam.
namespace Despicable.HeroKarma.Patches.HeroKarma;
/// <summary>
/// RimWorld 1.6 charity gifting hooks.
///
/// Exact patch sites provided by user (Assembly-CSharp 1.6.9438.37837):
/// - RimWorld.Planet.FactionGiftUtility.OfferGiftsCommand(Caravan, Settlement)
/// - RimWorld.Planet.FactionGiftUtility.GiveGift(List<Tradeable>, Faction, GlobalTargetInfo)
/// - RimWorld.Planet.FactionGiftUtility.GiveGift(List<ActiveTransporterInfo>, Settlement)
/// </summary>
public static class HKGiftContext
{
    private static readonly GiftInvocationState RuntimeState = new();

    // Keep short to avoid accidental attribution if something else calls GiveGift later.
    private const int ExpireTicks = 600;

    public static void Set(Pawn pawn)
    {
        if (!HKSettingsUtil.HookEnabled("CharityGift"))
        {
            return;
        }

        if (pawn == null)
        {
            return;
        }

        RuntimeState.LastGifterPawnId = pawn.GetUniqueLoadID();
        RuntimeState.LastSetTick = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
    }

    public static void SetSettlement(Settlement settlement)
    {
        RuntimeState.LastSettlementUniqueId = settlement != null ? settlement.GetUniqueLoadID() : null;
        RuntimeState.LastSettlementLabel = settlement != null ? settlement.LabelCap : null;
    }

    public static Pawn GetValidPawn()
    {
        if (RuntimeState.LastGifterPawnId.NullOrEmpty())
        {
            return null;
        }

        int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        if (now - RuntimeState.LastSetTick > ExpireTicks)
        {
            Clear();
            return null;
        }

        return HKResolve.TryResolvePawnById(RuntimeState.LastGifterPawnId);
    }

    public static string GetValidSettlementUniqueId()
    {
        int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        if (now - RuntimeState.LastSetTick > ExpireTicks)
        {
            Clear();
            return null;
        }

        return RuntimeState.LastSettlementUniqueId;
    }

    public static string GetValidSettlementLabel()
    {
        int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        if (now - RuntimeState.LastSetTick > ExpireTicks)
        {
            Clear();
            return null;
        }

        return RuntimeState.LastSettlementLabel;
    }

    public static void ResetRuntimeState()
    {
        RuntimeState.LastGifterPawnId = null;
        RuntimeState.LastSettlementUniqueId = null;
        RuntimeState.LastSettlementLabel = null;
        RuntimeState.LastSetTick = 0;
    }

    public static void Clear()
    {
        ResetRuntimeState();
    }

    private sealed class GiftInvocationState
    {
        public string LastGifterPawnId;
        public string LastSettlementUniqueId;
        public string LastSettlementLabel;
        public int LastSetTick;
    }
}

[HarmonyPatch(typeof(FactionGiftUtility), nameof(FactionGiftUtility.OfferGiftsCommand))]
public static class HarmonyPatch_FactionGiftUtility_OfferGiftsCommand
{
    private const string PatchId = "HKPatch.CharityGift.OfferGiftsCommand";
    // Guardrail-Allow-Static: Cached Harmony target for this patch class; resolved during Prepare and reused for version-safe patch diagnostics during load.
    private static MethodBase _guardTarget;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        MethodBase m = AccessTools.Method(typeof(FactionGiftUtility), nameof(FactionGiftUtility.OfferGiftsCommand));
        return HKPatchGuard.PrepareSingle(
            PatchId,
            "Charity gift: capture negotiator (OfferGiftsCommand)",
            featureKey: "CoreKarma",
            required: false,
            target: m,
            cached: out _guardTarget);
    }

    public static void Postfix(Caravan caravan, Settlement settlement, ref Command __result)
    {
        try
        {
            HKGiftContext.SetSettlement(settlement);
            HKGiftPatchHelpers.TryWrapOfferCommand(__result, caravan);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_FactionGiftUtility_Gifts:102",
                "HarmonyPatch_FactionGiftUtility_Gifts suppressed an exception.",
                ex);
        }
    }
}

[HarmonyPatch(typeof(FactionGiftUtility), nameof(FactionGiftUtility.GiveGift), new Type[] { typeof(List<Tradeable>), typeof(Faction), typeof(GlobalTargetInfo) })]
public static class HarmonyPatch_FactionGiftUtility_GiveGift_Tradeables
{
    private const string PatchId = "HKPatch.CharityGift.GiveGift.Tradeables";
    // Guardrail-Allow-Static: Cached Harmony target for this patch class; resolved during Prepare and reused for version-safe patch diagnostics during load.
    private static MethodBase _guardTarget;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        MethodBase m = AccessTools.Method(typeof(FactionGiftUtility), nameof(FactionGiftUtility.GiveGift), new Type[] { typeof(List<Tradeable>), typeof(Faction), typeof(GlobalTargetInfo) });
        return HKPatchGuard.PrepareSingle(
            PatchId,
            "Charity gift (tradeables) (FactionGiftUtility.GiveGift)",
            featureKey: "CoreKarma",
            required: true,
            target: m,
            cached: out _guardTarget);
    }

    // This matches: GiveGift(List<Tradeable> tradeables, Faction giveTo, GlobalTargetInfo lookTarget)
    public static void Prefix(ref bool __state)
    {
        __state = false;
        try
        {
            __state = HKGiftPatchHelpers.TryBeginHeroGoodwillForGift();
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_FactionGiftUtility_Gifts:1",
                "HarmonyPatch_FactionGiftUtility_Gifts suppressed an exception.",
                ex);
        }
    }

    public static void Postfix(List<Tradeable> tradeables, Faction giveTo, GlobalTargetInfo lookTarget)
    {
        try
        {
            HKGiftPatchHelpers.ProcessTradeableGift(tradeables, giveTo, lookTarget);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_FactionGiftUtility_Gifts:104",
                "HarmonyPatch_FactionGiftUtility_Gifts suppressed an exception.",
                ex);
        }
    }

    public static void Finalizer(Exception __exception, bool __state)
    {
        if (!__state)
        {
            return;
        }

        try
        {
            HKGoodwillContext.End();
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_FactionGiftUtility_Gifts:2",
                "HarmonyPatch_FactionGiftUtility_Gifts suppressed an exception.",
                ex);
        }
    }
}

[HarmonyPatch(typeof(FactionGiftUtility), nameof(FactionGiftUtility.GiveGift), new Type[] { typeof(List<ActiveTransporterInfo>), typeof(Settlement) })]
public static class HarmonyPatch_FactionGiftUtility_GiveGift_Pods
{
    private const string PatchId = "HKPatch.CharityGift.GiveGift.Pods";
    // Guardrail-Allow-Static: Cached Harmony target for this patch class; resolved during Prepare and reused for version-safe patch diagnostics during load.
    private static MethodBase _guardTarget;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        MethodBase m = AccessTools.Method(typeof(FactionGiftUtility), nameof(FactionGiftUtility.GiveGift), new Type[] { typeof(List<ActiveTransporterInfo>), typeof(Settlement) });
        return HKPatchGuard.PrepareSingle(
            PatchId,
            "Charity gift (pods) (FactionGiftUtility.GiveGift)",
            featureKey: "CoreKarma",
            required: true,
            target: m,
            cached: out _guardTarget);
    }

    // This matches: GiveGift(List<ActiveTransporterInfo> pods, Settlement giveTo)
public static void Prefix(ref bool __state)
{
    __state = false;
    try
    {
        if (!HKSettingsUtil.HookEnabled("CharityGift")) return;

        Pawn hero = HKRuntime.GetHeroPawnSafe();
        if (hero == null) return;

        // Treat pod gifting as hero-attributed player intent (no explicit instigator pawn is carried down).
        HKGoodwillContext.Begin(hero);
        __state = true;
    }
    catch (Exception ex)
    {
        Despicable.Core.DebugLogger.WarnExceptionOnce(
            "HarmonyPatch_FactionGiftUtility_Gifts:201",
            "HarmonyPatch_FactionGiftUtility_Gifts suppressed an exception.",
            ex);
    }
}

public static void Postfix(List<ActiveTransporterInfo> pods, Settlement giveTo)
    {
        try
        {
            HKGiftPatchHelpers.ProcessPodGift(pods, giveTo);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_FactionGiftUtility_Gifts:105",
                "HarmonyPatch_FactionGiftUtility_Gifts suppressed an exception.",
                ex);
        }
    }


public static void Finalizer(Exception __exception, bool __state)
{
    if (!__state)
    {
        return;
    }

    try
    {
        HKGoodwillContext.End();
    }
    catch (Exception ex)
    {
        Despicable.Core.DebugLogger.WarnExceptionOnce(
            "HarmonyPatch_FactionGiftUtility_Gifts:202",
            "HarmonyPatch_FactionGiftUtility_Gifts suppressed an exception.",
            ex);
    }
}
}
