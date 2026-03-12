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
        StartOrderedResolvedJob(
            fallbackJobDef,
            pawn,
            target.Pawn?.thingIDNumber ?? 0,
            req,
            ctx,
            jobDef => JobMaker.MakeJob(jobDef, target),
            key => $"Blocked duplicate start key={key}",
            res => $"No JobDef after resolve id={res.ChosenInteractionId} stage={res.ChosenStageId}",
            res => $"Resolve blocked allowed={res?.Allowed ?? false} reason={res?.Reason}");
    }


    /// <summary>
    /// Core-aware ordered self job with idempotence + per-job instance tracking.
    /// </summary>
    public static void OrderedSelfJob(
        JobDef fallbackJobDef,
        Pawn pawn,
        Core.InteractionRequest req,
        Core.InteractionContext ctx)
    {
        StartOrderedResolvedJob(
            fallbackJobDef,
            pawn,
            0,
            req,
            ctx,
            jobDef => JobMaker.MakeJob(jobDef),
            key => $"Blocked duplicate self start key={key}",
            res => $"No Self JobDef after resolve id={res.ChosenInteractionId} stage={res.ChosenStageId}",
            res => $"Resolve self blocked allowed={res?.Allowed ?? false} reason={res?.Reason}");
    }


    private static void StartOrderedResolvedJob(
        JobDef fallbackJobDef,
        Pawn pawn,
        int recipientId,
        Core.InteractionRequest req,
        Core.InteractionContext ctx,
        System.Func<JobDef, Job> createJob,
        System.Func<Core.InteractionKey, string> duplicateLog,
        System.Func<Core.InteractionResolution, string> noJobLog,
        System.Func<Core.InteractionResolution, string> blockedLog)
    {
        if (pawn == null) return;

        int tick = ctx?.Tick ?? Find.TickManager.TicksGame;
        int bucket = Core.InteractionRegistry.GetBucket(tick);

        string id = req?.RequestedInteractionId ?? fallbackJobDef?.defName ?? "NULL";
        if ((bool)!req?.RequestedStageId.NullOrEmpty())
            id = $"{id}:{req.RequestedStageId}";

        var key = new Core.InteractionKey(pawn.thingIDNumber, recipientId, id, bucket);
        if (!Core.InteractionRegistry.TryRegister(pawn.Map, key))
        {
            Core.DebugLogger.Debug(duplicateLog?.Invoke(key) ?? $"Blocked duplicate start key={key}");
            return;
        }

        var res = Core.Resolver.Resolve(req, ctx);
        if (res == null || !res.Allowed)
        {
            Core.DebugLogger.Debug(blockedLog?.Invoke(res) ?? $"Resolve blocked allowed={res?.Allowed ?? false} reason={res?.Reason}");
            return;
        }

        JobDef jobDef = res.ChosenJobDef ?? fallbackJobDef;
        if (jobDef == null)
        {
            Core.DebugLogger.Debug(noJobLog?.Invoke(res) ?? $"No JobDef after resolve id={res?.ChosenInteractionId} stage={res?.ChosenStageId}");
            return;
        }

        Job job = createJob != null
            ? createJob(jobDef)
            : JobMaker.MakeJob(jobDef);

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
