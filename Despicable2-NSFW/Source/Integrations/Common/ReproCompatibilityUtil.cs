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
    /// <summary>
    /// Returns true if the pair appears to satisfy the sex requirements of a LovinTypeDef.
    /// This is best-effort and should never hard-block unknown alien anatomy systems.
    /// </summary>
    internal static bool PairSatisfiesLovinTypeRequirements(Pawn a, Pawn b, LovinTypeDef lovinType)
    {
        if (lovinType == null) return true;
        return PairSatisfiesSexRequirements(a, b, lovinType.requiresMale, lovinType.requiresFemale);
    }

    /// <summary>
    /// Best-effort check for whether the pair has at least one "male" and/or "female" reproductive signal.
    /// </summary>
    internal static bool PairSatisfiesSexRequirements(Pawn a, Pawn b, bool requiresMale, bool requiresFemale)
    {
        if (a == null || b == null) return false;

        if (!requiresMale && !requiresFemale) return true;

        if (a.RaceProps == null || b.RaceProps == null) return false;
        if (!a.RaceProps.Humanlike || !b.RaceProps.Humanlike) return false;

        bool aHasSlot = AnatomyQuery.HasExternalGenitalsSlot(a);
        bool bHasSlot = AnatomyQuery.HasExternalGenitalsSlot(b);

        // Preserve tolerant behavior for unknown frameworks / unpatched bodies.
        if (!aHasSlot || !bHasSlot)
            return true;

        bool malePresent = AnatomyQuery.HasPenis(a) || AnatomyQuery.HasPenis(b);
        bool femalePresent = AnatomyQuery.HasVagina(a) || AnatomyQuery.HasVagina(b);

        if (requiresMale && !malePresent) return false;
        if (requiresFemale && !femalePresent) return false;
        return true;
    }

    /// <summary>
    /// Returns true if the pair appears capable of "vaginal" style intercourse.
    /// This is intentionally conservative and remains permissive for unknown alien frameworks.
    /// </summary>
    internal static bool CanDoVaginal(Pawn a, Pawn b)
    {
        if (a == null || b == null) return false;

        if (a.RaceProps == null || b.RaceProps == null) return false;
        if (!a.RaceProps.Humanlike || !b.RaceProps.Humanlike) return false;
        if (!a.RaceProps.IsFlesh || !b.RaceProps.IsFlesh) return false;

        bool aHasSlot = AnatomyQuery.HasExternalGenitalsSlot(a);
        bool bHasSlot = AnatomyQuery.HasExternalGenitalsSlot(b);

        // Preserve the old permissive philosophy for unknown systems.
        if (!aHasSlot || !bHasSlot)
            return true;

        bool malePresent = AnatomyQuery.HasPenis(a) || AnatomyQuery.HasPenis(b);
        bool femalePresent = AnatomyQuery.HasVagina(a) || AnatomyQuery.HasVagina(b);

        return malePresent && femalePresent;
    }

    /// <summary>
    /// Returns true if a single pawn appears to satisfy the sex requirements of a solo LovinTypeDef.
    /// This mirrors the pair helper's best-effort philosophy and avoids hard-blocking unknown alien frameworks.
    /// </summary>
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
        if (!AnatomyQuery.HasExternalGenitalsSlot(pawn)) return true;

        bool malePresent = AnatomyQuery.HasPenis(pawn);
        bool femalePresent = AnatomyQuery.HasVagina(pawn);

        if (requiresMale && !malePresent) return false;
        if (requiresFemale && !femalePresent) return false;
        return true;
    }
}
