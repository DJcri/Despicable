using RimWorld;
using Verse;
using Verse.AI;

namespace Despicable;
public static class Interactions
{
    /// <summary>
    /// Vanilla-style ordered job. (No content-specific stamping.)
    /// </summary>
    public static void OrderedJob(JobDef jobDef, Pawn pawn, LocalTargetInfo target)
    {
        if (jobDef == null || pawn == null) return;

        Job job = JobMaker.MakeJob(jobDef, target);
        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        pawn.jobs.TryTakeOrderedJob(job);
    }

    /// <summary>
    /// Core-aware ordered job with idempotence + per-job instance tracking.
    /// 

    /// This method is content-agnostic: it does not reference any external module types.
    /// Modules should set req.RequestedInteractionId / req.RequestedStageId and resolve via hooks.
    /// </summary>
    public static void OrderedJob(
        JobDef fallbackJobDef,
        Pawn pawn,
        LocalTargetInfo target,
        Core.InteractionRequest req,
        Core.InteractionContext ctx)
    {
        if (pawn == null) return;

        int tick = ctx?.Tick ?? Find.TickManager.TicksGame;
        int bucket = Core.InteractionRegistry.GetBucket(tick);

        string id = req?.RequestedInteractionId ?? fallbackJobDef?.defName ?? "NULL";
        if ((bool)!req?.RequestedStageId.NullOrEmpty())
            id = $"{id}:{req.RequestedStageId}";

        int recipientId = target.Pawn?.thingIDNumber ?? 0;
        var key = new Core.InteractionKey(pawn.thingIDNumber, recipientId, id, bucket);

        bool ok = Core.InteractionRegistry.TryRegister(pawn.Map, key);
        if (!ok)
        {
            Core.DebugLogger.Debug($"Blocked duplicate start key={key}");
            return;
        }

        // Resolve (hooks may fill in job defs, animation, etc.)
        var res = Core.Resolver.Resolve(req, ctx);
        if (res == null || !res.Allowed)
        {
            Core.DebugLogger.Debug($"Resolve blocked allowed={res?.Allowed ?? false} reason={res?.Reason}");
            return;
        }

        JobDef jobDef = res.ChosenJobDef ?? fallbackJobDef;
        if (jobDef == null)
        {
            Core.DebugLogger.Debug($"No JobDef after resolve id={res.ChosenInteractionId} stage={res.ChosenStageId}");
            return;
        }

        // Create job HERE so we can store by job.loadID
        Job job = JobMaker.MakeJob(jobDef, target);

        // Record resolved interaction per job instance (safe for multiple pairs)
        var store = Core.InteractionInstanceStore.Get(pawn.Map);
        if (store != null)
        {
            store.Set(job.loadID, res.ChosenInteractionDef?.defName ?? res.ChosenInteractionId);
            store.SetStage(job.loadID, res.ChosenStageId);
            store.SetChannel(job.loadID, req?.Channel);
        }

        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        pawn.jobs.TryTakeOrderedJob(job);
    }
}
