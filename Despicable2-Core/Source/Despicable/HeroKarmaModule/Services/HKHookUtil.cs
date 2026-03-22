using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;

namespace Despicable.HeroKarma.Patches.HeroKarma;
internal static class HKHookUtil
{
    public static int GetFactionIdSafe(Pawn p)
    {
        return global::Despicable.PawnAffiliation.GetNonPlayerFactionIdSafe(p);
    }

    public static bool IsAnimal(Pawn p)
    {
        return global::Despicable.PawnQuery.IsAnimal(p);
    }

    public static bool IsPermanentManhunterOrBerserk(Pawn p)
    {
        return global::Despicable.PawnQuery.IsPermanentManhunterOrBerserk(p);
    }

    public static bool IsMechanoid(Pawn p)
    {
        return global::Despicable.PawnQuery.IsMechanoid(p);
    }

public static bool IsGuestLike(Pawn p)
{
    return global::Despicable.PawnAffiliation.IsGuestLike(p);
}

public static bool IsEmergencyTendTarget(Pawn patient)
{
    return global::Despicable.PawnHealthQuery.IsEmergencyTendTarget(patient);
}


public static bool IsSlaveLike(Pawn p)
{
    return global::Despicable.PawnAffiliation.IsSlaveLike(p);
}

public static bool IsPrisonerLike(Pawn p)
{
    return global::Despicable.PawnAffiliation.IsPrisonerLike(p);
}


public static bool IsCurrentlyGuilty(Pawn p)
{
    return global::Despicable.PawnQuery.IsCurrentlyGuilty(p);
}

public static bool IsLikelyBeggarsQuestPawn(Pawn p)
{
    return global::Despicable.PawnAffiliation.IsLikelyBeggarsQuestPawn(p);
}


public static Pawn FindNearbyHeroWithPerk(Pawn center, string hediffDefName, float maxDistance)
{
    try
    {
        if (center == null || center.MapHeld == null) return null;

        Pawn hero = HKRuntime.GetHeroPawnSafe();
        if (hero == null || hero.MapHeld != center.MapHeld) return null;
        if (!HKPerkEffects.HasPerkHediff(hero, hediffDefName)) return null;
        if (!hero.Spawned || !center.Spawned) return null;
        if (!hero.Position.InHorDistOf(center.Position, maxDistance)) return null;

        return hero;
    }
    catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("HKHookUtil:109", "HKHookUtil suppressed an exception.", ex); return null; }
}

public static bool TryOffsetNeedMood(Pawn pawn, float offset)
{
    try
    {
        var mood = pawn?.needs?.mood;
        if (mood == null || Math.Abs(offset) < 0.0001f) return false;

        mood.CurLevel = Mathf.Clamp01(mood.CurLevel + offset);
        return true;
    }
    catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("HKHookUtil:110", "HKHookUtil suppressed an exception.", ex); return false; }
}

public static bool TryOffsetPrisonerResistance(Pawn pawn, float delta)
{
    try
    {
        if (pawn == null || Math.Abs(delta) < 0.0001f) return false;

        object guestTracker = AccessTools.Field(pawn.GetType(), "guest")?.GetValue(pawn)
            ?? AccessTools.Property(pawn.GetType(), "guest")?.GetValue(pawn, null);
        if (guestTracker == null) return false;

        var resistanceProp = AccessTools.Property(guestTracker.GetType(), "Resistance");
        if (resistanceProp == null || !resistanceProp.CanRead || !resistanceProp.CanWrite) return false;

        float current = (float)resistanceProp.GetValue(guestTracker, null);
        float next = Math.Max(0f, current + delta);
        if (Math.Abs(next - current) < 0.0001f) return false;

        resistanceProp.SetValue(guestTracker, next, null);
        return true;
    }
    catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("HKHookUtil:111", "HKHookUtil suppressed an exception.", ex); return false; }
}

public static bool TryOffsetIdeoCertainty(Pawn pawn, float delta)
{
    try
    {
        if (pawn == null || Math.Abs(delta) < 0.0001f) return false;

        object ideoTracker = AccessTools.Field(pawn.GetType(), "ideo")?.GetValue(pawn)
            ?? AccessTools.Property(pawn.GetType(), "ideo")?.GetValue(pawn, null);
        if (ideoTracker == null) return false;

        var method = AccessTools.Method(ideoTracker.GetType(), "OffsetCertainty", new[] { typeof(float) })
            ?? AccessTools.Method(ideoTracker.GetType(), "OffsetCertainty", new[] { typeof(float), typeof(bool) });
        if (method == null) return false;

        var parameters = method.GetParameters();
        if (parameters.Length == 1)
            method.Invoke(ideoTracker, new object[] { delta });
        else if (parameters.Length == 2)
            method.Invoke(ideoTracker, new object[] { delta, true });
        else
            return false;

        return true;
    }
    catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("HKHookUtil:112", "HKHookUtil suppressed an exception.", ex); return false; }
}

public static bool TryAwardBonusGoodwill(Pawn hero, Pawn target, int goodwillDelta, string eventKey, int cooldownTicks)
{
    try
    {
        if (hero == null || target == null || goodwillDelta == 0) return false;
        Faction heroFaction = hero.Faction;
        Faction targetFaction = target.Faction;
        if (heroFaction == null || targetFaction == null) return false;
        if (targetFaction.IsPlayer || heroFaction == targetFaction) return false;

        int factionId = targetFaction.loadID;
        if (!HKEventDebouncer.ShouldProcess(
                eventKey,
                hero.GetUniqueLoadID(),
                target.GetUniqueLoadID(),
                factionId,
                stage: 0,
                cooldownTicks: cooldownTicks,
                windowTicks: cooldownTicks * 3,
                maxPerWindow: 1))
            return false;

        var method = AccessTools.Method(typeof(Faction), "TryAffectGoodwillWith");
        if (method == null) return false;

        object[] args = BuildTryAffectGoodwillArgs(method, targetFaction, goodwillDelta);
        if (args == null) return false;

        using (HKGoodwillContext.Enter(hero))
        {
            method.Invoke(heroFaction, args);
        }

        return true;
    }
    catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("HKHookUtil:113", "HKHookUtil suppressed an exception.", ex); return false; }
}

private static object[] BuildTryAffectGoodwillArgs(MethodInfo method, Faction other, int goodwillDelta)
{
    try
    {
        var parameters = method.GetParameters();
        if (parameters == null || parameters.Length == 0) return null;

        var args = new object[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            var p = parameters[i];
            var t = p.ParameterType;

            if (i == 0 && typeof(Faction).IsAssignableFrom(t))
            {
                args[i] = other;
                continue;
            }

            if (t == typeof(int))
            {
                args[i] = goodwillDelta;
                continue;
            }

            if (t == typeof(bool))
            {
                args[i] = false;
                continue;
            }

            if (t.IsValueType)
            {
                args[i] = Activator.CreateInstance(t);
                continue;
            }

            args[i] = null;
        }

        return args;
    }
    catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("HKHookUtil:114", "HKHookUtil suppressed an exception.", ex); return null; }
}

}
