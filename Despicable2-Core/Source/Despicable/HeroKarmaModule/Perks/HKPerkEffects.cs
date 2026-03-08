using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Perk effects runner (Step 4).
///
/// The UI already shows perks per tier via HKPerkCatalog, but until we apply
/// gameplay effects, perks are "paper props".
///
/// Design goal:
/// - Tier decides which perks are active (data-driven by HKPerkCatalog).
/// - Effects are implemented in one place and are safe/no-op when definitions are missing.
///
/// Recommended implementation path (when you flesh this out):
/// 1) Hediff-based passives (most compatible): add HediffDefs and apply/remove them here.
/// 2) Optional targeted Harmony hooks for "spicy" perks (goodwill bias, arrest success, etc).
/// </summary>
public static partial class HKPerkEffects
{
    // Cache to avoid re-applying every time.
    // Key: Pawn.GetUniqueLoadID() to survive save/load identity.
    private static readonly Dictionary<string, Cache> cacheByPawnId = new();

    private sealed class Cache
    {
        public HKTier tier;
        public List<string> perkIds = new();
    }

    public static void TrySyncHeroPerks(Pawn hero, int globalKarma)
    {
        if (hero == null) return;

        try
        {
            var pawnId = hero.GetUniqueLoadID();
            if (pawnId.NullOrEmpty()) return;

            var tier = HKRuntime.GetTierFor(globalKarma);
            var perkIds = HKPerkCatalog.GetPerksFor(tier)?
                .Select(p => p.id)
                .Where(id => !id.NullOrEmpty())
                .ToList()
                ?? new List<string>();

            if (!cacheByPawnId.TryGetValue(pawnId, out var cache))
            {
                cache = new Cache { tier = (HKTier)999 };
                cacheByPawnId[pawnId] = cache;
            }

            if (cache.tier == tier && SameList(cache.perkIds, perkIds))
                return;

            ApplyPerkSnapshot(hero, cache.perkIds, perkIds);

            cache.tier = tier;
            cache.perkIds = perkIds;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable] HKPerkEffects.TrySyncHeroPerks failed: {e}");
        }
    }

    /// <summary>
    /// Remove all known perk effects from the pawn and clear any cached tier/perk snapshot.
    /// Used when switching heroes or when a hero becomes invalid (dead/despawned).
    /// Safe no-op if defs are missing.
    /// </summary>
    public static void TryClearAllPerks(Pawn pawn)
    {
        if (pawn == null) return;

        try
        {
            RemoveAllKnownPerkHediffs(pawn);

            var id = pawn.GetUniqueLoadID();
            if (!id.NullOrEmpty())
                cacheByPawnId.Remove(id);
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable] HKPerkEffects.TryClearAllPerks failed: {e}");
        }
    }

    private static void ApplyPerkSnapshot(Pawn hero, List<string> previousPerkIds, List<string> nextPerkIds)
    {
        TryRemovePerks(hero, previousPerkIds);
        TryApplyPerks(hero, nextPerkIds);
    }

    public static void ResetRuntimeState()
    {
        cacheByPawnId.Clear();
    }

    private static bool SameList(List<string> a, List<string> b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null) return false;
        if (a.Count != b.Count) return false;

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }

        return true;
    }
}
