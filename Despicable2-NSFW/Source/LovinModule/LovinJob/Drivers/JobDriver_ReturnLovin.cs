using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using Verse.AI;

namespace Despicable;
public class JobDriver_ReturnLovin : JobDriver_LovinBase
{
    public override void ExposeData()
    {
        base.ExposeData();
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        PreInit();

        // Bed lovin'
        if (Partner.CurJob.def == LovinModule_JobDefOf.Job_GetBedLovin)
        {
            this.KeepLyingDown(iBed);
            yield return Toils_Reserve.Reserve(iTarget, 1, 0);
            yield return Toils_Reserve.Reserve(iBed, Bed.SleepingSlotsCount, 0);

            yield return ReturnLovinToil();
        }
        // Lovin' on the spot
        else if (Partner.CurJob.def == LovinModule_JobDefOf.Job_GetLovin)
        {
            yield return Toils_Reserve.Reserve(iTarget, 1, 0);
            yield return Toils_Goto.GotoThing(iTarget, PathEndMode.Touch);

            var returnLovinToil = ReturnLovinToil();
            returnLovinToil.handlingFacing = false;

            yield return returnLovinToil;
        }
    }

    private Toil ReturnLovinToil()
    {
        var returnLovinToil
            = Partner.CurJob.def != LovinModule_JobDefOf.Job_GetBedLovin ? new Toil()
            : Toils_LayDown.LayDown(iBed, true, false, false, false);

        returnLovinToil.defaultCompleteMode = ToilCompleteMode.Never;
        returnLovinToil.socialMode = RandomSocialMode.Off;
        returnLovinToil.handlingFacing = true;
        returnLovinToil.AddFinishAction(() =>
        {
            pawn.Drawer.renderer.SetAllGraphicsDirty();
            GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
        });
        returnLovinToil.FailOn(() => !PartnerPresent(Partner));

        return returnLovinToil;
    }
}
