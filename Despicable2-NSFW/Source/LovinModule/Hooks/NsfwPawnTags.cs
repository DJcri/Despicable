using System.Collections.Generic;
using RimWorld;
using Verse;
using Despicable.Core.Staging;

namespace Despicable;
/// <summary>
/// Baseline tag provider for NSFW staging. Tags are opaque strings consumed by slot requirements.
/// Repro tags are derived from logical anatomy first, then fall back to visible gender if anatomy is still unknown.
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

        if (pawn.gender == Gender.Male)
            into.Add("male");

        if (pawn.gender == Gender.Female)
            into.Add("female");

        bool anatomyKnown = AnatomyQuery.TryGetLogicalAnatomy(pawn, out bool hasPenis, out bool hasVagina);
        if (anatomyKnown)
        {
            if (hasPenis)
                into.Add("repro_male");

            if (hasVagina)
                into.Add("repro_female");

            return;
        }

        if (pawn.gender == Gender.Male)
            into.Add("repro_male");

        if (pawn.gender == Gender.Female)
            into.Add("repro_female");
    }
}
