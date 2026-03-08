using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Despicable.NSFW.Integrations.Intimacy;
internal static partial class HarmonyPatch_Intimacy_SexToilAnimation
{
    /// <summary>
    /// Picks a LovinTypeDef.defName (stage tag) for visual alignment.
    /// - If we can read Intimacy pregnancy chance, we set P(Vaginal)=chance.
    /// - If vaginal seems anatomically impossible (best-effort), we avoid selecting it.
    /// - Otherwise, we keep the selection broad and low-maintenance.
    /// </summary>
    private static string ChooseLovinTypeForVisualAlignment(Pawn pawn, Pawn partner)
    {
        // We only pick for valid pairs. Otherwise, let Despicable default selection handle it.
        if (!IntegrationGuards.IsHumanoidFleshPairForAnimation(pawn, partner))
            return null;

        float pregChance;
        bool hasChance = IntimacyReflectionUtil.TryGetPregnancyChance(pawn, partner, out pregChance);

        // Pull all known LovinTypeDefs so adding new types is automatically supported.
        // We still do best-effort compatibility filtering so we don't pick clearly impossible types.
        var allTypes = DefDatabase<LovinTypeDef>.AllDefsListForReading;
        if (allTypes == null || allTypes.Count == 0)
            return null;

        // Stable ordering so selection doesn't jitter across loads.
        var ordered = allTypes.Where(d => d != null && !d.defName.NullOrEmpty())
                              .OrderBy(d => d.defName)
                              .ToList();

        // Filter by declared requirements (male/female) when we can infer them.
        var candidates = ordered.Where(d => ReproCompatibilityUtil.PairSatisfiesLovinTypeRequirements(pawn, partner, d)).ToList();
        if (candidates.Count == 0)
            return null;

        // Identify "Vaginal" def (by defName). If absent, we just do a simple uniform pick.
        var vaginalDef = candidates.FirstOrDefault(d => d.defName == "Vaginal");

        // If we have a vaginal option, additionally require best-effort vaginal compatibility.
        bool canVaginal = vaginalDef != null && ReproCompatibilityUtil.CanDoVaginal(pawn, partner);

        // Split non-vaginal pool.
        var nonVaginal = candidates.Where(d => vaginalDef == null || d != vaginalDef).ToList();

        // If we cannot read pregnancy chance OR there is no vaginal def, keep selection simple.
        if (!hasChance || vaginalDef == null)
        {
            // If vaginal exists but is not feasible, exclude it.
            var pool = candidates;
            if (vaginalDef != null && !canVaginal)
                pool = candidates.Where(d => d != vaginalDef).ToList();

            if (pool.Count == 0)
                return null;

            return pool.RandomElement().defName;
        }

        // If vaginal isn't feasible, never pick it.
        if (!canVaginal)
        {
            if (nonVaginal.Count == 0) return null;
            return nonVaginal.RandomElement().defName;
        }

        // Probability matching: P(Vaginal) = Intimacy pregnancy chance.
        // This aligns vaginal animation frequency with fertility, without modifying Intimacy's pregnancy roll.
        float pVaginal = Mathf.Clamp01(pregChance);
        if (Rand.Value < pVaginal)
            return vaginalDef.defName;

        if (nonVaginal.Count == 0)
            return vaginalDef.defName;

        return nonVaginal.RandomElement().defName;
    }

    private static int FindActToilIndex(List<Toil> toils)
    {
        // Heuristic, but tighter than before:
        // Prefer the *longest* timed toil that ticks, since the actual act usually
        // occupies the longest duration (approach/wait toils are typically short).
        // If we can't confidently pick one, return -1 (no patch).

        int bestIdx = -1;
        int bestDur = -1;

        for (int i = 0; i < toils.Count; i++)
        {
            var t = toils[i];
            if (t == null) continue;

            if (t.tickAction == null) continue;
            if (t.defaultDuration <= 0) continue;

            if (t.defaultDuration > bestDur)
            {
                bestDur = t.defaultDuration;
                bestIdx = i;
            }
        }

        return bestIdx;
    }

}
