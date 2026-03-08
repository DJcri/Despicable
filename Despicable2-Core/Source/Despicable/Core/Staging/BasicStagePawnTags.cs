using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable.Core.Staging.Providers;
/// <summary>
/// Core-safe baseline pawn tags to support slot assignment.
/// Modules can register higher-priority providers to add richer semantics.
/// </summary>
public sealed class BasicStagePawnTags : IStagePawnTagProvider
{
    public int Priority => 0;

    public void AddTags(Pawn pawn, StageTagContext ctx, HashSet<string> into)
    {
        if (pawn == null) return;

        if (pawn.RaceProps != null)
        {
            if (pawn.RaceProps.Humanlike) into.Add("humanlike");
            else into.Add("nonhuman");
        }

        if (pawn.ageTracker != null && pawn.ageTracker.Adult) into.Add("adult");

        // Keep gender tags generic (used by legacy adapter and optional slot filters).
        if (pawn.gender == Gender.Male) into.Add("male");
        if (pawn.gender == Gender.Female) into.Add("female");

        if (!pawn.Downed && pawn.health?.capacities != null &&
            pawn.health.capacities.CapableOf(PawnCapacityDefOf.Moving))
            into.Add("can_move");
    }
}
