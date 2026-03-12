using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
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

        return HKPatchTargetUtil.FindFirstMethods(names, "Interacted");
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

            HKSettlementContextUtil.TryAssignFromPawns(ev, initiator, recipient);

            HKKarmaProcessor.Process(ev);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_EnslaveAttempt:1", "HarmonyPatch_EnslaveAttempt suppressed an exception.", ex);
        }
    }

    private static void Prefix(Pawn initiator, Pawn recipient, ref HKGoodwillContext.Scope __state)
    {
        __state = default;
        try
        {
            if (initiator == null || recipient == null) return;
            if (!HKHookUtilSafe.ActorIsHero(initiator)) return;

            __state = HKGoodwillContext.Enter(initiator);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_EnslaveAttempt:2", "HarmonyPatch_EnslaveAttempt suppressed an exception.", ex);
        }
    }

    private static void Finalizer(Exception __exception, HKGoodwillContext.Scope __state)
    {
        try { __state.Dispose(); }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_EnslaveAttempt:3", "HarmonyPatch_EnslaveAttempt suppressed an exception.", ex);
        }
    }
}
