using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Despicable; 
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;

namespace Despicable;
public class JobDriver_LovinNoBed : JobDriver_LovinBase
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(Target, job, LovinUtil.MaxLovinPartners, 0, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        PreInit();

        JobDef partnerJobDef = LovinModule_JobDefOf.Job_GiveLovin;

        Toil pickPrivacyCell = new();
        pickPrivacyCell.defaultCompleteMode = ToilCompleteMode.Instant;
        pickPrivacyCell.initAction = delegate
        {
            if (LovinPrivacyCellFinder.TryFindPrivacyCell(pawn, Partner, out IntVec3 privacyCell))
                job.SetTarget(iCell, privacyCell);
        };
        yield return pickPrivacyCell;

        if (job.GetTarget(iCell).IsValid)
            yield return Toils_Goto.GotoCell(iCell, PathEndMode.OnCell);
        else
            yield return Toils_Goto.GotoThing(iTarget, PathEndMode.OnCell);

        Toil startPartnerJob = new();
        startPartnerJob.defaultCompleteMode = ToilCompleteMode.Instant;
        startPartnerJob.socialMode = RandomSocialMode.Off;
        startPartnerJob.initAction = delegate
        {
            Job partnerJob = JobMaker.MakeJob(partnerJobDef, pawn);
            Partner.jobs.StartJob(partnerJob, JobCondition.InterruptForced);
        };
        yield return startPartnerJob;

        Toil waitForPartner = new();
        waitForPartner.defaultCompleteMode = ToilCompleteMode.Delay;
        waitForPartner.initAction = delegate
        {
            ticksLeftThisToil = 5000;
        };
        waitForPartner.tickAction = delegate
        {
            pawn.GainComfortFromCellIfPossible(durationTicks - 1);
            if (pawn.Position.DistanceTo(Partner.Position) <= 1f)
            {
                ReadyForNextToil();
            }
        };
        yield return waitForPartner;

        yield return LovinToil();

        // Finalize lovin
        yield return FinalizeLovinToil();
    }
}
