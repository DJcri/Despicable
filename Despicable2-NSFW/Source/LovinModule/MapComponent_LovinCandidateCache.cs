using System.Collections.Generic;
using Verse;

namespace Despicable;
/// <summary>
/// Broad, low-risk candidate cache used to avoid repeated full-map pawn scans.
/// Final lovin validation should still happen elsewhere.
/// </summary>
public class MapComponent_LovinCandidateCache : MapComponent
{
    private readonly List<Pawn> candidates = new();
    private int lastRefreshTick = -99999;
    private const int RefreshInterval = 120;

    public MapComponent_LovinCandidateCache(Map map) : base(map)
    {
    }

    public List<Pawn> GetCandidates()
    {
        RefreshIfNeeded();
        return candidates;
    }

    public static List<Pawn> GetCandidatesFor(Map map)
    {
        if (map == null)
            return Empty();

        return map.GetComponent<MapComponent_LovinCandidateCache>()?.GetCandidates() ?? Empty();
    }

    public void MarkDirty()
    {
        lastRefreshTick = -99999;
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        MarkDirty();
    }

    public override void MapComponentTick()
    {
        // Keep the cache warm on a modest interval so hot callers can just read.
        RefreshIfNeeded();
    }

    private void RefreshIfNeeded()
    {
        int currentTick = Find.TickManager?.TicksGame ?? 0;
        if (currentTick - lastRefreshTick < RefreshInterval)
            return;

        lastRefreshTick = currentTick;
        candidates.Clear();

        if (map?.mapPawns?.AllPawnsSpawned == null)
            return;

        List<Pawn> spawned = (List<Pawn>)map.mapPawns.AllPawnsSpawned;
        for (int i = 0; i < spawned.Count; i++)
        {
            Pawn pawn = spawned[i];
            if (pawn == null || pawn.Dead)
                continue;
            if (pawn.RaceProps?.Humanlike != true)
                continue;

            candidates.Add(pawn);
        }
    }

    private static List<Pawn> Empty()
    {
        return new List<Pawn>();
    }
}
