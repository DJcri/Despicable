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

        // If nothing is required, always allow.
        if (!requiresMale && !requiresFemale) return true;

        // Defensive checks.
        if (a.RaceProps == null || b.RaceProps == null) return false;
        if (!a.RaceProps.Humanlike || !b.RaceProps.Humanlike) return false;

        bool malePresent = false;
        bool femalePresent = false;

        if (IntegrationGuards.IsGenderWorksLoaded())
        {
            bool aMale = GenderWorks.GenderWorksUtil.HasMaleReproductiveOrganTag(a);
            bool bMale = GenderWorks.GenderWorksUtil.HasMaleReproductiveOrganTag(b);
            bool aFem = GenderWorks.GenderWorksUtil.HasFemaleReproductiveOrganTag(a);
            bool bFem = GenderWorks.GenderWorksUtil.HasFemaleReproductiveOrganTag(b);

            // If neither pawn exposes ANY repro tags, we don't know. Don't hard-block alien frameworks.
            if (aMale || bMale || aFem || bFem)
            {
                malePresent = aMale || bMale;
                femalePresent = aFem || bFem;
            }
            else
            {
                // Unknown: allow.
                return true;
            }
        }
        else if (a.def == ThingDefOf.Human && b.def == ThingDefOf.Human)
        {
            bool aMale = a.gender == Gender.Male;
            bool bMale = b.gender == Gender.Male;
            bool aFem = a.gender == Gender.Female;
            bool bFem = b.gender == Gender.Female;
            malePresent = aMale || bMale;
            femalePresent = aFem || bFem;
        }
        else
        {
            // Unknown anatomy system: don't block.
            return true;
        }

        if (requiresMale && !malePresent) return false;
        if (requiresFemale && !femalePresent) return false;
        return true;
    }

    /// <summary>
    /// Returns true if the pair appears capable of "vaginal" style intercourse.
    /// This is intentionally conservative:
    ///  - If GenderWorks is loaded, prefer its organ tags.
    ///  - Otherwise, fall back to vanilla gender heuristic for vanilla humans only.
    ///  - For unknown alien frameworks, return true (do not block) unless we are confident it is impossible.
    /// </summary>
    internal static bool CanDoVaginal(Pawn a, Pawn b)
    {
        if (a == null || b == null) return false;

        // We only use this for humanlike flesh pairs, but keep the check defensive.
        if (a.RaceProps == null || b.RaceProps == null) return false;
        if (!a.RaceProps.Humanlike || !b.RaceProps.Humanlike) return false;
        if (!a.RaceProps.IsFlesh || !b.RaceProps.IsFlesh) return false;

        // If GenderWorks is loaded, use its tags when present.
        if (IntegrationGuards.IsGenderWorksLoaded())
        {
            bool aMale = GenderWorks.GenderWorksUtil.HasMaleReproductiveOrganTag(a);
            bool bMale = GenderWorks.GenderWorksUtil.HasMaleReproductiveOrganTag(b);
            bool aFem = GenderWorks.GenderWorksUtil.HasFemaleReproductiveOrganTag(a);
            bool bFem = GenderWorks.GenderWorksUtil.HasFemaleReproductiveOrganTag(b);

            // If neither pawn exposes ANY repro tags, we don't know. Don't hard-block alien frameworks.
            if (!(aMale || bMale || aFem || bFem))
                return true;

            // Otherwise, require at least one "male" and at least one "female" signal across the pair.
            return (aMale || bMale) && (aFem || bFem);
        }

        // Without GenderWorks: keep it strict for vanilla humans only; do not guess for aliens.
        if (a.def == ThingDefOf.Human && b.def == ThingDefOf.Human)
        {
            bool aMale = a.gender == Gender.Male;
            bool bMale = b.gender == Gender.Male;
            bool aFem = a.gender == Gender.Female;
            bool bFem = b.gender == Gender.Female;
            return (aMale || bMale) && (aFem || bFem);
        }

        // Unknown anatomy system: don't block.
        return true;
    }
}
