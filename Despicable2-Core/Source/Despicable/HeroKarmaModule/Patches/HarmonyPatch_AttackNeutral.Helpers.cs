using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

public static partial class HarmonyPatch_AttackNeutral
{
    private const string PatchId = "HKPatch.AttackNeutral";
    // Guardrail-Allow-Static: Cached Harmony target list for this patch class; populated during Prepare and reused for patch registration within the current load.
    private static List<MethodBase> _targets;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return HKPatchGuard.PrepareMany(
            PatchId,
            "Attack neutral outsiders (damage hook)",
            featureKey: "CoreKarma",
            required: true,
            candidates: FindTargets(),
            cached: out _targets);
    }

    static IEnumerable<MethodBase> TargetMethods()
    {
        return _targets ?? (IEnumerable<MethodBase>)Array.Empty<MethodBase>();
    }

    private static IEnumerable<MethodBase> FindTargets()
    {
        Type healthTrackerType = AccessTools.TypeByName("Verse.Pawn_HealthTracker");
        if (healthTrackerType == null)
            yield break;

        MethodInfo postApplyDamage = AccessTools.Method(healthTrackerType, "PostApplyDamage");
        if (postApplyDamage != null)
            yield return postApplyDamage;
    }

    private static void MaybePrune(int now)
    {
        if (PruneTracker.ShouldSkipPrune(InitiatedTicksByInteractionKey.Count, MaxEntries, now, 10000))
        {
            return;
        }

        PruneTracker.MarkPruned(now);
        if (InitiatedTicksByInteractionKey.Count == 0)
        {
            return;
        }

        try
        {
            var toRemove = new List<string>();
            foreach (var kv in InitiatedTicksByInteractionKey)
            {
                if (now - kv.Value > StaleTicks)
                {
                    toRemove.Add(kv.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                InitiatedTicksByInteractionKey.Remove(toRemove[i]);
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_AttackNeutral:101",
                "HarmonyPatch_AttackNeutral suppressed an exception.",
                ex);
        }
    }

    private static Pawn GetVictimPawn(object healthTracker)
    {
        try
        {
            var pawnField = AccessTools.Field(healthTracker.GetType(), "pawn");
            if (pawnField != null)
            {
                return pawnField.GetValue(healthTracker) as Pawn;
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_AttackNeutral:4",
                "HarmonyPatch_AttackNeutral suppressed an exception.",
                ex);
        }

        return null;
    }

    private static string BuildKey(Pawn attacker, Pawn victim)
    {
        try
        {
            return attacker.GetUniqueLoadID() + "|" + victim.GetUniqueLoadID();
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_AttackNeutral:102",
                "HarmonyPatch_AttackNeutral suppressed an exception.",
                ex);
            return null;
        }
    }

    private static void RememberInitiated(Pawn attacker, Pawn victim)
    {
        string key = BuildKey(attacker, victim);
        if (key.NullOrEmpty())
        {
            return;
        }

        int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        MaybePrune(now);
        InitiatedTicksByInteractionKey[key] = now;
    }

    private static bool WasInitiatedRecently(Pawn attacker, Pawn victim)
    {
        string key = BuildKey(attacker, victim);
        if (key.NullOrEmpty())
        {
            return false;
        }

        if (!InitiatedTicksByInteractionKey.TryGetValue(key, out int last))
        {
            return false;
        }

        int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        MaybePrune(now);
        return now - last <= InitiationMemoryTicks;
    }
}
