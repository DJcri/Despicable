using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Despicable.HeroKarma;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

public static partial class HarmonyPatch_ExecutePrisoner
{
    private const string PatchId = "HKPatch.ExecutePrisoner";
    // Guardrail-Allow-Static: Cached Harmony target list for this patch class; populated during Prepare and reused for patch registration within the current load.
    private static List<MethodBase> _targets;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return HKPatchGuard.PrepareMany(
            PatchId,
            "Execute prisoner (execution hooks)",
            featureKey: "CoreKarma",
            required: true,
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
        var seen = new HashSet<MethodBase>();
        Type utilityType = AccessTools.TypeByName("RimWorld.ExecutionUtility");
        if (utilityType != null)
        {
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(utilityType))
            {
                if (ShouldYieldExecutionMethod(method) && seen.Add(method))
                    yield return method;
            }
        }

        foreach (Type type in AccessTools.AllTypes())
        {
            if (!IsRimWorldExecutionType(type))
                continue;

            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(type))
            {
                if (ShouldYieldExecutionMethod(method) && seen.Add(method))
                    yield return method;
            }
        }
    }

    private static bool ShouldYieldExecutionMethod(MethodInfo method)
    {
        return method != null
            && method.Name.IndexOf("Execution", StringComparison.OrdinalIgnoreCase) >= 0
            && HasAtLeastTwoPawnParameters(method);
    }

    private static bool IsRimWorldExecutionType(Type type)
    {
        return type != null
            && string.Equals(type.Namespace, "RimWorld", StringComparison.Ordinal)
            && type.Name.IndexOf("Execution", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasAtLeastTwoPawnParameters(MethodBase method)
    {
        if (method == null)
            return false;

        int count = 0;
        foreach (ParameterInfo parameter in method.GetParameters())
        {
            if (parameter.ParameterType == typeof(Pawn) || parameter.ParameterType.IsSubclassOf(typeof(Pawn)))
            {
                count++;
                if (count >= 2)
                    return true;
            }
        }

        return false;
    }

    private static bool TryGetExecutionerAndVictim(object[] args, out Pawn executioner, out Pawn victim)
{
    executioner = null;
    victim = null;

    if (!TryGetFirstTwoPawns(args, out Pawn first, out Pawn second))
    {
        return false;
    }

    // Be resilient to argument order differences across execution helpers.
    // If either of the first two pawns is the hero, treat that pawn as the executioner.
    if (HKHookUtilSafe.ActorIsHero(second) && !HKHookUtilSafe.ActorIsHero(first))
    {
        executioner = second;
        victim = first;
        return true;
    }

    executioner = first;
    victim = second;
    return true;
}

private static bool TryGetFirstTwoPawns(object[] args, out Pawn first, out Pawn second)
{
    first = null;
    second = null;
    if (args == null)
        return false;

    foreach (object arg in args)
    {
        if (!(arg is Pawn pawn))
            continue;

        if (first == null)
        {
            first = pawn;
            continue;
        }

        second = pawn;
        return true;
    }

    return false;
}
}


/// <summary>
/// Small adapter so patches can safely check hero without touching internals.
/// </summary>
internal static class HKHookUtilSafe
{
    public static bool ActorIsHero(Pawn actor)
    {
        try
        {
            var gameComponent = Current.Game != null
                ? Current.Game.GetComponent<GameComponent_HeroKarma>()
                : null;
            if (gameComponent == null)
            {
                return false;
            }

            string heroPawnId = gameComponent.HeroPawnId;
            if (heroPawnId.NullOrEmpty())
            {
                return false;
            }

            return actor != null && actor.GetUniqueLoadID() == heroPawnId;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_ExecutePrisoner:101",
                "HarmonyPatch_ExecutePrisoner suppressed an exception.",
                ex);
            return false;
        }
    }
}