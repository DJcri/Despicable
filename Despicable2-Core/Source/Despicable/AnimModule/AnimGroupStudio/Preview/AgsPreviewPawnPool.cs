using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace Despicable.AnimGroupStudio.Preview;
/// <summary>
/// Resolves preview pawns for AGS roles.
///
/// Strategy:
/// 1) Always generate clean, detached preview pawns owned by the studio.
/// 2) Never pull live colony/world pawns into preview casting.
///
/// Owned pawns created by this pool are cleaned up on Dispose.
/// </summary>
public sealed class AgsPreviewPawnPool : IDisposable
{
    private readonly Dictionary<string, Pawn> assigned = new();
    private readonly HashSet<Pawn> owned = new();

    public AgsPreviewPawnPool()
    {
    }

    public Pawn GetOrCreate(string key, int gender, string bodyTypeDefName)
    {
        if (key.NullOrEmpty()) key = "Role";

        // Reuse existing assignment if still valid.
        if (assigned.TryGetValue(key, out var cur) && cur != null && !cur.DestroyedOrNull())
        {
            if (Matches(cur, gender, bodyTypeDefName))
                return cur;
        }

        // Always use a detached generated preview pawn. Never source live pawns from the save.
        const bool registerAsWorldPawn = false;
        var gen = PreviewPawnFactory.MakeBaselinePreviewPawn(template: null, gender: gender, bodyTypeDefName: bodyTypeDefName, labelSuffix: key, registerAsWorldPawn: registerAsWorldPawn);
        if (gen != null)
        {
            owned.Add(gen);
            assigned[key] = gen;
        }
        return gen;
    }

    public void ClearUnused(HashSet<string> aliveKeys)
    {
        if (aliveKeys == null) aliveKeys = new HashSet<string>();

        var toRemove = assigned.Keys.Where(k => !aliveKeys.Contains(k)).ToList();
        for (int i = 0; i < toRemove.Count; i++)
        {
            var k = toRemove[i];
            if (assigned.TryGetValue(k, out var pawn) && pawn != null && owned.Contains(pawn))
            {
                SafeRemoveAndDestroy(pawn);
                owned.Remove(pawn);
            }
            assigned.Remove(k);
        }
    }

    public void Dispose()
    {
        // Only destroy pawns we created.
        foreach (var p in owned.ToList())
            SafeRemoveAndDestroy(p);

        assigned.Clear();
        owned.Clear();
    }

    private static bool Matches(Pawn pawn, int gender, string bodyTypeDefName)
    {
        if (pawn == null) return false;

        try
        {
            if (gender == 1 && pawn.gender != Gender.Male) return false;
            if (gender == 2 && pawn.gender != Gender.Female) return false;
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewPawnPool.EmptyCatch:1", "AGS preview pawn pool best-effort step failed.", e); }

        try
        {
            if (!bodyTypeDefName.NullOrEmpty() && pawn.story?.bodyType?.defName != bodyTypeDefName)
                return false;
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewPawnPool.EmptyCatch:2", "AGS preview pawn pool best-effort step failed.", e); }

        return true;
    }

    private void SafeRemoveAndDestroy(Pawn pawn)
    {
        if (pawn == null) return;

        try { if (pawn.Spawned) pawn.DeSpawn(); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewPawnPool.EmptyCatch:3", "AGS preview pawn pool best-effort step failed.", e); }
        try { pawn.Destroy(DestroyMode.Vanish); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewPawnPool.EmptyCatch:4", "AGS preview pawn pool best-effort step failed.", e); }
    }
}
