using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

[HarmonyPatch]
public static partial class HarmonyPatch_OrganHarvest
{
    private const string PatchId = "HKPatch.OrganHarvest";
    // Guardrail-Allow-Static: Cached Harmony target list for this patch class; populated during Prepare and reused for patch registration within the current load.
    private static List<MethodBase> _targets;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return HKPatchGuard.PrepareMany(
            PatchId,
            "Organ harvest (surgery)",
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
        var t = AccessTools.TypeByName("RimWorld.Recipe_RemoveBodyPart");
        if (t != null)
        {
            var m = AccessTools.Method(t, "ApplyOnPawn");
            if (m != null)
                yield return m;
        }

        // Some versions route through Recipe_Surgery.ApplyOnPawn; we can optionally patch that later.
    }

    public static void Prefix(object[] __args, ref bool __state)
    {
        __state = false;
        try
        {
            if (!TryGetPawnPair(__args, out var pawn, out var billDoer))
            {
                return;
            }

            if (!HKHookUtilSafe.ActorIsHero(billDoer))
            {
                return;
            }

            HKGoodwillContext.Begin(billDoer);
            __state = true;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_OrganHarvest:1",
                "HarmonyPatch_OrganHarvest suppressed an exception.",
                ex);
        }
    }

    public static void Postfix(object[] __args)
    {
        if (!HKSettingsUtil.HookEnabled("OrganHarvest"))
            return;

        try
        {
            if (!TryGetPawnPair(__args, out var pawn, out var billDoer))
            {
                return;
            }

            if (!HKHookUtilSafe.ActorIsHero(billDoer))
            {
                return;
            }

            var part = FindFirstArg<BodyPartRecord>(__args);
            if (!LooksLikeHarvestableOrgan(part))
            {
                return;
            }

            int factionId = HKHookUtil.GetFactionIdSafe(pawn);
            var ev = KarmaEvent.Create("OrganHarvest", billDoer, pawn, factionId);
            HKKarmaProcessor.Process(ev);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_OrganHarvest:2",
                "HarmonyPatch_OrganHarvest suppressed an exception.",
                ex);
        }
    }

    public static void Finalizer(Exception __exception, bool __state)
    {
        if (!__state) return;

        try { HKGoodwillContext.End(); }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_OrganHarvest:3",
                "HarmonyPatch_OrganHarvest suppressed an exception.",
                ex);
        }
    }
}
