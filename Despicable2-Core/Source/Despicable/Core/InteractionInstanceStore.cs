using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Despicable.Core;
public class InteractionInstanceStore : MapComponent
{
    private const int PruneIntervalTicks = 2500;
    private int nextPruneTick;

    // job.loadID -> interactionDefName
    private Dictionary<int, string> jobToInteraction = new();

    // job.loadID -> stageId (opaque content key, e.g. LovinTypeDef.defName)
    private Dictionary<int, string> jobToStage = new();

    // job.loadID -> request channel (used for compatibility routing, e.g. ManualLovin)
    private Dictionary<int, string> jobToChannel = new();

    // job.loadID -> NL lovin-active window. Optional explicit control from content jobs.
    // When absent/false, the NL compatibility layer can still fall back to live animator state.
    private Dictionary<int, bool> jobToNLLovinActive = new();

    public InteractionInstanceStore(Map map) : base(map) { }

    public static InteractionInstanceStore Get(Map map) => map?.GetComponent<InteractionInstanceStore>();

    public void Set(int jobId, string interactionDefName)
    {
        if (jobId < 0 || interactionDefName.NullOrEmpty()) return;
        jobToInteraction[jobId] = interactionDefName;
    }

    public void SetStage(int jobId, string stageId)
    {
        if (jobId < 0 || stageId.NullOrEmpty()) return;
        jobToStage[jobId] = stageId;
    }

    public bool TryGet(int jobId, out string interactionDefName)
        => jobToInteraction.TryGetValue(jobId, out interactionDefName);

    public bool TryGetStage(int jobId, out string stageId)
        => jobToStage.TryGetValue(jobId, out stageId);

    public void SetChannel(int jobId, string channel)
    {
        if (jobId < 0 || channel.NullOrEmpty()) return;
        jobToChannel[jobId] = channel;
    }

    public bool TryGetChannel(int jobId, out string channel)
        => jobToChannel.TryGetValue(jobId, out channel);

    public void SetNLLovinActive(int jobId, bool active)
    {
        if (jobId < 0) return;

        if (active)
            jobToNLLovinActive[jobId] = true;
        else
            jobToNLLovinActive.Remove(jobId);
    }

    public bool IsNLLovinActive(int jobId)
        => jobId >= 0 && jobToNLLovinActive.TryGetValue(jobId, out bool active) && active;

    public void Clear(int jobId)
    {
        if (jobId < 0) return;
        jobToInteraction.Remove(jobId);
        jobToStage.Remove(jobId);
        jobToChannel.Remove(jobId);
        jobToNLLovinActive.Remove(jobId);
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        int now = Find.TickManager?.TicksGame ?? 0;
        if (now < nextPruneTick)
            return;

        nextPruneTick = now + PruneIntervalTicks;
        PruneToActiveJobs();
    }

    private void PruneToActiveJobs()
    {
        if (map?.mapPawns == null)
            return;

        HashSet<int> activeJobIds = new();
        var pawns = map.mapPawns.AllPawnsSpawned;
        if (pawns != null)
        {
            for (int i = 0; i < pawns.Count; i++)
            {
                Job curJob = pawns[i]?.CurJob;
                if (curJob != null && curJob.loadID >= 0)
                    activeJobIds.Add(curJob.loadID);
            }
        }

        PruneDictionary(jobToInteraction, activeJobIds);
        PruneDictionary(jobToStage, activeJobIds);
        PruneDictionary(jobToChannel, activeJobIds);
        PruneDictionary(jobToNLLovinActive, activeJobIds);
    }

    private static void PruneDictionary<T>(Dictionary<int, T> dict, HashSet<int> activeJobIds)
    {
        if (dict == null || dict.Count == 0)
            return;

        List<int> remove = null;
        foreach (int jobId in dict.Keys)
        {
            if (activeJobIds.Contains(jobId))
                continue;

            remove ??= new List<int>();
            remove.Add(jobId);
        }

        if (remove == null)
            return;

        for (int i = 0; i < remove.Count; i++)
            dict.Remove(remove[i]);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref jobToInteraction, "despicableJobToInteraction", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref jobToStage, "despicableJobToStage", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref jobToChannel, "despicableJobToChannel", LookMode.Value, LookMode.Value);
        Scribe_Collections.Look(ref jobToNLLovinActive, "despicableJobToNLLovinActive", LookMode.Value, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (jobToInteraction == null)
                jobToInteraction = new Dictionary<int, string>();
            if (jobToStage == null)
                jobToStage = new Dictionary<int, string>();
            if (jobToChannel == null)
                jobToChannel = new Dictionary<int, string>();
            if (jobToNLLovinActive == null)
                jobToNLLovinActive = new Dictionary<int, bool>();

            nextPruneTick = 0;
            PruneToActiveJobs();
        }
    }
}
