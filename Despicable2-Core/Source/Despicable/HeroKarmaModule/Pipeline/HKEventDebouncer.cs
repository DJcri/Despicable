using System.Collections.Generic;
using Verse;

namespace Despicable.HeroKarma;
public static class HKEventDebouncer
{
    private struct DebounceState
    {
        public int lastTick;
        public int lastStage;
        public int countInWindow;
        public int windowStartTick;
    }

    private static readonly Dictionary<string, DebounceState> stateByKey = new(2048);
    private static readonly List<string> pruneBuffer = new();

    // Prevent slow-burn growth in long sessions.
    private const int MaxEntries = 5000;
    private const int StaleTicks = 120000; // ~2 in-game days at 60k ticks/day
    private static readonly HKPruneTickTracker PruneTracker = new();

    public static bool ShouldProcess(string eventKey, string actorPawnId, string targetPawnId, int targetFactionId,
        int stage, int cooldownTicks, int windowTicks, int maxPerWindow)
    {
        string _;
        return ShouldProcess(eventKey, actorPawnId, targetPawnId, targetFactionId, stage, cooldownTicks, windowTicks, maxPerWindow, out _);
    }

    public static bool ShouldProcess(string eventKey, string actorPawnId, string targetPawnId, int targetFactionId,
        int stage, int cooldownTicks, int windowTicks, int maxPerWindow, out string dropReason)
    {
        dropReason = null;

        if (eventKey.NullOrEmpty() || actorPawnId.NullOrEmpty())
            return true;

        int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
        MaybePrune(now);
        string key = BuildKey(eventKey, actorPawnId, targetPawnId, targetFactionId);

        DebounceState s;
        if (!stateByKey.TryGetValue(key, out s))
        {
            s.lastTick = -999999;
            s.lastStage = int.MinValue;
            s.countInWindow = 0;
            s.windowStartTick = now;
        }

        if (windowTicks > 0 && now - s.windowStartTick >= windowTicks)
        {
            s.windowStartTick = now;
            s.countInWindow = 0;
        }

        bool escalation = stage > s.lastStage;

        if (maxPerWindow > 0 && windowTicks > 0 && s.countInWindow >= maxPerWindow && !escalation)
        {
            dropReason = "window_cap";
            stateByKey[key] = s;
            return false;
        }

        if (cooldownTicks > 0 && (now - s.lastTick) < cooldownTicks && !escalation)
        {
            dropReason = "cooldown";
            stateByKey[key] = s;
            return false;
        }

        s.lastTick = now;
        s.lastStage = stage;
        if (maxPerWindow > 0 && windowTicks > 0)
            s.countInWindow++;

        stateByKey[key] = s;
        return true;
    }

    
    private static void MaybePrune(int now)
    {
        // Cheap throttle: prune at most once per in-game day, or when dictionary is too large.
        if (PruneTracker.ShouldSkipPrune(stateByKey.Count, MaxEntries, now, 60000)) return;

        PruneTracker.MarkPruned(now);
        if (stateByKey.Count == 0) return;

        try
        {
            pruneBuffer.Clear();
            foreach (var kv in stateByKey)
            {
                if (now - kv.Value.lastTick > StaleTicks)
                    pruneBuffer.Add(kv.Key);
            }

            for (int i = 0; i < pruneBuffer.Count; i++)
                stateByKey.Remove(pruneBuffer[i]);
        }
        catch
        {
            // Never let pruning break gameplay.
        }
    }

    public static void ResetRuntimeState()
    {
        stateByKey.Clear();
        PruneTracker.Reset();
    }

    private static string BuildKey(string eventKey, string actorPawnId, string targetPawnId, int targetFactionId)
    {
        return eventKey + "|" + actorPawnId + "|" + (targetPawnId ?? "") + "|" + targetFactionId.ToString();
    }
}
