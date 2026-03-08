using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Despicable;
using RimWorld;
using Verse;
using Verse.AI;

namespace Despicable;
public class JobDriver_LovinBed : JobDriver_LovinBase
{
    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(Target, job, LovinUtil.MaxLovinPartners, 0, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        PreInit();

        JobDef PartnerJobDef = LovinModule_JobDefOf.Job_GiveLovin;

        // Vanilla Toils
        yield return Toils_Reserve.Reserve(iTarget, LovinUtil.MaxLovinPartners, 0);
        yield return Toils_Goto.GotoThing(iTarget, PathEndMode.OnCell);

        // Custom Toils
        Toil StartPartnerJob = new();
        StartPartnerJob.defaultCompleteMode = ToilCompleteMode.Instant;
        StartPartnerJob.socialMode = RandomSocialMode.Off;
        StartPartnerJob.initAction = delegate
        {
            Job partnerJob = JobMaker.MakeJob(PartnerJobDef, pawn, Bed);
            Partner.jobs.StartJob(partnerJob, JobCondition.InterruptForced);
        };

        yield return StartPartnerJob;
        yield return LovinToil();

        // Finalize lovin
        yield return FinalizeLovinToil();
    }
}
