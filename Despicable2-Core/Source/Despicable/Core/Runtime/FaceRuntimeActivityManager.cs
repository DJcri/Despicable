using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable;

/// <summary>
/// Lightweight central registry for face-runtime relevance.
/// This begins the migration from per-comp activity ownership toward a shared active-pawn model
/// without changing face behavior aggressively.
/// </summary>
public static class FaceRuntimeActivityManager
{
    private static readonly Dictionary<int, int> recentlyRenderedUntilByPawnId = new();
    private static readonly Dictionary<int, int> explicitWakeUntilByPawnId = new();
    private static readonly Dictionary<int, int> editorHeartbeatUntilByPawnIdFrame = new();
    private static readonly HashSet<int> editorActivePawnIds = new();
    private static readonly HashSet<int> highPriorityRuntimePawnIds = new();
    private static readonly HashSet<int> activeRuntimePawnIds = new();

    // Comp registry — populated by PostSpawnSetup / cleared by PostDeSpawn and PostDestroy.
    // Only humanlike pawns are registered; everything else falls through CompTick normally.
    private static readonly Dictionary<int, CompFaceParts> compByPawnId = new();
    // Stable scratch list rebuilt when the registry changes.
    // Only valid for single-frame use from GameComponent_FacePartsTick.
    private static readonly List<CompFaceParts> registeredCompsScratch = new();
    private static bool registeredCompsScratchDirty = true;
    private static int runtimeGeneration = 1;

    public static int RuntimeGeneration => runtimeGeneration;

    public static void RegisterComp(CompFaceParts comp)
    {
        if (comp?.parent is not Pawn pawn)
            return;

        compByPawnId[pawn.thingIDNumber] = comp;
        registeredCompsScratchDirty = true;
    }

    public static void EnsureRegistered(CompFaceParts comp)
    {
        if (comp?.parent is not Pawn pawn)
            return;

        int pawnId = pawn.thingIDNumber;
        if (compByPawnId.TryGetValue(pawnId, out CompFaceParts existing) && ReferenceEquals(existing, comp))
            return;

        compByPawnId[pawnId] = comp;
        registeredCompsScratchDirty = true;
    }

    public static bool IsRegistered(CompFaceParts comp)
    {
        if (comp?.parent is not Pawn pawn)
            return false;

        return compByPawnId.TryGetValue(pawn.thingIDNumber, out CompFaceParts existing)
            && ReferenceEquals(existing, comp);
    }

    public static void UnregisterComp(CompFaceParts comp)
    {
        if (comp?.parent is not Pawn pawn)
            return;

        if (compByPawnId.TryGetValue(pawn.thingIDNumber, out CompFaceParts stored) && ReferenceEquals(stored, comp))
        {
            compByPawnId.Remove(pawn.thingIDNumber);
            registeredCompsScratchDirty = true;
        }
    }

    /// <summary>
    /// Returns a stable scratch list of all currently registered comps for GameComponent_FacePartsTick.
    /// Only call from the main game thread. Do not cache the returned reference across ticks.
    /// </summary>
    public static List<CompFaceParts> GetRegisteredCompsScratch()
    {
        if (registeredCompsScratchDirty)
        {
            registeredCompsScratch.Clear();
            foreach (CompFaceParts comp in compByPawnId.Values)
                registeredCompsScratch.Add(comp);
            registeredCompsScratchDirty = false;
        }

        return registeredCompsScratch;
    }

    public static void NotifyRuntimeRendered(Pawn pawn, int currentGameTick, int graceTicks)
    {
        SetUntilTick(recentlyRenderedUntilByPawnId, pawn, currentGameTick + graceTicks);
        SetMembership(activeRuntimePawnIds, pawn, true);
    }

    public static bool WasRuntimeRenderedRecently(Pawn pawn, int currentGameTick)
    {
        return IsActiveUntilTick(recentlyRenderedUntilByPawnId, pawn, currentGameTick);
    }

    public static void NotifyExplicitWake(Pawn pawn, int currentGameTick, int wakeTicks = 1)
    {
        int clampedWakeTicks = wakeTicks < 1 ? 1 : wakeTicks;
        SetUntilTick(explicitWakeUntilByPawnId, pawn, currentGameTick + clampedWakeTicks);
        SetMembership(highPriorityRuntimePawnIds, pawn, true);
        SetMembership(activeRuntimePawnIds, pawn, true);
    }

