using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;
internal static class HKGiftPatchHelpers
{
    public static void TryWrapOfferCommand(Command result, Caravan caravan)
    {
        if (result is not Command_Action command || command.action == null || caravan == null)
        {
            return;
        }

        Action originalAction = command.action;
        command.action = delegate
        {
            try
            {
                Pawn negotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan);
                if (negotiator != null)
                {
                    HKGiftContext.Set(negotiator);
                }
            }
            catch (Exception ex)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce(
                    "HarmonyPatch_FactionGiftUtility_Gifts:101",
                    "HarmonyPatch_FactionGiftUtility_Gifts suppressed an exception.",
                    ex);
            }

            originalAction.Invoke();
        };
    }

    internal static HKGoodwillContext.Scope TryEnterHeroGoodwillForGift()
    {
        Pawn actor = HKGiftContext.GetValidPawn();
        Pawn hero = HKRuntime.GetHeroPawnSafe();
        if (actor == null || hero == null || actor != hero)
        {
            return default;
        }

        return HKGoodwillContext.Enter(hero);
    }

    public static void ProcessTradeableGift(List<Tradeable> tradeables, Faction giveTo, GlobalTargetInfo lookTarget)
    {
        if (tradeables == null || giveTo == null)
        {
            return;
        }

        Pawn actor = HKGiftContext.GetValidPawn();
        string settlementUniqueId = HKSettlementContextUtil.TryResolveSettlementUniqueId(lookTarget, "HarmonyPatch_FactionGiftUtility_Gifts:106", "HarmonyPatch_FactionGiftUtility_Gifts failed to resolve the target settlement for a charity gift.") ?? HKGiftContext.GetValidSettlementUniqueId();
        string settlementLabel = HKSettlementContextUtil.TryResolveSettlementLabel(lookTarget, "HarmonyPatch_FactionGiftUtility_Gifts:107", "HarmonyPatch_FactionGiftUtility_Gifts failed to resolve the target settlement label for a charity gift.") ?? HKGiftContext.GetValidSettlementLabel();
        HKGiftContext.Clear();

        Pawn hero = HKRuntime.GetHeroPawnSafe();
        if (hero == null || actor == null || actor != hero)
        {
            return;
        }

        int amount = CalculateTradeableGiftAmount(tradeables);
        if (amount <= 0)
        {
            return;
        }

        var karmaEvent = new KarmaEvent
        {
            eventKey = "CharityGift",
            actor = hero,
            targetFactionId = giveTo.loadID,
            amount = amount,
            settlementUniqueId = settlementUniqueId,
            settlementLabel = settlementLabel
        };

        HKKarmaProcessor.Process(karmaEvent);
    }

    public static void ProcessPodGift(List<ActiveTransporterInfo> pods, Settlement giveTo)
    {
        if (pods == null || giveTo == null || giveTo.Faction == null)
        {
            return;
        }

        Pawn hero = HKRuntime.GetHeroPawnSafe();
        if (hero == null)
        {
            return;
        }

        bool heroInPods = false;
        float totalValue = 0f;
        for (int i = 0; i < pods.Count; i++)
        {
            ActiveTransporterInfo pod = pods[i];
            if (pod == null || pod.innerContainer == null)
            {
                continue;
            }

            var inner = pod.innerContainer;
            for (int j = 0; j < inner.Count; j++)
            {
                Thing carriedThing = inner[j];
                if (carriedThing == null)
                {
                    continue;
                }

                if (!heroInPods && carriedThing is Pawn pawn && pawn == hero)
                {
                    heroInPods = true;
                }

                totalValue += carriedThing.MarketValue * carriedThing.stackCount;
            }
        }

        if (!heroInPods)
        {
            return;
        }

        int amount = Mathf.RoundToInt(totalValue);
        if (amount <= 0)
        {
            return;
        }

        var karmaEvent = new KarmaEvent
        {
            eventKey = "CharityGift",
            actor = hero,
            targetFactionId = giveTo.Faction.loadID,
            amount = amount,
            settlementUniqueId = giveTo.GetUniqueLoadID(),
            settlementLabel = giveTo.LabelCap
        };

        HKKarmaProcessor.Process(karmaEvent);
    }



    private static int CalculateTradeableGiftAmount(List<Tradeable> tradeables)
    {
        float totalValue = 0f;
        for (int i = 0; i < tradeables.Count; i++)
        {
            Tradeable tradeable = tradeables[i];
            if (tradeable == null || tradeable.ActionToDo != TradeAction.PlayerSells)
            {
                continue;
            }

            int count = Mathf.Min(
                tradeable.CountToTransferToDestination,
                tradeable.CountHeldBy(Transactor.Colony));
            if (count <= 0)
            {
                continue;
            }

            Thing anyThing = tradeable.AnyThing;
            if (anyThing == null)
            {
                continue;
            }

            float unitPrice = TryGetSellUnitPrice(tradeable, anyThing);
            totalValue += unitPrice * count;
        }

        return Mathf.RoundToInt(totalValue);
    }

    private static float TryGetSellUnitPrice(Tradeable tradeable, Thing anyThing)
    {
        try
        {
            return tradeable.GetPriceFor(TradeAction.PlayerSells);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_FactionGiftUtility_Gifts:103",
                "HarmonyPatch_FactionGiftUtility_Gifts suppressed an exception.",
                ex);
            return anyThing.MarketValue;
        }
    }
}
