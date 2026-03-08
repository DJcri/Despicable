using System.Collections.Generic;

namespace Despicable.HeroKarma;
/// <summary>
/// Backend-locked perk catalog (Step 3 UI wiring).
///
/// In the rebuilt system, these should be sourced from your real perk catalog backend.
/// This file exists to keep the UI fully data-driven and stable even before gameplay
/// effects are implemented.
/// </summary>
public static partial class HKPerkCatalog
{
    public static bool TryGetPerk(string id, out HKPerkDef def)
    {
        if (string.IsNullOrEmpty(id))
        {
            def = null;
            return false;
        }

        return perks.TryGetValue(id, out def);
    }

    public static bool TryGetTierForPerk(string perkId, out HKTier tier)
    {
        if (string.IsNullOrEmpty(perkId))
        {
            tier = default;
            return false;
        }

        foreach (var kv in tierToPerkIds)
        {
            List<string> ids = kv.Value;
            if (ids != null && ids.Contains(perkId))
            {
                tier = kv.Key;
                return true;
            }
        }

        tier = default;
        return false;
    }

    public static IEnumerable<HKPerkDef> GetPerksFor(HKTier tier)
    {
        List<string> list;
        if (!tierToPerkIds.TryGetValue(tier, out list) || list == null)
            yield break;

        foreach (string id in list)
        {
            HKPerkDef def;
            if (perks.TryGetValue(id, out def))
                yield return def;
        }
    }

    public static IEnumerable<HKTier> AllTiersInOrder()
    {
        foreach (HKTier tier in orderedTiers)
            yield return tier;
    }

    public static string TierRangeLabel(HKTier tier)
    {
        return HKBalanceTuning.GetTierRangeLabel(tier);
    }
}
