using RimWorld;
using Verse;
using Verse.AI;

namespace Despicable;

public class JoyGiver_SelfLovin : JoyGiver
{
    public override Job TryGiveJob(Pawn pawn)
    {
        if (pawn == null)
            return null;

        if (!CommonUtil.GetSettings().lovinExtensionEnabled)
            return null;

        if (pawn.needs?.joy == null)
            return null;

        LovinTypeDef lovinType = LovinUtil.FindAutonomousSelfLovinType(pawn);
        if (lovinType == null)
            return null;

        Job job = JobMaker.MakeJob(LovinModule_JobDefOf.Job_SelfLovin);
        LovinUtil.StampAutonomousLovinJob(job, pawn.Map, lovinType);
        return job;
    }
}
