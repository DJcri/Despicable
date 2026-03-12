using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using System.Reflection;

namespace Despicable.HeroKarma.Patches.HeroKarma;

/// <summary>
/// Tracks selling captive pawns, including prisoners and slaves, via the base-game trade deal execution path.
/// This keeps the hook available without any DLC-specific dependency.
/// </summary>
[HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
public static class HarmonyPatch_SellCaptive
{
    private const string PatchId = "HKPatch.SellCaptive";
    // Guardrail-Allow-Static: Cached Harmony target for this patch class; resolved during Prepare and reused for version-safe patch diagnostics during load.
    private static MethodBase _guardTarget;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        MethodBase m = AccessTools.Method(typeof(TradeDeal), nameof(TradeDeal.TryExecute));
        return HKPatchGuard.PrepareSingle(
            PatchId,
            "Sell captive (TradeDeal.TryExecute)",
            featureKey: "CoreKarma",
            required: true,
            target: m,
            cached: out _guardTarget);
    }

    public sealed class CaptiveSaleRecord
    {
        public Pawn pawn;
        public int factionId;
    }

    public sealed class CaptiveSaleState
    {
        public Pawn actor;
        public List<CaptiveSaleRecord> captivesSold;
        public string settlementUniqueId;
        public string settlementLabel;
    }

    public static void Prefix(TradeDeal __instance, ref CaptiveSaleState __state)
    {
        __state = null;
        if (!HKSettingsUtil.HookEnabled("SellCaptive") || __instance == null) return;

        try
        {
            Pawn negotiator = HKTradeSessionUtil.TryGetPlayerNegotiator();
            if (negotiator == null || !HKHookUtilSafe.ActorIsHero(negotiator)) return;

            List<CaptiveSaleRecord> captivesSold = CaptureSoldCaptives(__instance);
            if (captivesSold.Count == 0) return;

            __state = new CaptiveSaleState
            {
                actor = negotiator,
                captivesSold = captivesSold,
                settlementUniqueId = HKTradeSessionUtil.TryGetCurrentSettlementUniqueId("HarmonyPatch_SellCaptive:3", "HarmonyPatch_SellCaptive failed to resolve the current trade settlement."),
                settlementLabel = HKTradeSessionUtil.TryGetCurrentSettlementLabel("HarmonyPatch_SellCaptive:3", "HarmonyPatch_SellCaptive failed to resolve the current trade settlement.")
            };
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_SellCaptive:1", "HarmonyPatch_SellCaptive suppressed an exception.", ex);
        }
    }

    public static void Postfix(bool __result, CaptiveSaleState __state)
    {
        if (!__result || __state == null || __state.actor == null || __state.captivesSold == null || __state.captivesSold.Count == 0)
            return;

        try
        {
            for (int i = 0; i < __state.captivesSold.Count; i++)
            {
                CaptiveSaleRecord sale = __state.captivesSold[i];
                if (sale == null || sale.pawn == null) continue;

                
                var ev = KarmaEvent.Create("SellCaptive", __state.actor, sale.pawn, sale.factionId);
                ev.settlementUniqueId = __state.settlementUniqueId;
                ev.settlementLabel = __state.settlementLabel;
                HKKarmaProcessor.Process(ev);
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_SellCaptive:2", "HarmonyPatch_SellCaptive suppressed an exception.", ex);
        }
    }

    private static List<CaptiveSaleRecord> CaptureSoldCaptives(TradeDeal deal)
    {
        var results = new List<CaptiveSaleRecord>();
        List<Tradeable> tradeables = TryGetTradeables(deal);
        if (tradeables == null) return results;

        for (int i = 0; i < tradeables.Count; i++)
        {
            Tradeable tradeable = tradeables[i];
            if (tradeable == null || tradeable.ActionToDo != TradeAction.PlayerSells)
                continue;

            int count = Math.Min(tradeable.CountToTransferToDestination, tradeable.CountHeldBy(Transactor.Colony));
            if (count <= 0)
                continue;

            if (tradeable.AnyThing is not Pawn pawn)
                continue;

            if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
                continue;

            if (!HKHookUtil.IsPrisonerLike(pawn) && !HKHookUtil.IsSlaveLike(pawn))
                continue;

            results.Add(new CaptiveSaleRecord
            {
                pawn = pawn,
                factionId = HKHookUtil.GetFactionIdSafe(pawn)
            });
        }

        return results;
    }

    private static List<Tradeable> TryGetTradeables(TradeDeal deal)
    {
        if (deal == null) return null;

        object value = AccessTools.Field(deal.GetType(), "tradeables")?.GetValue(deal);
        if (value is List<Tradeable> fieldList) return fieldList;

        PropertyInfo prop = AccessTools.Property(deal.GetType(), "Tradeables");
        if (prop != null && prop.GetValue(deal, null) is List<Tradeable> propList) return propList;

        return null;
    }

}
