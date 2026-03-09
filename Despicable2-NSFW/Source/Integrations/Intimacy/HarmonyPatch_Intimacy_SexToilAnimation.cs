using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using Despicable;
using Despicable.Core;

namespace Despicable.NSFW.Integrations.Intimacy;
/// <summary>
/// Intimacy integration (soft):
/// Inject Despicable animation start when Intimacy begins its "sex act" toil.
///
/// Design goals:
///  - Do not replace Intimacy mechanics (need gain, thoughts, cooldown, pregnancy, etc.)
///  - Only add animation if we can safely do so (validation)
///  - Prefer humanlike↔humanlike flesh pairs (includes Humanoid Alien Races)
///  - Fail silent if Intimacy changes signatures or we can't find a hook point
///
/// Additional goal (visual alignment):
///  - We DO NOT change pregnancy chances.
///  - Instead, we bias the probability of selecting a Vaginal animation to match
///    Intimacy's computed pregnancy chance for this pair (best-effort).
/// </summary>
[HarmonyPatch]
internal static partial class HarmonyPatch_Intimacy_SexToilAnimation
{
    private static readonly Type JobDriver_SexLead = AccessTools.TypeByName("LoveyDoveySexWithEuterpe.JobDriver_SexLead");
    private static readonly Type JobDriver_Sex = AccessTools.TypeByName("LoveyDoveySexWithEuterpe.JobDriver_Sex");
    private static readonly Type JobDriver_MechSex = AccessTools.TypeByName("LoveyDoveySexWithEuterpe.JobDriver_Sex_Mech");

    private static IEnumerable<MethodBase> TargetMethods()
    {
        if (!IntegrationGuards.IsIntimacyLoaded())
            yield break;

        foreach (var t in new[] { JobDriver_SexLead, JobDriver_Sex, JobDriver_MechSex })
        {
            if (t == null) continue;
            var m = AccessTools.Method(t, "MakeNewToils");
            if (m != null) yield return m;
        }
    }

    private static void Postfix(object __instance, ref IEnumerable<Toil> __result)
    {
        if (__result == null) return;

        // Convert enumerable to list so we can safely re-yield (Intimacy might lazily enumerate).
        var list = __result as List<Toil> ?? new List<Toil>(__result);
        __result = PatchToils(__instance, list);
    }

    private static IEnumerable<Toil> PatchToils(object instance, List<Toil> toils)
    {
        if (instance is not JobDriver driver)
        {
            foreach (var t in toils) yield return t;
            yield break;
        }

        var pawn = driver.pawn;
        var job = driver.job;

        // Partner resolution varies between drivers. Try job targets first (common in RimWorld).
        Pawn partner = job?.targetA.Pawn ?? job?.targetB.Pawn;

        // Bed resolution (best-effort; ok if null)
        Building_Bed bed = job?.targetA.Thing as Building_Bed
                           ?? job?.targetB.Thing as Building_Bed
                           ?? pawn?.CurrentBed();

        bool canAnimate = IntegrationGuards.IsHumanoidFleshPairForAnimation(pawn, partner)
                          && CommonUtil.GetSettings().animationExtensionEnabled;

        int actToilIndex = -1;
        if (canAnimate)
            actToilIndex = FindActToilIndex(toils);

        for (int i = 0; i < toils.Count; i++)
        {
            var toil = toils[i];
            if (toil != null && i == actToilIndex && canAnimate)
            {

                // Capture per-job state for finishAction so we can stop/clear animations reliably.
                // (Without this, pawns can keep animating while the job moves on, causing "sliding".)
                var participants = new List<Pawn>(2);
                string chosenLovinTypeForJob = null;

                var priorInit = toil.initAction;
                toil.initAction = delegate
                {
                    try
                    {
                        priorInit?.Invoke();

                        // Choose a stage (LovinTypeDef.defName) in a way that visually aligns with
                        // Intimacy's intended pregnancy outcomes, WITHOUT changing its mechanics.
                        chosenLovinTypeForJob = ChooseLovinTypeForVisualAlignment(pawn, partner);

                        var chosenLovinType = chosenLovinTypeForJob;

                        // If we have a choice, store it so LovinStagePlayback resolves that stage tag.
                        if (!chosenLovinType.NullOrEmpty())
                        {
                            var store = InteractionInstanceStore.Get(pawn?.Map);
                            store?.SetStage(job.loadID, chosenLovinType);
                        }

                        participants.Clear();
                        participants.Add(pawn);
                        participants.Add(partner);
                        LovinVisualRuntime.SetLovinVisualActive(pawn, true);
                        LovinVisualRuntime.SetLovinVisualActive(partner, true);
                        int durationTicks;

                        // First attempt: with our chosen stage tag.
                        bool started = LovinStagePlayback.TryStartForJob(job, pawn, partner, bed, participants, out durationTicks);

                        // If the chosen type has no matching animations, fall back to Despicable's default selection.
                        if (!started && !chosenLovinType.NullOrEmpty())
                        {
                            try
                            {
                                var store = InteractionInstanceStore.Get(pawn?.Map);
                                store?.Clear(job.loadID);
                            }
                            catch (Exception ex)
                            {
                                DebugLogger.WarnExceptionOnce(
                                    "NSFW.Intimacy.ClearForcedStageFallback",
                                    "Failed to clear the forced Intimacy stage during fallback; retrying with the default Despicable selection.",
                                    ex);
                            }

                            started = LovinStagePlayback.TryStartForJob(job, pawn, partner, bed, participants, out durationTicks);
                        }

                        if (started)
                        {
                            LovinContextBridge.Set(pawn, partner, job.loadID, chosenLovinType);
                            // Keep toil duration intact to minimize side-effects.
                        }
                    }
                    catch (Exception e)
                    {
                        Log.Warning($"[Despicable2.NSFW] Intimacy animation injection failed (soft): {e}");
                    }
                };
                // Ensure we stop/reset animations as soon as the act toil completes.
                // This prevents pawns continuing animation while pathing resumes (sliding).
                toil.AddFinishAction(delegate
                {
                    try
                    {
                        if (participants != null && participants.Count > 0)
                        {
                            for (int participantIndex = 0; participantIndex < participants.Count; participantIndex++)
                                LovinVisualRuntime.SetLovinVisualActive(participants[participantIndex], false);

                            AnimUtil.ResetAnimatorsForGroup(participants);
                        }

                        // Clear any forced stage tag for this job so it doesn't leak to other interactions.
                        try
                        {
                            var store = InteractionInstanceStore.Get(pawn?.Map);
                            store?.Clear(job.loadID);
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.WarnExceptionOnce(
                                "NSFW.Intimacy.ClearForcedStageFinish",
                                "Failed to clear the forced Intimacy stage during finish cleanup; continuing with animator reset.",
                                ex);
                        }

                        // Clear best-effort context.
                        LovinContextBridge.Clear(pawn, partner);
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.WarnExceptionOnce(
                            "NSFW.Intimacy.FinishCleanup",
                            "Failed to finish Intimacy animation cleanup cleanly; the interaction will continue without the cleanup extras.",
                            ex);
                    }
                });

            }

            yield return toil;
        }
    }

}
