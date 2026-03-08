using System.Collections.Generic;
using RimWorld;
using Verse;
using Despicable.Core.Staging;
using Despicable.NSFW.Integrations;

namespace Despicable;
/// <summary>
/// Baseline tag provider for NSFW staging. Tags are opaque strings consumed by slot requirements.
/// This implementation is intentionally conservative: anatomy-specific tags can be added later.
/// </summary>
public sealed class NsfwPawnTags : IStagePawnTagProvider
{
    public int Priority => 100;

    public void AddTags(Pawn pawn, StageTagContext ctx, HashSet<string> into)
    {
        if (pawn == null) return;

        if (pawn.RaceProps != null && pawn.RaceProps.Humanlike)
            into.Add("humanlike");

        if (pawn.ageTracker != null && pawn.ageTracker.Adult)
            into.Add("adult");

        if (!pawn.Downed && pawn.health?.capacities != null &&
            pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
            into.Add("can_move");

        // Visual gender tags remain available for non-NSFW / appearance-oriented staging rules.
        if (pawn.gender == Gender.Male) into.Add("male");
        if (pawn.gender == Gender.Female) into.Add("female");

        // Repro tags are used by NSFW slot assignment so role matching can follow anatomy
        // instead of visible gender when GenderWorks is present.
        bool addedReproSignal = false;
        if (IntegrationGuards.IsGenderWorksLoaded())
        {
            if (Despicable.NSFW.Integrations.GenderWorks.GenderWorksUtil.HasMaleReproductiveOrganTag(pawn))
            {
                into.Add("repro_male");
                addedReproSignal = true;
            }

            if (Despicable.NSFW.Integrations.GenderWorks.GenderWorksUtil.HasFemaleReproductiveOrganTag(pawn))
            {
                into.Add("repro_female");
                addedReproSignal = true;
            }
        }

        // Vanilla / unknown fallback: preserve legacy behavior when there is no anatomy signal.
        if (!addedReproSignal)
        {
            if (pawn.gender == Gender.Male) into.Add("repro_male");
            if (pawn.gender == Gender.Female) into.Add("repro_female");
        }
    }
}
