using System.Collections.Generic;
using Verse;

namespace Despicable;

/// <summary>
/// Ticks only currently active extended animators so idle pawns do not pay a colony-wide comp-tick tax.
/// </summary>
public sealed class GameComponent_ExtendedAnimatorRuntime : GameComponent
{
    // Guardrail-Allow-Static: Active runtime singleton owned by the current GameComponent instance; reset naturally on new game/load via component recreation.
    private static GameComponent_ExtendedAnimatorRuntime current;
    private readonly HashSet<CompExtendedAnimator> activeAnimators = new();
    private readonly List<CompExtendedAnimator> activeScratch = new();
    private bool needsPostLoadRehydrate;

    public GameComponent_ExtendedAnimatorRuntime(Game game)
    {
        current = this;
    }

    internal static void RegisterActive(CompExtendedAnimator comp)
    {
        if (comp == null)
            return;

        Pawn pawn = comp.parent as Pawn;
        if (pawn?.Spawned != true || !comp.hasAnimPlaying)
            return;

        Current?.activeAnimators.Add(comp);
    }

    internal static void UnregisterActive(CompExtendedAnimator comp)
    {
        if (comp == null)
            return;

        Current?.activeAnimators.Remove(comp);
    }

    internal static void NotifyLoadedGame()
    {
        if (Current != null)
            Current.needsPostLoadRehydrate = true;
    }

    internal static void ResetRuntimeState()
    {
        if (Current == null)
            return;

        Current.activeAnimators.Clear();
        Current.activeScratch.Clear();
        Current.needsPostLoadRehydrate = false;
    }

    public override void GameComponentTick()
    {
        base.GameComponentTick();

        if (needsPostLoadRehydrate)
        {
            needsPostLoadRehydrate = false;
            RehydrateAfterRuntimeReset();
        }

        if (activeAnimators.Count == 0)
            return;

        activeScratch.Clear();
        activeScratch.AddRange(activeAnimators);
        for (int i = 0; i < activeScratch.Count; i++)
        {
            CompExtendedAnimator comp = activeScratch[i];
            if (comp == null)
                continue;

            Pawn pawn = comp.parent as Pawn;
            if (pawn?.Spawned != true || !comp.hasAnimPlaying)
            {
                activeAnimators.Remove(comp);
                continue;
            }

            comp.StepPlayback();

            if (!comp.hasAnimPlaying)
                activeAnimators.Remove(comp);
        }

        activeScratch.Clear();
    }

    private static GameComponent_ExtendedAnimatorRuntime Current
    {
        get
        {
            if (current != null)
                return current;

            current = Verse.Current.Game?.GetComponent<GameComponent_ExtendedAnimatorRuntime>();
            return current;
        }
    }

    private void RehydrateAfterRuntimeReset()
    {
        foreach (Map map in Find.Maps)
        {
            if (map?.mapPawns?.AllPawnsSpawned == null)
                continue;

            List<Pawn> pawns = (List<Pawn>)map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                try
                {
                    CompExtendedAnimator comp = pawn?.TryGetComp<CompExtendedAnimator>();
                    comp?.RehydrateAfterRuntimeReset();
                }
                catch (System.Exception ex)
                {
                    Despicable.Core.DebugLogger.WarnExceptionOnce("ExtendedAnimatorRuntime.Rehydrate", "Extended animator runtime rehydrate skipped one pawn after a non-fatal exception.", ex);
                }
            }
        }
    }
}
