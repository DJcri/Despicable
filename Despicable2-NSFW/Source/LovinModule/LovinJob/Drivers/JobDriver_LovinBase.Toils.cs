using RimWorld;
using System;
using Verse;
using Verse.AI;
using Despicable.NSFW.Integrations.Intimacy;

namespace Despicable;
public partial class JobDriver_LovinBase
{
    // Lovin toil
    protected Toil LovinToil()
    {
        Toil lovinToil = new();
        lovinToil.defaultCompleteMode = ToilCompleteMode.Never;
        lovinToil.socialMode = RandomSocialMode.Off;
        lovinToil.handlingFacing = true;
        lovinToil.initAction = delegate
        {
            try
            {
                LovinVisualRuntime.SetLovinVisualActive(pawn, true);
                LovinVisualRuntime.SetLovinVisualActive(Partner, true);
                TryStartLovinAnimation();
            }
            catch (Exception e)
            {
                if (Prefs.DevMode)
                    Log.Warning($"[DespicableNSFW] LovinToil initAction error: {e}");
            }

            try
            {
                TryApplyManualBoundaryThoughtAtLovinStart();
            }
            catch (Exception e)
            {
                if (Prefs.DevMode)
                    Log.Warning($"[DespicableNSFW] Manual boundary thought initAction error: {e}");
            }
        };
        lovinToil.AddPreTickAction(delegate
        {
            durationTicks--;

            try
            {
                TickParticipants();

                if (ShouldFinishLovin())
                    ReadyForNextToil();
            }
            catch (Exception e)
            {
                if (Prefs.DevMode)
                    Log.Warning($"[DespicableNSFW] LovinToil tick error: {e}");
            }

            if (durationTicks <= 0)
                ReadyForNextToil();
        });
        lovinToil.AddFinishAction(delegate
        {
            LovinVisualRuntime.SetLovinVisualActive(pawn, false);
            LovinVisualRuntime.SetLovinVisualActive(Partner, false);
            ResumePartnerAndResetAnimators();
        });
        lovinToil.FailOn(() => !PartnerPresent(Partner));

        return lovinToil;
    }

    // Gives memory of lovin' to pawns
    protected Toil FinalizeLovinToil()
    {
        Toil finalizeLovinToil = new Toil
        {
            initAction = () =>
            {
                var map = pawn.Map;
                if (map == null)
                    return;

                var currentJob = pawn.CurJob;
                if (currentJob == null)
                    return;

                int jobId = currentJob.loadID;

                string interactionDefName = null;

                var store = Despicable.Core.InteractionInstanceStore.Get(map);
                if (store != null)
                    store.TryGet(jobId, out interactionDefName);

                InteractionDef interactionDef = null;
                if (!interactionDefName.NullOrEmpty())
                    interactionDef = DefDatabase<InteractionDef>.GetNamedSilentFail(interactionDefName);

                // Fallback (keeps old behavior if store missing)
                if (interactionDef == null)
                {
                    LovinTypeDef lovinType = currentJob.def.GetModExtension<ModExtension_LovinType>()?.lovinType;
                    interactionDef = lovinType?.interaction;
                }

                if (interactionDef == null)
                {
                    store?.Clear(jobId);
                    return;
                }

                int a = pawn.thingIDNumber;
                int b = Partner.thingIDNumber;

                if (!Despicable.Core.OutcomeHistory.TryApplyOnce(map, "LovinFinalize", a, b, interactionDef.defName, jobId))
                {
                    store?.Clear(jobId);
                    return;
                }

                IntimacyApplyUtil.TryApplyPostLovinEffects(pawn, Partner);
                pawn.interactions.TryInteractWith(Partner, interactionDef);

                store?.Clear(jobId);
            },
            defaultCompleteMode = ToilCompleteMode.Instant
        };

        return finalizeLovinToil;
    }

    // Check if partner pawn is still valid
    protected void PreInit()
    {
        pawn.jobs.curJob.playerForced = false;
        FailOnDespawnedNullOrForbidden(iTarget);
        FailOn(() => !Spawned(Partner));
        FailOn(() => !PassesCurrentLovinValidation());
    }

    protected void FailOn(Func<bool> failCondition)
    {
        AddEndCondition(() => failCondition() ? JobCondition.Incompletable : JobCondition.Ongoing);
    }

    protected void FailOnDespawnedNullOrForbidden(TargetIndex targetIndex)
    {
        AddEndCondition(() =>
        {
            LocalTargetInfo targetInfo = job.GetTarget(targetIndex);
            if (!targetInfo.IsValid)
                return JobCondition.Incompletable;

            Thing targetThing = targetInfo.Thing;
            if (targetThing == null || targetThing.Destroyed)
                return JobCondition.Incompletable;

            if (targetThing.IsForbidden(pawn))
                return JobCondition.Incompletable;

            return JobCondition.Ongoing;
        });
    }

    // Predicate conditions where lovin' should end if not met
    protected bool Spawned(Pawn partner) =>
        partner.Spawned && partner.Map == pawn.Map;

    protected bool NotDrafted(Pawn partner) =>
        !partner.Drafted && !pawn.Drafted;

