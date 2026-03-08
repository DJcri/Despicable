using System.Collections.Generic;
using Verse;
using RimWorld;
using Verse.AI;

namespace Despicable;
/// <summary>
/// Small helper to keep lovin job drivers thin: builds a StageRequest, plans, and plays via the Core backend.
/// Intentionally preserves existing in-game behavior (anchor selection + bed ejection).
/// </summary>
public static class LovinStagePlayback
{
    public static bool TryStartForJob(Job job, Pawn initiator, Pawn partner, Building_Bed bed, List<Pawn> participants, out int durationTicks)
    {
        durationTicks = LovinUtil.DefaultDurationTicks;

        if (initiator == null || partner == null) return false;

        participants?.Clear();
        if (participants != null)
        {
            participants.Add(initiator);
            if (!participants.Contains(partner))
                participants.Add(partner);
        }

        if (!CommonUtil.GetSettings().animationExtensionEnabled) return false;

        // If no bed, stage on initiator pawn's position (matches previous behavior)
        Thing anchorThing = bed != null ? (Thing)bed : initiator;

        // Prefer resolved stage id (manual selection) when present; fallback to job def extension.

        string stageTag = null;

        try

        {

            var store = Despicable.Core.InteractionInstanceStore.Get(initiator.Map);

            if (store != null && job != null && store.TryGetStage(job.loadID, out var stageId) && !stageId.NullOrEmpty())

                stageTag = stageId;

        }

        catch { /* ignore */ }


        if (stageTag.NullOrEmpty())

            stageTag = job?.def?.GetModExtension<ModExtension_LovinType>()?.lovinType?.defName;


        var stageReq = new Despicable.Core.Staging.StageRequest
        {
            StageTag = stageTag,
            Anchor = anchorThing
        };

        if (participants != null)
            stageReq.Participants.AddRange(participants);
        else
        {
            stageReq.Participants.Add(initiator);
            stageReq.Participants.Add(partner);
        }

        if (Despicable.Core.Staging.StagePlanner.TryPlan(
                initiator.Map,
                stageReq,
                interactionContext: null,
                out var stagePlan,
                out _))
        {
            // Eject partner from bed (matches old behavior)
            if (partner.InBed())
                partner.jobs.posture = PawnPosture.Standing;

            Despicable.Core.Staging.StagePlaybackBackends.TryPlay(stagePlan);
            return true;
        }

        return false;
    }
}
