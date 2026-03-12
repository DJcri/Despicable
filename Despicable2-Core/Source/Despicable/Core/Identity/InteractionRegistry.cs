using System.Collections.Generic;
using Verse;

namespace Despicable.Core;
/// <summary>
/// In-session de-dupe for starting interactions. Not saved.
/// Prevents rapid double-starts and re-entrancy from ordering the same job repeatedly.
/// </summary>
public static class InteractionRegistry
{
    private static readonly Dictionary<int, HashSet<InteractionKey>> activeKeysByMapId = new();

    // Keep a short window; 30 ticks = 0.5 seconds at 60 TPS.
    public const int TickBucketSize = 30;

    public static int GetBucket(int tick)
    {
        return tick / TickBucketSize;
    }

    public static bool TryRegister(Map map, InteractionKey key)
    {
        if (map == null)
            return true; // fail-open

        int mapId = map.uniqueID;
        if (!activeKeysByMapId.TryGetValue(mapId, out var activeKeys))
        {
            activeKeys = new HashSet<InteractionKey>();
            activeKeysByMapId[mapId] = activeKeys;
        }

        // Clean old buckets opportunistically (cheap).
        // Only keep keys for current bucket and previous bucket.
        int currentBucket = key.TickBucket;
        activeKeys.RemoveWhere(k => k.TickBucket < currentBucket - 1);

        return activeKeys.Add(key);
    }

    public static void ResetRuntimeState()
    {
        activeKeysByMapId.Clear();
    }
}
