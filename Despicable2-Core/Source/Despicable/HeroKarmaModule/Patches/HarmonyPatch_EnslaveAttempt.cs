using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld.Planet;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

[HarmonyPatch]
public static class HarmonyPatch_EnslaveAttempt
{
    private const string PatchId = "HKPatch.EnslaveAttempt";
    // Guardrail-Allow-Static: Cached Harmony target list for this patch class; populated during Prepare and reused for patch registration within the current load.
    private static List<MethodBase> _targets;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return HKPatchGuard.PrepareMany(
            PatchId,
            "Enslave attempt (interaction)",
            featureKey: "CoreKarma",
            required: true,
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
            "RimWorld.InteractionWorker_EnslaveAttempt",
            "RimWorld.InteractionWorker_Enslave"
        };

        for (int i = 0; i < names.Length; i++)
        {
            var t = AccessTools.TypeByName(names[i]);
            if (t == null) continue;

            var m = AccessTools.Method(t, "Interacted");
            if (m != null) yield return m;
        }
    }

    public static void Postfix(Pawn initiator, Pawn recipient)
    {
        if (!HKSettingsUtil.HookEnabled("EnslaveAttempt")) return;

        try
        {
            if (initiator == null || recipient == null) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;

            int factionId = HKHookUtil.GetFactionIdSafe(recipient);
            var ev = KarmaEvent.Create("EnslaveAttempt", initiator, recipient, factionId);

            Settlement settlement = (initiator.MapHeld ?? initiator.Map)?.Parent as Settlement
                ?? (recipient.MapHeld ?? recipient.Map)?.Parent as Settlement;
            if (settlement != null && settlement.Faction != null && !settlement.Faction.IsPlayer)
            {
                ev.settlementUniqueId = settlement.GetUniqueLoadID();
                ev.settlementLabel = settlement.LabelCap;
            }

            HKKarmaProcessor.Process(ev);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_EnslaveAttempt:1", "HarmonyPatch_EnslaveAttempt suppressed an exception.", ex);
        }
    }

    public static void Prefix(Pawn initiator, Pawn recipient, ref bool __state)
    {
        __state = false;
        try
        {
            if (initiator == null || recipient == null) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;

            HKGoodwillContext.Begin(initiator);
            __state = true;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_EnslaveAttempt:2", "HarmonyPatch_EnslaveAttempt suppressed an exception.", ex);
        }
    }

    public static void Finalizer(Exception __exception, bool __state)
    {
        if (!__state) return;
        try { HKGoodwillContext.End(); }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_EnslaveAttempt:3", "HarmonyPatch_EnslaveAttempt suppressed an exception.", ex);
        }
    }
}