    public static bool HasExplicitWake(Pawn pawn, int currentGameTick)
    {
        return IsActiveUntilTick(explicitWakeUntilByPawnId, pawn, currentGameTick);
    }

    public static void NotifyEditorHeartbeat(Pawn pawn, int currentFrameCount, int graceFrames = 2)
    {
        int clampedGraceFrames = graceFrames < 1 ? 1 : graceFrames;
        SetUntilFrame(editorHeartbeatUntilByPawnIdFrame, pawn, currentFrameCount + clampedGraceFrames);
        SetMembership(highPriorityRuntimePawnIds, pawn, true);
        SetMembership(activeRuntimePawnIds, pawn, true);
    }

    public static bool HasEditorHeartbeat(Pawn pawn, int currentFrameCount)
    {
        return IsActiveUntilFrame(editorHeartbeatUntilByPawnIdFrame, pawn, currentFrameCount);
    }

    public static bool HasActiveExtendedAnimator(Pawn pawn)
    {
        return VisualActivityTracker.IsExtendedAnimatorActive(pawn);
    }

    public static void NotifyExtendedAnimatorState(Pawn pawn, bool isActive)
    {
        if (isActive)
        {
            SetMembership(highPriorityRuntimePawnIds, pawn, true);
            SetMembership(activeRuntimePawnIds, pawn, true);
            return;
        }

        RefreshMembership(pawn, CurrentGameTickSafe);
    }

    public static bool IsHighPriorityRuntimeRelevant(Pawn pawn, int currentGameTick)
    {
        RefreshMembership(pawn, currentGameTick);
        return Contains(highPriorityRuntimePawnIds, pawn);
    }

    public static bool IsRuntimeRelevant(Pawn pawn, int currentGameTick)
    {
        RefreshMembership(pawn, currentGameTick);
        return Contains(activeRuntimePawnIds, pawn);
    }

    public static bool ShouldMaintainRuntimeMicroDynamics(Pawn pawn, int currentGameTick, bool isSelected)
    {
        if (isSelected)
            return true;

        if (IsEditorActive(pawn) || HasActiveExtendedAnimator(pawn))
            return true;

        return WasRuntimeRenderedRecently(pawn, currentGameTick);
    }

    public static bool ShouldUseCrowdBlinkCadence(Pawn pawn, int currentGameTick, bool isSelected)
    {
        if (!ShouldMaintainRuntimeMicroDynamics(pawn, currentGameTick, isSelected))
            return false;

        if (isSelected)
            return false;

        return !IsEditorActive(pawn) && !HasActiveExtendedAnimator(pawn);
    }

    public static int RecommendCompTickOffset(Pawn pawn, int currentGameTick, bool isSelected, bool hasImmediateLocalWork, int visibleIntervalTicks, int idleIntervalTicks)
    {
        if (isSelected || hasImmediateLocalWork || IsHighPriorityRuntimeRelevant(pawn, currentGameTick))
            return 1;

        if (IsRuntimeRelevant(pawn, currentGameTick))
            return visibleIntervalTicks;

        return idleIntervalTicks;
    }

    public static bool ShouldConsumeRuntimeDynamics(Pawn pawn, int currentGameTick, bool runtimeDynamicsAllowed, bool isSelected)
    {
        if (!runtimeDynamicsAllowed)
            return false;

        if (isSelected)
            return true;

        return IsRuntimeRelevant(pawn, currentGameTick);
    }

    public static void SetEditorActive(Pawn pawn, bool isActive)
    {
        if (pawn == null)
            return;

        if (isActive)
        {
            editorActivePawnIds.Add(pawn.thingIDNumber);
            SetMembership(highPriorityRuntimePawnIds, pawn, true);
            SetMembership(activeRuntimePawnIds, pawn, true);
            return;
        }

        editorActivePawnIds.Remove(pawn.thingIDNumber);
        RefreshMembership(pawn, CurrentGameTickSafe);
    }

    public static bool IsEditorActive(Pawn pawn)
    {
        return pawn != null
            && (editorActivePawnIds.Contains(pawn.thingIDNumber)
                || HasEditorHeartbeat(pawn, Time.frameCount));
    }

    public static void ResetRuntimeState()
    {
        recentlyRenderedUntilByPawnId.Clear();
        explicitWakeUntilByPawnId.Clear();
        editorHeartbeatUntilByPawnIdFrame.Clear();
        editorActivePawnIds.Clear();
        highPriorityRuntimePawnIds.Clear();
        activeRuntimePawnIds.Clear();
        compByPawnId.Clear();
        registeredCompsScratch.Clear();
        registeredCompsScratchDirty = true;
        runtimeGeneration++;
    }

