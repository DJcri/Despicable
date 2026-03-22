using System;
using System.Collections.Generic;
using Verse;

namespace Despicable;

[Flags]
internal enum FacePartsEventMask
{
    None = 0,
    Drafted = 1 << 0,
    Job = 1 << 1,
    Mental = 1 << 2,
    Health = 1 << 3,
    Rest = 1 << 4,
    LifeStage = 1 << 5,
    Structure = 1 << 6,
    All = Drafted | Job | Mental | Health | Rest | LifeStage | Structure
}

internal static class FacePartsEventRuntime
{
    private static readonly Dictionary<int, FacePartsEventMask> PendingEventsByPawnId = new();
    // Guardrail-Allow-Static: One runtime-wide pending-event gate, reset on new game/load via DespicableRuntimeState.
    private static bool hasPendingEvents;

    internal static bool HasPendingEvents => hasPendingEvents;

    internal static void Queue(Pawn pawn, FacePartsEventMask mask)
    {
        if (pawn == null || mask == FacePartsEventMask.None)
            return;

        int pawnId = pawn.thingIDNumber;
        if (PendingEventsByPawnId.TryGetValue(pawnId, out FacePartsEventMask existing))
        {
            if ((existing & mask) == mask)
                return;

            PendingEventsByPawnId[pawnId] = existing | mask;
        }
        else
        {
            PendingEventsByPawnId[pawnId] = mask;
        }

        hasPendingEvents = true;
    }

    internal static bool TryConsume(Pawn pawn, out FacePartsEventMask mask)
    {
        mask = FacePartsEventMask.None;
        if (pawn == null || !hasPendingEvents)
            return false;

        int pawnId = pawn.thingIDNumber;
        if (!PendingEventsByPawnId.TryGetValue(pawnId, out mask) || mask == FacePartsEventMask.None)
            return false;

        PendingEventsByPawnId.Remove(pawnId);
        if (PendingEventsByPawnId.Count == 0)
            hasPendingEvents = false;

        return true;
    }

    internal static void ResetRuntimeState()
    {
        PendingEventsByPawnId.Clear();
        hasPendingEvents = false;
    }
}
