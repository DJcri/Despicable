using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using System.Reflection;

namespace Despicable.HeroKarma.Patches.HeroKarma;

/// <summary>
/// Local reputation trade bias.
///
/// RimWorld 1.6 computes final prices in TradeUtility.GetPricePlayerBuy/Sell, applying
/// the faction/settlement base gain via the parameter priceGain_FactionBase.
/// We patch those methods directly and nudge that parameter based on Local Rep,
/// so the behavior stays consistent across call sites (Tradeable, dialogs, etc).
/// </summary>
[HarmonyPatch]
internal static class HarmonyPatch_TradePrice_LocalRep
{
    private const string PatchId = "HKPatch.TradePriceLocalRep";
    private static readonly string HediffReputationTax = "HK_Hediff_ReputationTax";

    // Guardrail-Allow-Static: Cached Harmony target list for this patch class; populated during Prepare and reused for patch registration within the current load.
    private static List<MethodBase> _targets;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return HKPatchGuard.PrepareMany(
            PatchId,
            "Trade pricing (Local Rep)",
            HKPatchGuard.FeatureLocalRepTrade,
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
        // RimWorld 1.6 signatures (see ILSpy):
        // GetPricePlayerBuy(Thing, float, float, float, float)
        // GetPricePlayerSell(Thing, float, float, float, float, float, float, TradeCurrency)

        var buy = AccessTools.Method(
            typeof(TradeUtility),
            nameof(TradeUtility.GetPricePlayerBuy),
            new[] { typeof(Thing), typeof(float), typeof(float), typeof(float), typeof(float) });

        if (buy != null) yield return buy;

        var sell = AccessTools.Method(
            typeof(TradeUtility),
            nameof(TradeUtility.GetPricePlayerSell),
            new[] { typeof(Thing), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(float), typeof(TradeCurrency) });

        if (sell != null) yield return sell;
    }

    private static void Prefix([HarmonyArgument(4)] ref float priceGain_FactionBase)
    {
        try
        {
            if (!HKSettingsUtil.EnableLocalRep || !HKSettingsUtil.LocalRepTradePricing) return;
            if (HKTradeSessionUtil.IsGiftMode("HarmonyPatch_TradePrice_LocalRep.TryIsGiftMode", "Trade price local rep patch could not probe TradeSession gift mode.")) return;

            var hero = HKRuntime.GetHeroPawnSafe();
            if (hero == null) return;

            // Only when the hero is the negotiator (if detectable).
            Pawn negotiator = HKTradeSessionUtil.TryGetPlayerNegotiator("HarmonyPatch_TradePrice_LocalRep.TryGetCurrentNegotiator", "Trade price local rep patch could not probe TradeSession negotiator.");
            if (negotiator != null && negotiator != hero) return;

            Faction faction = HKTradeSessionUtil.TryGetTraderFaction("HarmonyPatch_TradePrice_LocalRep.TryGetCurrentTraderFaction", "Trade price local rep patch could not probe TradeSession trader faction.");
            if (faction == null || faction.IsPlayer) return;

            string heroId = hero.GetUniqueLoadID();
            if (!LocalReputationUtility.TryGetFactionInfluenceIndex(heroId, faction.loadID, out float r))
                return;

            float delta = LocalRepTuning.TradeDelta(r);

            // Reputation Tax blocks positive gains.
            if (delta > 0f && HKPerkEffects.HasPerkHediff(hero, HediffReputationTax))
                delta = 0f;

            priceGain_FactionBase += delta;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_TradePrice_LocalRep",
                "Trade price local rep patch suppressed an exception.",
                ex);
        }
    }

}
