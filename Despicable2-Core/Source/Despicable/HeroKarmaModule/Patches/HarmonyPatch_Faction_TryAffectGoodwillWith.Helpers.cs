using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;
internal static partial class HarmonyPatch_Faction_TryAffectGoodwillWith
{
    private static void EnsureDefs()
    {
        if (DefCache.DefsTried)
        {
            return;
        }

        DefCache.DefsTried = true;
        DefCache.TailwindDef = DefDatabase<HediffDef>.GetNamedSilentFail(HediffTailwind);
        DefCache.FrictionDef = DefDatabase<HediffDef>.GetNamedSilentFail(HediffFriction);
    }

    private static bool TryGetPerkFlags(HediffSet hediffSet, out bool hasTailwind, out bool hasFriction)
    {
        // If defs are missing, we do nothing (keeps load order and optional content safe).
        hasTailwind = DefCache.TailwindDef != null && hediffSet.HasHediff(DefCache.TailwindDef);
        hasFriction = DefCache.FrictionDef != null && hediffSet.HasHediff(DefCache.FrictionDef);
        return hasTailwind || hasFriction;
    }

    private static int ApplyPositivePerkModifiers(int goodwillChange, bool hasTailwind, bool hasFriction)
    {
        // Conservative tuning: we bias changes, but we do not rewrite them.
        if (hasTailwind)
        {
            int bonus = Mathf.Max(1, Mathf.RoundToInt(goodwillChange * HKBalanceTuning.PerkBehavior.GoodwillTailwindPositiveBonus));
            goodwillChange += bonus;
        }

        if (hasFriction)
        {
            goodwillChange = Mathf.Max(1, Mathf.RoundToInt(goodwillChange * HKBalanceTuning.PerkBehavior.GoodwillFrictionPositiveMultiplier));
        }

        return goodwillChange;
    }

    private static int ApplyNegativePerkModifiers(int goodwillChange, bool hasTailwind, bool hasFriction)
    {
        int loss = -goodwillChange;

        if (hasFriction)
        {
            int penalty = Mathf.Max(1, Mathf.RoundToInt(loss * HKBalanceTuning.PerkBehavior.GoodwillFrictionNegativeLossBonus));
            loss += penalty;
        }

        if (hasTailwind)
        {
            loss = Mathf.Max(1, Mathf.RoundToInt(loss * HKBalanceTuning.PerkBehavior.GoodwillTailwindNegativeLossMultiplier));
        }

        return -loss;
    }
}
