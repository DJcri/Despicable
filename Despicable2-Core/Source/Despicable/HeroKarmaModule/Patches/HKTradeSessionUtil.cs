using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;
internal static class HKTradeSessionUtil
{
    public static bool IsGiftMode(string warnKey = null, string warnMessage = null)
    {
        try
        {
            Type tradeSession = AccessTools.TypeByName("RimWorld.TradeSession");
            if (tradeSession == null) return false;

            object value = AccessTools.Field(tradeSession, "giftMode")?.GetValue(null);
            if (value is bool fieldValue) return fieldValue;

            PropertyInfo property = AccessTools.Property(tradeSession, "giftMode")
                ?? AccessTools.Property(tradeSession, "GiftMode");
            if (property != null && property.GetValue(null, null) is bool propertyValue)
                return propertyValue;
        }
        catch (Exception ex)
        {
            Warn(warnKey, warnMessage, ex);
        }

        return false;
    }

    public static Pawn TryGetPlayerNegotiator(string warnKey = null, string warnMessage = null)
    {
        try
        {
            Type tradeSession = AccessTools.TypeByName("RimWorld.TradeSession");
            if (tradeSession == null) return null;

            object value = AccessTools.Field(tradeSession, "playerNegotiator")?.GetValue(null);
            if (value is Pawn fieldPawn) return fieldPawn;

            PropertyInfo property = AccessTools.Property(tradeSession, "playerNegotiator")
                ?? AccessTools.Property(tradeSession, "PlayerNegotiator");
            if (property != null && property.GetValue(null, null) is Pawn propertyPawn)
                return propertyPawn;
        }
        catch (Exception ex)
        {
            Warn(warnKey, warnMessage, ex);
        }

        return null;
    }

    public static object TryGetTrader(string warnKey = null, string warnMessage = null)
    {
        try
        {
            Type tradeSession = AccessTools.TypeByName("RimWorld.TradeSession");
            if (tradeSession == null) return null;

            object trader = AccessTools.Field(tradeSession, "trader")?.GetValue(null);
            if (trader != null) return trader;

            PropertyInfo property = AccessTools.Property(tradeSession, "Trader")
                ?? AccessTools.Property(tradeSession, "trader");
            return property?.GetValue(null, null);
        }
        catch (Exception ex)
        {
            Warn(warnKey, warnMessage, ex);
            return null;
        }
    }

    public static Faction TryGetTraderFaction(string warnKey = null, string warnMessage = null)
    {
        try
        {
            object trader = TryGetTrader();
            if (trader == null) return null;

            if (trader is Pawn pawn)
                return pawn.Faction;

            PropertyInfo factionProperty = trader.GetType().GetProperty("Faction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (factionProperty != null)
                return factionProperty.GetValue(trader, null) as Faction;

            MethodInfo getFaction = trader.GetType().GetMethod("GetFaction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (getFaction != null)
                return getFaction.Invoke(trader, Array.Empty<object>()) as Faction;
        }
        catch (Exception ex)
        {
            Warn(warnKey, warnMessage, ex);
        }

        return null;
    }

    public static Settlement TryGetCurrentSettlement(string warnKey = null, string warnMessage = null)
    {
        try
        {
            object trader = TryGetTrader();
            if (trader == null) return null;

            if (trader is Settlement settlement)
                return IsUsableSettlement(settlement) ? settlement : null;

            if (TryResolveSettlementFromMember(trader, "Settlement") is Settlement settlementFromProperty)
                return IsUsableSettlement(settlementFromProperty) ? settlementFromProperty : null;

            if (TryResolveSettlementFromMember(trader, "WorldObject") is Settlement settlementFromWorldObject)
                return IsUsableSettlement(settlementFromWorldObject) ? settlementFromWorldObject : null;

            if (TryResolveSettlementFromMethod(trader, "GetWorldObject") is Settlement settlementFromMethod)
                return IsUsableSettlement(settlementFromMethod) ? settlementFromMethod : null;
        }
        catch (Exception ex)
        {
            Warn(warnKey, warnMessage, ex);
        }

        return null;
    }

    public static string TryGetCurrentSettlementUniqueId(string warnKey = null, string warnMessage = null)
    {
        Settlement settlement = TryGetCurrentSettlement(warnKey, warnMessage);
        return settlement != null ? settlement.GetUniqueLoadID() : null;
    }

    public static string TryGetCurrentSettlementLabel(string warnKey = null, string warnMessage = null)
    {
        Settlement settlement = TryGetCurrentSettlement(warnKey, warnMessage);
        return settlement != null ? settlement.LabelCap : null;
    }

    private static object TryResolveSettlementFromMember(object instance, string memberName)
    {
        if (instance == null || memberName.NullOrEmpty()) return null;

        PropertyInfo property = AccessTools.Property(instance.GetType(), memberName);
        if (property != null)
            return property.GetValue(instance, null);

        FieldInfo field = AccessTools.Field(instance.GetType(), memberName);
        if (field != null)
            return field.GetValue(instance);

        return null;
    }

    private static object TryResolveSettlementFromMethod(object instance, string methodName)
    {
        if (instance == null || methodName.NullOrEmpty()) return null;

        MethodInfo method = AccessTools.Method(instance.GetType(), methodName, Type.EmptyTypes);
        return method?.Invoke(instance, null);
    }

    private static bool IsUsableSettlement(Settlement settlement)
    {
        return settlement != null && settlement.Faction != null && !settlement.Faction.IsPlayer;
    }

    private static void Warn(string warnKey, string warnMessage, Exception ex)
    {
        if (warnKey.NullOrEmpty() || warnMessage.NullOrEmpty() || ex == null)
            return;

        Despicable.Core.DebugLogger.WarnExceptionOnce(warnKey, warnMessage, ex);
    }
}