    protected bool PassesCurrentLovinValidation()
    {
        if (Partner == null)
            return false;

        var store = Despicable.Core.InteractionInstanceStore.Get(pawn.Map);
        int myJobId = pawn.jobs?.curJob?.loadID ?? -1;
        int partnerJobId = Partner.jobs?.curJob?.loadID ?? -1;

        bool selfManual = store != null && store.TryGetChannel(myJobId, out string selfChannel) && selfChannel == Despicable.Core.Channels.ManualLovin;
        bool partnerManual = store != null && store.TryGetChannel(partnerJobId, out string partnerChannel) && partnerChannel == Despicable.Core.Channels.ManualLovin;

        if (selfManual)
            return LovinUtil.PassesManualLovinCheck(pawn, Partner, out _);

        if (partnerManual)
            return LovinUtil.PassesManualLovinCheck(Partner, pawn, out _);

        return LovinUtil.PassesLovinCheck(pawn, Partner, ordered: true);
    }

    protected bool PartnerPresent(Pawn partner) =>
        partner.jobs.curDriver is JobDriver_LovinBase driver && driver.Partner == pawn;

    private static ThoughtDef GetManualLovinBoundaryThoughtDef(LovinUtil.ManualLovinBoundaryCause cause)
    {
        if (cause == LovinUtil.ManualLovinBoundaryCause.Ideology)
            return DefDatabase<ThoughtDef>.GetNamedSilentFail("D2N_Thought_ManualLovinBoundary_Ideology");

        if (cause == LovinUtil.ManualLovinBoundaryCause.Orientation)
            return DefDatabase<ThoughtDef>.GetNamedSilentFail("D2N_Thought_ManualLovinBoundary_Orientation");

        if (cause == LovinUtil.ManualLovinBoundaryCause.Relation)
            return DefDatabase<ThoughtDef>.GetNamedSilentFail("D2N_Thought_ManualLovinBoundary_Relation");

        return null;
    }

    private void TryApplyManualBoundaryThoughtAtLovinStart()
    {
        if (Despicable.NSFW.Integrations.IntegrationGuards.ShouldUseIntimacyForLovinValidation())
            return;

        Map map = pawn.Map;
        if (map == null || Partner == null)
            return;

        var store = Despicable.Core.InteractionInstanceStore.Get(map);
        if (store == null)
            return;

        int myJobId = pawn.jobs?.curJob?.loadID ?? -1;
        int partnerJobId = Partner.jobs?.curJob?.loadID ?? -1;

        bool selfManual = store.TryGetChannel(myJobId, out string selfChannel) && selfChannel == Despicable.Core.Channels.ManualLovin;
        bool partnerManual = store.TryGetChannel(partnerJobId, out string partnerChannel) && partnerChannel == Despicable.Core.Channels.ManualLovin;

        Pawn orderedPawn = null;
        Pawn otherPawn = null;
        int orderedJobId = -1;

        if (selfManual)
        {
            orderedPawn = pawn;
            otherPawn = Partner;
            orderedJobId = myJobId;
        }
        else if (partnerManual)
        {
            orderedPawn = Partner;
            otherPawn = pawn;
            orderedJobId = partnerJobId;
        }
        else
        {
            return;
        }

        LovinUtil.ManualLovinBoundaryCause cause = LovinUtil.GetOrderedPawnManualBoundaryCause(orderedPawn, otherPawn);
        if (cause == LovinUtil.ManualLovinBoundaryCause.None)
            return;

        ThoughtDef thoughtDef = GetManualLovinBoundaryThoughtDef(cause);
        if (thoughtDef == null)
            return;

        if (!Despicable.Core.OutcomeHistory.TryApplyOnce(map, "LovinManualBoundaryThought", orderedPawn.thingIDNumber, otherPawn.thingIDNumber, thoughtDef.defName, orderedJobId))
            return;

        Thought_Memory memory = ThoughtMaker.MakeThought(thoughtDef) as Thought_Memory;
        if (memory == null)
            return;

        orderedPawn.needs?.mood?.thoughts?.memories?.TryGainMemory(memory, otherPawn);
    }

    private void TryStartLovinAnimation()
    {
        // Face each other, in case there's no animation
        pawn.rotationTracker.FaceTarget(Partner);
        Partner.rotationTracker.FaceTarget(pawn);

        // Stop partner's pathing
        Partner.jobs.curDriver.asleep = false;
        Partner.pather.StopDead();

        // ANIMATION (Core staging planner + playback backend)
        durationTicks = LovinUtil.DefaultDurationTicks;
        if (CommonUtil.GetSettings().animationExtensionEnabled)
            LovinStagePlayback.TryStartForJob(job, pawn, Partner, Bed, participants, out durationTicks);
    }

    private void TickParticipants()
    {
        foreach (Pawn participant in participants)
        {
            if (pawn.IsHashIntervalTick(LovinUtil.RestDepletionInterval))
                participant.needs.rest?.NeedInterval();

            if (pawn.IsHashIntervalTick(LovinUtil.TicksBetweenHearts))
                FleckMaker.ThrowMetaIcon(participant.Position, participant.Map, FleckDefOf.Heart);
        }
    }

    private bool ShouldFinishLovin()
    {
        CompExtendedAnimator animator = pawn.TryGetComp<CompExtendedAnimator>();
        if ((animator.animQueue.Count <= 0 && !animator.hasAnimPlaying)
            || (!animator.hasAnimPlaying && durationTicks <= 0))
        {
            return true;
        }

        return durationTicks <= 0;
    }

    private void ResumePartnerAndResetAnimators()
    {
        Partner.pather.StartPath(Target, PathEndMode.OnCell);
        AnimUtil.ResetAnimatorsForGroup(participants);
    }
}
