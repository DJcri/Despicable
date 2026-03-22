using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Despicable;

public class JobDriver_SelfLovin : JobDriver
{
    private const TargetIndex iCell = TargetIndex.B;

    private readonly List<Pawn> participants = new();
    private int durationTicks = LovinUtil.SelfLovinDefaultDurationTicks;
    private bool hasStartedAnimation;
    private bool selfLovinPlaybackActive;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return true;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref durationTicks, "durationTicks", LovinUtil.SelfLovinDefaultDurationTicks);
        Scribe_Values.Look(ref hasStartedAnimation, "hasStartedAnimation", defaultValue: false);
        Scribe_Values.Look(ref selfLovinPlaybackActive, "selfLovinPlaybackActive", defaultValue: false);
    }

    internal bool IsSelfLovinPlaybackActive => selfLovinPlaybackActive;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        PreInit();

        Toil pickSpot = new Toil();
        pickSpot.defaultCompleteMode = ToilCompleteMode.Instant;
        pickSpot.initAction = delegate
        {
            if (SelfLovinSpotFinder.TryFindSpot(pawn, out IntVec3 spot))
                job.SetTarget(iCell, spot);
        };
        yield return pickSpot;

        if (job.GetTarget(iCell).IsValid && job.GetTarget(iCell).Cell != pawn.Position)
            yield return Toils_Goto.GotoCell(iCell, PathEndMode.OnCell);

        yield return SelfLovinToil();
        yield return FinalizeSelfLovinToil();
    }

    private void PreInit()
    {
        pawn.jobs.curJob.playerForced = false;
        AddEndCondition(() => pawn == null || !pawn.Spawned || pawn.Dead ? JobCondition.Incompletable : JobCondition.Ongoing);
        AddEndCondition(() => !LovinUtil.PassesSelfLovinCheck(pawn, GetChosenLovinType(), out _) ? JobCondition.Incompletable : JobCondition.Ongoing);
    }

    private LovinTypeDef GetChosenLovinType()
    {
        return LovinJobStageUtil.GetChosenLovinType(pawn?.CurJob, pawn?.Map);
    }

    private Toil SelfLovinToil()
    {
        Toil toil = new Toil();
        toil.defaultCompleteMode = ToilCompleteMode.Never;
        toil.handlingFacing = false;
        toil.socialMode = RandomSocialMode.Off;
        toil.initAction = delegate
        {
            participants.Clear();
            participants.Add(pawn);

            durationTicks = LovinUtil.SelfLovinDefaultDurationTicks;
            hasStartedAnimation = false;
            selfLovinPlaybackActive = true;
            ReapplySelfLovinRuntimeState(refreshVisuals: true);
            hasStartedAnimation = TryStartSelfLovinAnimation();
        };
        toil.AddPreTickAction(delegate
        {
            durationTicks--;

            if (selfLovinPlaybackActive)
                ReapplySelfLovinRuntimeState(refreshVisuals: false);

            TickParticipant();
            if (ShouldFinishSelfLovin())
                ReadyForNextToil();

            if (durationTicks <= 0)
                ReadyForNextToil();
        });
        toil.AddFinishAction(delegate
        {
            selfLovinPlaybackActive = false;
            hasStartedAnimation = false;
            LovinVisualRuntime.SetLovinVisualActive(pawn, false);
            AnimUtil.ResetAnimatorsForGroup(participants);
        });
        return toil;
    }

    private Toil FinalizeSelfLovinToil()
    {
        Toil toil = new Toil();
        toil.defaultCompleteMode = ToilCompleteMode.Instant;
        toil.initAction = delegate
        {
            Map map = pawn.Map;
            Job currentJob = pawn.CurJob;
            int jobId = currentJob?.loadID ?? -1;
            var store = map != null ? Despicable.Core.InteractionInstanceStore.Get(map) : null;

            if (map != null)
            {
                string interactionDefName = null;
                store?.TryGet(jobId, out interactionDefName);
                string onceKey = interactionDefName.NullOrEmpty() ? "SelfLovinFinalize" : $"SelfLovinFinalize:{interactionDefName}";

                if (!Despicable.Core.OutcomeHistory.TryApplyOnce(map, onceKey, pawn.thingIDNumber, 0, interactionDefName ?? "Self", jobId))
                {
                    store?.Clear(jobId);
                    return;
                }
            }

            Thought_Memory memory = ThoughtMaker.MakeThought(LovinModule_ThoughtDefOf.D2N_Thought_SelfLovin) as Thought_Memory;
            if (memory != null)
                pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(memory);

            if (pawn.needs?.joy != null)
                pawn.needs.joy.GainJoy(0.20f, JoyKindDefOf.Social);

            string channel = null;
            bool isManualSelf = store != null && store.TryGetChannel(jobId, out channel) && channel == Despicable.Core.Channels.ManualSelfLovin;
            if (!isManualSelf)
                pawn.mindState.canLovinTick = Find.TickManager.TicksGame + LovinUtil.SelfLovinDefaultDurationTicks;

            store?.Clear(jobId);
        };
        return toil;
    }

    private bool TryStartSelfLovinAnimation()
    {
        ReapplySelfLovinRuntimeState(refreshVisuals: false);

        if (!CommonUtil.GetSettings().animationExtensionEnabled)
            return false;

        return SelfLovinStagePlayback.TryStartForJob(job, pawn, pawn, participants, out durationTicks);
    }

    internal void ReapplySelfLovinRuntimeState(bool refreshVisuals = true)
    {
        if (!selfLovinPlaybackActive)
            return;

        participants.Clear();
        participants.Add(pawn);

        LovinVisualRuntime.SetLovinVisualActive(pawn, true, refreshVisuals);

        if (pawn?.jobs?.curDriver != null)
            pawn.jobs.curDriver.asleep = false;
        pawn?.pather?.StopDead();
    }

    private void TickParticipant()
    {
        if (pawn.IsHashIntervalTick(LovinUtil.RestDepletionInterval))
            pawn.needs?.rest?.NeedInterval();

        if (pawn.IsHashIntervalTick(LovinUtil.TicksBetweenHearts))
            FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Heart);
    }

    private bool ShouldFinishSelfLovin()
    {
        if (durationTicks <= 0)
            return true;

        if (!hasStartedAnimation)
            return false;

        CompExtendedAnimator animator = pawn.TryGetComp<CompExtendedAnimator>();
        if (animator == null)
            return false;

        return animator.animQueue.Count <= 0 && !animator.hasAnimPlaying;
    }
}
