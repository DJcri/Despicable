using RimWorld;
using Verse;
using Despicable;

namespace Despicable.NSFW.Integrations;
/// <summary>
/// Best-effort anatomy checks used ONLY to choose animations more sensibly.
/// This must never throw and must be tolerant of alien frameworks.
///
/// IMPORTANT: These checks are NOT used to change Intimacy's pregnancy math.
/// They only influence what animation we try to play.
/// </summary>
internal static class ReproCompatibilityUtil
{
    internal static bool PairSatisfiesLovinTypeRequirements(Pawn a, Pawn b, LovinTypeDef lovinType)
    {
        if (lovinType == null) return true;
        return PairSatisfiesSexRequirements(a, b, lovinType.requiresMale, lovinType.requiresFemale);
    }

    internal static bool PairSatisfiesSexRequirements(Pawn a, Pawn b, bool requiresMale, bool requiresFemale)
    {
        if (a == null || b == null) return false;

        if (!requiresMale && !requiresFemale) return true;

        if (a.RaceProps == null || b.RaceProps == null) return false;
        if (!a.RaceProps.Humanlike || !b.RaceProps.Humanlike) return false;

        bool aKnown = AnatomyQuery.TryGetLogicalAnatomy(a, out bool aHasPenis, out bool aHasVagina);
        bool bKnown = AnatomyQuery.TryGetLogicalAnatomy(b, out bool bHasPenis, out bool bHasVagina);

        if (!aKnown || !bKnown)
            return true;

        bool malePresent = aHasPenis || bHasPenis;
        bool femalePresent = aHasVagina || bHasVagina;

        if (requiresMale && !malePresent) return false;
        if (requiresFemale && !femalePresent) return false;
        return true;
    }

    internal static bool CanDoVaginal(Pawn a, Pawn b)
    {
        if (a == null || b == null) return false;

        if (a.RaceProps == null || b.RaceProps == null) return false;
        if (!a.RaceProps.Humanlike || !b.RaceProps.Humanlike) return false;
        if (!a.RaceProps.IsFlesh || !b.RaceProps.IsFlesh) return false;

        bool aKnown = AnatomyQuery.TryGetLogicalAnatomy(a, out bool aHasPenis, out bool aHasVagina);
        bool bKnown = AnatomyQuery.TryGetLogicalAnatomy(b, out bool bHasPenis, out bool bHasVagina);

        if (!aKnown || !bKnown)
            return true;

        bool malePresent = aHasPenis || bHasPenis;
        bool femalePresent = aHasVagina || bHasVagina;

        return malePresent && femalePresent;
    }

    internal static bool PawnSatisfiesSoloLovinTypeRequirements(Pawn pawn, LovinTypeDef lovinType)
    {
        if (lovinType == null) return true;
        return PawnSatisfiesSexRequirements(pawn, lovinType.requiresMale, lovinType.requiresFemale);
    }

    internal static bool PawnSatisfiesSexRequirements(Pawn pawn, bool requiresMale, bool requiresFemale)
    {
        if (pawn == null) return false;
        if (!requiresMale && !requiresFemale) return true;

        if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike) return false;

        bool known = AnatomyQuery.TryGetLogicalAnatomy(pawn, out bool hasPenis, out bool hasVagina);
        if (!known) return true;

        if (requiresMale && !hasPenis) return false;
        if (requiresFemale && !hasVagina) return false;
        return true;
    }
}
