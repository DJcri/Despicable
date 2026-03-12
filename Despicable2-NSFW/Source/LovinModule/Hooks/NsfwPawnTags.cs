using System.Collections.Generic;
using RimWorld;
using Verse;
using Despicable.Core.Staging;

namespace Despicable;
/// <summary>
/// Baseline tag provider for NSFW staging. Tags are opaque strings consumed by slot requirements.
/// Repro tags are derived from seeded anatomy so staging can consume one stable truth source.
/// </summary>
public sealed class NsfwPawnTags : IStagePawnTagProvider
{
    public int Priority => 100;

    public void AddTags(Pawn pawn, StageTagContext ctx, HashSet<string> into)
    {
        if (pawn == null)
            return;

        if (pawn.RaceProps != null && pawn.RaceProps.Humanlike)
            into.Add("humanlike");

        if (pawn.ageTracker != null && pawn.ageTracker.Adult)
            into.Add("adult");

        if (!pawn.Downed
            && pawn.health?.capacities != null
            && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
        {
            into.Add("can_move");
        }

        // Visual gender tags remain available for appearance-oriented staging rules.
        if (pawn.gender == Gender.Male)
            into.Add("male");

        if (pawn.gender == Gender.Female)
            into.Add("female");

        // Repro tags now come from anatomy truth, not direct GW/gender probing.
        if (AnatomyQuery.HasPenis(pawn))
            into.Add("repro_male");

        if (AnatomyQuery.HasVagina(pawn))
            into.Add("repro_female");
    }
}
