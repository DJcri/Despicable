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
public class JobGiver_GetLovin : ThinkNode_JobGiver
{
    protected override Job TryGiveJob(Pawn pawn)
    {
        if (IntegrationGuards.ShouldDeferLovinToIntimacy())
            return null;

        if (!LovinUtil.CouldUseSomeLovin(pawn))
            return null;
        if (!CommonUtil.GetSettings().lovinExtensionEnabled)
            return null;

        // Find suitable partner
        Pawn partner = LovinUtil.FindPartner(pawn);
        if (partner == null || !pawn.CanReserveAndReach(partner, PathEndMode.Touch, Danger.Deadly, 1, -1, null, false))
        {
            return null;
        }

        return new Job(LovinModule_JobDefOf.Job_GetLovin, partner);
    }
}
