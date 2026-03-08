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
    private static void Postfix(Pawn __instance, ref JobDef __result)
    {
        if (!ModMain.IsNlFacialInstalled) return;
        if (__instance?.Map == null) return;

        Job curJob = __instance.CurJob;
        if (curJob == null) return;
        if (!IsRelevantCustomLovinJob(curJob.def)) return;

        var store = InteractionInstanceStore.Get(__instance.Map);
        if (store == null) return;

        if (!store.TryGetChannel(curJob.loadID, out string channel)) return;
        if (channel != Channels.ManualLovin) return;
        if (!ShouldExposeAsLovin(__instance, store, curJob)) return;

        __result = JobDefOf.Lovin;
    }

    private static bool IsRelevantCustomLovinJob(JobDef jobDef)
    {
        string defName = jobDef?.defName;
        if (defName == null) return false;

        return defName == "Job_GetLovin"
            || defName == "Job_GetBedLovin"
            || defName == "Job_GiveLovin";
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
