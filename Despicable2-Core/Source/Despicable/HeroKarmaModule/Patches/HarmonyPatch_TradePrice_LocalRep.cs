using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

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
            if (TryIsGiftMode()) return;

            var hero = HKRuntime.GetHeroPawnSafe();
            if (hero == null) return;

            // Only when the hero is the negotiator (if detectable).
            Pawn negotiator = TryGetCurrentNegotiator();
            if (negotiator != null && negotiator != hero) return;

            Faction faction = TryGetCurrentTraderFaction();
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

    private static bool TryIsGiftMode()
    {
        try
        {
            var tradeSession = AccessTools.TypeByName("RimWorld.TradeSession");
            if (tradeSession == null) return false;

            var f = AccessTools.Field(tradeSession, "giftMode");
            if (f != null && f.FieldType == typeof(bool))
                return (bool)f.GetValue(null);

            var p = AccessTools.Property(tradeSession, "giftMode");
            if (p != null && p.PropertyType == typeof(bool))
                return (bool)p.GetValue(null, null);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_TradePrice_LocalRep.TryIsGiftMode",
                "Trade price local rep patch could not probe TradeSession gift mode.",
                ex);
        }

        return false;
    }

    private static Pawn TryGetCurrentNegotiator()
    {
        try
        {
            var tradeSession = AccessTools.TypeByName("RimWorld.TradeSession");
            if (tradeSession == null) return null;

            var f = AccessTools.Field(tradeSession, "playerNegotiator");
            if (f != null) return f.GetValue(null) as Pawn;

            var p = AccessTools.Property(tradeSession, "PlayerNegotiator");
            if (p != null) return p.GetValue(null, null) as Pawn;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_TradePrice_LocalRep.TryGetCurrentNegotiator",
                "Trade price local rep patch could not probe TradeSession negotiator.",
                ex);
        }

        return null;
    }

    private static Faction TryGetCurrentTraderFaction()
    {
        try
        {
            var tradeSession = AccessTools.TypeByName("RimWorld.TradeSession");
            if (tradeSession == null) return null;

            object trader = null;
            var f = AccessTools.Field(tradeSession, "trader");
            if (f != null) trader = f.GetValue(null);
            if (trader == null)
            {
                var p = AccessTools.Property(tradeSession, "Trader");
                if (p != null) trader = p.GetValue(null, null);
            }

            if (trader == null) return null;

            // If it's a pawn trader.
            if (trader is Pawn pawn) return pawn.Faction;

            // Try property "Faction".
            var factionProp = trader.GetType().GetProperty("Faction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (factionProp != null)
                return factionProp.GetValue(trader, null) as Faction;

            // Try method "GetFaction".
            var getFaction = trader.GetType().GetMethod("GetFaction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getFaction != null)
                return getFaction.Invoke(trader, Array.Empty<object>()) as Faction;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_TradePrice_LocalRep.TryGetCurrentTraderFaction",
                "Trade price local rep patch could not probe TradeSession trader faction.",
                ex);
        }

        return null;
    }
}
