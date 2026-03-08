using RimWorld;
using Verse;

namespace Despicable.NSFW.Integrations;
/// <summary>
/// A tiny, best-effort context bridge between:
///  - "we started an animation for this pair/job"
///  - "a third-party mod is currently resolving pregnancy / cooldown / thoughts"
///
/// Mutable runtime state now lives in an instance-owned cache so the public bridge stays simple
/// without parking live workflow data in static fields.
/// </summary>
internal static class LovinContextBridge
{
    internal struct Context
    {
        public int InitiatorId;
        public int PartnerId;
        public int JobLoadId;
        public string LovinTypeDefName; // e.g. "Vaginal", "Anal", etc.
        public int ExpiresAtTick;
    }

    private static readonly LovinContextRuntimeState runtimeState = new();

    internal static void Set(Pawn initiator, Pawn partner, int jobLoadId, string lovinTypeDefName, int ttlTicks = 2500)
    {
        runtimeState.Set(initiator, partner, jobLoadId, lovinTypeDefName, ttlTicks);
    }

    internal static bool TryGet(Pawn a, Pawn b, out Context ctx)
    {
        return runtimeState.TryGet(a, b, out ctx);
    }

    internal static void Clear(Pawn a, Pawn b)
    {
        runtimeState.Clear(a, b);
    }

    internal static void ResetRuntimeState()
    {
        runtimeState.ResetRuntimeState();
    }
}
