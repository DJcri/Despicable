using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;
using Verse.AI;
using static UnityEngine.Experimental.Rendering.RayTracingAccelerationStructure;
using Despicable.NSFW.Integrations;

namespace Despicable;
public class JobGiver_GetBedLovin : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)
    {
        if (IntegrationGuards.ShouldDeferLovinToIntimacy())
            return null;

        if (!LovinUtil.CouldUseSomeLovin(pawn))
            return null;

        if (pawn?.CurJob == null || pawn?.CurJob?.def == JobDefOf.LayDown)
        {
            Pawn partner = LovinUtil.FindPartner(pawn, true);

            if (partner == null)
                return null;
            if (!CommonUtil.GetSettings().lovinExtensionEnabled)
                return null;

            // Can never be null, since find checks for bed.
            Building_Bed bed = partner.CurrentBed();

            // Interrupt current job for bed lovin'
            if (pawn?.CurJob != null && pawn?.jobs?.curDriver != null)
                pawn?.jobs?.curDriver?.EndJobWith(JobCondition.InterruptForced);

            return JobMaker.MakeJob(LovinModule_JobDefOf.Job_GetBedLovin, partner, bed);
        }

        return null;
    }
}