    public static void RehydrateRegisteredCompsFromCurrentMaps()
    {
        compByPawnId.Clear();
        registeredCompsScratch.Clear();
        registeredCompsScratchDirty = true;

        List<Map> maps = Find.Maps;
        for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
        {
            Map map = maps[mapIndex];
            if (map?.mapPawns?.AllPawnsSpawned == null)
                continue;

            List<Pawn> pawns = (List<Pawn>)map.mapPawns.AllPawnsSpawned;
            for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
            {
                Pawn pawn = pawns[pawnIndex];
                if (pawn?.RaceProps?.Humanlike != true)
                    continue;

                try
                {
                    pawn.TryGetComp<CompFaceParts>()?.RehydrateAfterRuntimeReset();
                }
                catch (System.Exception ex)
                {
                    Despicable.Core.DebugLogger.WarnExceptionOnce(
                        "FaceRuntimeActivityManager.Rehydrate",
                        "Face runtime rehydrate skipped one pawn after a non-fatal exception.",
                        ex);
                }
            }
        }
    }

    private static int CurrentGameTickSafe => Find.TickManager?.TicksGame ?? 0;

    private static void RefreshMembership(Pawn pawn, int currentGameTick)
    {
        if (pawn == null)
            return;

        bool highPriority = ComputeHighPriorityRuntimeRelevant(pawn, currentGameTick);
        bool active = highPriority || WasRuntimeRenderedRecently(pawn, currentGameTick);
        SetMembership(highPriorityRuntimePawnIds, pawn, highPriority);
        SetMembership(activeRuntimePawnIds, pawn, active);
    }

    private static bool ComputeHighPriorityRuntimeRelevant(Pawn pawn, int currentGameTick)
    {
        return IsEditorActive(pawn)
            || HasExplicitWake(pawn, currentGameTick)
            || HasActiveExtendedAnimator(pawn);
    }

    private static bool IsActiveUntilTick(Dictionary<int, int> untilByPawnId, Pawn pawn, int currentGameTick)
    {
        if (pawn == null)
            return false;

        int pawnId = pawn.thingIDNumber;
        if (!untilByPawnId.TryGetValue(pawnId, out int untilTick))
            return false;

        if (untilTick < currentGameTick)
        {
            untilByPawnId.Remove(pawnId);
            return false;
        }

        return true;
    }

    private static bool IsActiveUntilFrame(Dictionary<int, int> untilByPawnIdFrame, Pawn pawn, int currentFrameCount)
    {
        if (pawn == null)
            return false;

        int pawnId = pawn.thingIDNumber;
        if (!untilByPawnIdFrame.TryGetValue(pawnId, out int untilFrame))
            return false;

        if (untilFrame < currentFrameCount)
        {
            untilByPawnIdFrame.Remove(pawnId);
            return false;
        }

        return true;
    }

    private static bool Contains(HashSet<int> pawnIds, Pawn pawn)
    {
        return pawn != null && pawnIds.Contains(pawn.thingIDNumber);
    }

    private static void SetMembership(HashSet<int> pawnIds, Pawn pawn, bool isActive)
    {
        if (pawn == null)
            return;

        int pawnId = pawn.thingIDNumber;
        if (isActive)
            pawnIds.Add(pawnId);
        else
            pawnIds.Remove(pawnId);
    }

    private static void SetUntilFrame(Dictionary<int, int> untilByPawnIdFrame, Pawn pawn, int untilFrame)
    {
        if (pawn == null)
            return;

        int pawnId = pawn.thingIDNumber;
        if (untilByPawnIdFrame.TryGetValue(pawnId, out int existingUntilFrame) && existingUntilFrame >= untilFrame)
            return;

        untilByPawnIdFrame[pawnId] = untilFrame;
    }

    private static void SetUntilTick(Dictionary<int, int> untilByPawnId, Pawn pawn, int untilTick)
    {
        if (pawn == null)
            return;

        int pawnId = pawn.thingIDNumber;
        if (untilByPawnId.TryGetValue(pawnId, out int existingUntilTick) && existingUntilTick >= untilTick)
            return;

        untilByPawnId[pawnId] = untilTick;
    }
}
