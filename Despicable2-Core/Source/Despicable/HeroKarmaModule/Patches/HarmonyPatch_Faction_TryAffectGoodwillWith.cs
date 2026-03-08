using HarmonyLib;
using RimWorld;
using System;
using System.Reflection;

using UnityEngine;

using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;
/// <summary>
/// Makes the "Goodwill Tailwind" / "Goodwill Friction" perks affect faction goodwill changes directly,
/// rather than only indirectly through negotiation/trade stats.
///
/// Rule: only touches goodwill changes involving the player faction.
/// Safe: if defs are missing or hero is unset, this no-ops.
/// </summary>
[HarmonyPatch(typeof(Faction), nameof(Faction.TryAffectGoodwillWith))]
internal static partial class HarmonyPatch_Faction_TryAffectGoodwillWith
{
    private const string PatchId = "HKPatch.Faction.TryAffectGoodwillWith";
    // Guardrail-Allow-Static: Cached Harmony target for this patch class; resolved during Prepare and reused for version-safe patch diagnostics during load.
    private static MethodBase _guardTarget;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        MethodBase m = AccessTools.Method(typeof(Faction), nameof(Faction.TryAffectGoodwillWith));
        return HKPatchGuard.PrepareSingle(
            PatchId,
            "Faction goodwill modifier (Faction.TryAffectGoodwillWith)",
            featureKey: HKPatchGuard.FeatureLocalRepGoodwill,
            required: false,
            target: m,
            cached: out _guardTarget);
    }

    private const string HediffTailwind = "HK_Hediff_GoodwillTailwind";
    private const string HediffFriction = "HK_Hediff_GoodwillFriction";

    // Cache defs lazily (DefDatabase is safe once defs are loaded).
    private static readonly HediffLookupCache DefCache = new();

    /// <summary>
    /// Adjust goodwillChange before the game clamps/applies it.
    /// </summary>
    private static void Prefix(Faction __instance, Faction other, ref int goodwillChange)
    {
        try
        {
            if (goodwillChange == 0)
            {
                return;
            }

            if (__instance == null || other == null)
            {
                return;
            }

            // Only affect relations involving the player.
            if (!__instance.IsPlayer && !other.IsPlayer)
            {
                return;
            }

            var hero = HKRuntime.GetHeroPawnSafe();
            if (hero == null)
            {
                return;
            }

            // Only apply when this goodwill change is caused by a hero-initiated action.
            // If there is no active context, treat it as colony/system driven and leave it alone.
            var instigator = HKGoodwillContext.TryGetInstigator();
            if (instigator == null || instigator != hero)
            {
                return;
            }

            // Local reputation: bias goodwill changes based on how this faction remembers the Hero.
            if (HKSettingsUtil.EnableLocalRep && HKSettingsUtil.LocalRepGoodwillBias)
            {
                try
                {
                    Faction targetFaction = __instance.IsPlayer ? other : __instance;
                    if (targetFaction != null && !targetFaction.IsPlayer)
                    {
                        string heroId = hero.GetUniqueLoadID();
                        if (LocalReputationUtility.TryGetFactionInfluenceIndex(heroId, targetFaction.loadID, out float r))
                        {
                            float mult;
                            if (goodwillChange > 0)
                                mult = Mathf.Clamp(1f + (LocalRepTuning.GoodwillStrength * r), LocalRepTuning.GoodwillClampMin, LocalRepTuning.GoodwillClampMax);
                            else
                                mult = Mathf.Clamp(1f - (LocalRepTuning.GoodwillStrength * r), LocalRepTuning.GoodwillClampMin, LocalRepTuning.GoodwillClampMax);

                            int adjusted = Mathf.RoundToInt(goodwillChange * mult);

                            // Keep tiny changes from disappearing.
                            if (adjusted == 0) adjusted = goodwillChange > 0 ? 1 : -1;

                            goodwillChange = adjusted;
                        }
                    }
                }
                catch { /* never break goodwill */ }
            }

            var hs = hero.health?.hediffSet;
            if (hs == null)
            {
                return;
            }

            EnsureDefs();
            if (!TryGetPerkFlags(hs, out var hasTailwind, out var hasFriction))
            {
                return;
            }

            if (goodwillChange > 0)
            {
                goodwillChange = ApplyPositivePerkModifiers(goodwillChange, hasTailwind, hasFriction);
            }
            else
            {
                goodwillChange = ApplyNegativePerkModifiers(goodwillChange, hasTailwind, hasFriction);
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_Faction_TryAffectGoodwillWith:101",
                "HarmonyPatch_Faction_TryAffectGoodwillWith suppressed an exception.",
                ex);
            // Never break goodwill logic.
        }
    }

    private sealed class HediffLookupCache
    {
        public HediffDef TailwindDef;
        public HediffDef FrictionDef;
        public bool DefsTried;
    }
}
