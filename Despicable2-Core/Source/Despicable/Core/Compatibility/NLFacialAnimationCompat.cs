using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Despicable.Core.Compatibility;
/// <summary>
/// When NL Facial Animation is installed, let custom Despicable lovin jobs present as vanilla Lovin
/// to external job-based facial systems. This keeps the integration soft and avoids a hard dependency
/// on external JobDef names that may live in optional content packs.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.CurJobDef), MethodType.Getter)]
internal static class HarmonyPatch_Pawn_CurJobDef_NLFacialAnimationCompat
{
    private static readonly string[] RelevantCustomLovinJobDefNames =
    {
        "Job_GetLovin",
        "Job_GetBedLovin",
        "Job_GiveLovin"
    };

    private static readonly JobDef[] RelevantCustomLovinJobDefs = new JobDef[RelevantCustomLovinJobDefNames.Length];
    private static bool relevantCustomLovinJobDefsResolved;

    private static void Postfix(Pawn __instance, ref JobDef __result)
    {
        if (!ModMain.IsNlFacialInstalled) return;
        if (!ContentAvailability.NSFWActive) return;
        if (__instance?.Map == null) return;
        if (!IsRelevantCustomLovinJob(__result)) return;

        Job curJob = __instance.CurJob;
        if (curJob == null) return;

        var store = InteractionInstanceStore.Get(__instance.Map);
        if (store == null) return;

        if (!store.TryGetChannel(curJob.loadID, out string channel)) return;
        if (channel != Channels.ManualLovin) return;
        if (!ShouldExposeAsLovin(__instance, store, curJob)) return;

        __result = JobDefOf.Lovin;
    }

    private static bool IsRelevantCustomLovinJob(JobDef jobDef)
    {
        if (jobDef == null)
            return false;

        EnsureRelevantCustomLovinJobDefsResolved();
        for (int i = 0; i < RelevantCustomLovinJobDefs.Length; i++)
        {
            if (ReferenceEquals(jobDef, RelevantCustomLovinJobDefs[i]))
                return true;
        }

        return false;
    }

    private static void EnsureRelevantCustomLovinJobDefsResolved()
    {
        if (relevantCustomLovinJobDefsResolved)
            return;

        for (int i = 0; i < RelevantCustomLovinJobDefNames.Length; i++)
            RelevantCustomLovinJobDefs[i] = DefDatabase<JobDef>.GetNamedSilentFail(RelevantCustomLovinJobDefNames[i]);

        relevantCustomLovinJobDefsResolved = true;
    }

    private static bool ShouldExposeAsLovin(Pawn pawn, InteractionInstanceStore store, Job curJob)
    {
        if (store.IsNLLovinActive(curJob.loadID))
            return true;

        // Core-only fallback: only expose the lovin face window while a body animation is actually playing.
        // This prevents approach / travel / setup phases of broader custom jobs from reading as vanilla Lovin.
        var animator = pawn.TryGetComp<CompExtendedAnimator>();
        return animator?.hasAnimPlaying == true;
    }
}
