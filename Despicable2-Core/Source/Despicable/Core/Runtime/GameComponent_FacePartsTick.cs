using System.Collections.Generic;
using Verse;

namespace Despicable;

/// <summary>
/// Replaces the per-pawn CompTick dispatch for FaceParts.
/// On each game tick, iterates only the registered humanlike pawns rather than
/// receiving a virtual CompTick call for every pawn in the colony.
/// Idle pawns (nextScheduledCompTick far in the future) exit RunFacePartsTick
/// in O(1) via ShouldSkipScheduledCompTick, so the cost per dormant pawn is
/// a single dictionary value lookup + one integer compare.
/// </summary>
public sealed class GameComponent_FacePartsTick : GameComponent
{
    private static GameComponent_FacePartsTick current;
    private bool needsPostLoadRehydrate;

    public GameComponent_FacePartsTick(Game game)
    {
        current = this;
    }

    internal static void NotifyRuntimeReset()
    {
        if (Current != null)
            Current.needsPostLoadRehydrate = true;
    }

    public override void GameComponentTick()
    {
        base.GameComponentTick();

        if (ModMain.IsNlFacialInstalled)
            return;

        if (needsPostLoadRehydrate)
        {
            needsPostLoadRehydrate = false;
            FaceRuntimeActivityManager.RehydrateRegisteredCompsFromCurrentMaps();
        }

        int currentGameTick = Find.TickManager.TicksGame;

        // GetRegisteredCompsScratch returns a cached List<CompFaceParts> that is
        // rebuilt only when the registry changes (pawn spawn/despawn). Iterating
        // a List<T> by index avoids enumerator allocation on every tick.
        List<CompFaceParts> comps = FaceRuntimeActivityManager.GetRegisteredCompsScratch();
        for (int i = 0; i < comps.Count; i++)
        {
            CompFaceParts comp = comps[i];
            if (comp?.parent?.Spawned == true)
                comp.RunFacePartsTick(currentGameTick);
        }
    }

    private static GameComponent_FacePartsTick Current
    {
        get
        {
            if (current != null)
                return current;

            current = Verse.Current.Game?.GetComponent<GameComponent_FacePartsTick>();
            return current;
        }
    }
}
