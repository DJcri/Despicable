using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable.NSFW.Integrations;
/// <summary>
/// Instance-owned runtime cache for short-lived lovin context handoff.
/// Keeps mutable state out of static fields while preserving the existing bridge API.
/// </summary>
internal sealed class LovinContextRuntimeState
{
    private readonly Dictionary<long, LovinContextBridge.Context> contextsByPairKey = new(256);

    private static long BuildPairKey(int a, int b)
    {
        int min = a < b ? a : b;
        int max = a < b ? b : a;
        return ((long)min << 32) | (uint)max;
    }

    public void Set(Pawn initiator, Pawn partner, int jobLoadId, string lovinTypeDefName, int ttlTicks)
    {
        if (initiator == null || partner == null) return;

        int now = Find.TickManager?.TicksGame ?? 0;
        var ctx = new LovinContextBridge.Context
        {
            InitiatorId = initiator.thingIDNumber,
            PartnerId = partner.thingIDNumber,
            JobLoadId = jobLoadId,
            LovinTypeDefName = lovinTypeDefName,
            ExpiresAtTick = now + (ttlTicks > 0 ? ttlTicks : 1)
        };

        contextsByPairKey[BuildPairKey(ctx.InitiatorId, ctx.PartnerId)] = ctx;
    }

    public bool TryGet(Pawn a, Pawn b, out LovinContextBridge.Context ctx)
    {
        ctx = default;
        if (a == null || b == null) return false;

        long pairKey = BuildPairKey(a.thingIDNumber, b.thingIDNumber);
        int now = Find.TickManager?.TicksGame ?? 0;
        if (contextsByPairKey.TryGetValue(pairKey, out ctx))
        {
            if (ctx.ExpiresAtTick >= now) return true;
            contextsByPairKey.Remove(pairKey);
        }

        return false;
    }

    public void Clear(Pawn a, Pawn b)
    {
        if (a == null || b == null) return;
        contextsByPairKey.Remove(BuildPairKey(a.thingIDNumber, b.thingIDNumber));
    }

    public void ResetRuntimeState()
    {
        contextsByPairKey.Clear();
    }
}
