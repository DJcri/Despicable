using System.Collections.Generic;
using Verse;
using RimWorld;
using Verse.AI;

namespace Despicable;

/// <summary>
/// Solo staging wrapper that builds a one-pawn StageRequest and reuses the Core planner/backends unchanged.
/// </summary>
public static class SelfLovinStagePlayback
{
    public static bool TryStartForJob(Job job, Pawn pawn, Thing anchorThing, List<Pawn> participants, out int durationTicks)
    {
        durationTicks = LovinUtil.SelfLovinDefaultDurationTicks;

        if (pawn == null) return false;

        participants?.Clear();
        if (participants != null)
            participants.Add(pawn);

        if (!CommonUtil.GetSettings().animationExtensionEnabled) return false;

        string stageTag = LovinJobStageUtil.GetResolvedStageTag(job, pawn.Map);
        if (stageTag.NullOrEmpty())
            return false;

        var stageReq = new Despicable.Core.Staging.StageRequest
        {
            StageTag = stageTag,
            Anchor = anchorThing ?? pawn
        };

        stageReq.Participants.Add(pawn);

        if (Despicable.Core.Staging.StagePlanner.TryPlan(
                pawn.Map,
                stageReq,
                interactionContext: null,
                out var stagePlan,
                out _))
        {
            return Despicable.Core.Staging.StagePlaybackBackends.TryPlay(stagePlan);
        }

        return false;
    }
}
