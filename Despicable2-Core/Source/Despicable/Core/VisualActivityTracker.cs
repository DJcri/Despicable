using System.Collections.Generic;
using Verse;

namespace Despicable;
/// <summary>
/// Lightweight global activity flags for hot render-path fast exits.
/// Uses HashSets so duplicate register and unregister calls remain harmless.
/// </summary>
public static class VisualActivityTracker
{
    private static readonly HashSet<int> extendedAnimatorPawnIds = new();
    private static readonly HashSet<int> lovinVisualPawnIds = new();

    public static bool AnyExtendedAnimatorsActive => extendedAnimatorPawnIds.Count > 0;
    public static bool AnyLovinVisualsActive => lovinVisualPawnIds.Count > 0;

    public static bool IsExtendedAnimatorActive(Pawn pawn)
    {
        return Contains(extendedAnimatorPawnIds, pawn);
    }

    public static void SetExtendedAnimatorActive(Pawn pawn, bool isActive)
    {
        SetState(extendedAnimatorPawnIds, pawn, isActive);
        pawn?.TryGetComp<CompFaceParts>()?.NotifyExtendedAnimatorStateChanged(isActive);
        FaceRuntimeActivityManager.NotifyExtendedAnimatorState(pawn, isActive);
    }

    public static void SetLovinVisualActive(Pawn pawn, bool isActive)
    {
        SetState(lovinVisualPawnIds, pawn, isActive);
    }

    public static bool IsLovinVisualActive(Pawn pawn)
    {
        return Contains(lovinVisualPawnIds, pawn);
    }

    public static void ResetRuntimeState()
    {
        extendedAnimatorPawnIds.Clear();
        lovinVisualPawnIds.Clear();
    }

    public static void ClearAll()
    {
        ResetRuntimeState();
    }

    private static bool Contains(HashSet<int> pawnIds, Pawn pawn)
    {
        return pawn != null && pawnIds.Contains(pawn.thingIDNumber);
    }

    private static void SetState(HashSet<int> pawnIds, Pawn pawn, bool isActive)
    {
        if (pawn == null)
            return;

        int pawnId = pawn.thingIDNumber;
        if (isActive)
            pawnIds.Add(pawnId);
        else
            pawnIds.Remove(pawnId);
    }
}
