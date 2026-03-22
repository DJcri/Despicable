using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
internal static class AnatomyAppearanceResolver
{
    internal static string ResolveTexturePath(Pawn pawn, AnatomyPartDef part, bool isAroused)
    {
        if (part == null)
            return null;

        AnatomyPartVariantDef variant = AnatomyQuery.GetInstalledVariant(pawn, part);
        if (TryResolveOverrideEntry(pawn, part, variant, out AnatomyAppearanceEntry overrideEntry) && overrideEntry != null)
        {
            ResolveEntryTexturePair(pawn, part, overrideEntry, out string overrideNeutral, out string overrideAroused);
            string resolvedOverride = ChooseExistingTexture(isAroused, overrideNeutral, overrideAroused);
            if (!resolvedOverride.NullOrEmpty())
                return resolvedOverride;
        }

        string heuristicTexture = AnatomyTextureHeuristicResolver.ResolveTexturePath(pawn, part, isAroused);
        if (!heuristicTexture.NullOrEmpty())
            return heuristicTexture;

        ResolveSizeVariantPair(pawn, part, out string sizeNeutral, out string sizeAroused);
        string resolvedSize = ChooseExistingTexture(isAroused, sizeNeutral, sizeAroused);
        if (!resolvedSize.NullOrEmpty())
            return resolvedSize;

        return ChooseExistingTexture(isAroused, part.properties?.texPath, part.texPathAroused);
    }

    private static void ResolveSizeVariantPair(Pawn pawn, AnatomyPartDef part, out string neutral, out string aroused)
    {
        neutral = null;
        aroused = null;

        if (pawn == null || part?.sizeTextureVariants == null || part.sizeTextureVariants.Count == 0)
            return;

        float size = AnatomyQuery.GetPartSize(pawn, part, part.baseSize);
        for (int i = 0; i < part.sizeTextureVariants.Count; i++)
        {
            AnatomySizeTextureVariant variant = part.sizeTextureVariants[i];
            if (variant == null || !variant.Matches(size))
                continue;

            neutral = variant.texPath;
            aroused = variant.texPathAroused;
            return;
        }
    }

    private static bool TryResolveOverrideEntry(Pawn pawn, AnatomyPartDef part, AnatomyPartVariantDef variant, out AnatomyAppearanceEntry entry)
    {
        entry = null;
        if (pawn == null || part == null)
            return false;

        ApplyGenericOverrides(pawn, part, variant, ref entry);
        return entry != null;
    }

    private static void ResolveEntryTexturePair(Pawn pawn, AnatomyPartDef part, AnatomyAppearanceEntry entry, out string neutral, out string aroused)
    {
        neutral = null;
        aroused = null;

        if (entry == null)
            return;

        if (entry.sizeTextureVariants != null && entry.sizeTextureVariants.Count > 0)
        {
            float size = AnatomyQuery.GetPartSize(pawn, part, part.baseSize);
            for (int i = 0; i < entry.sizeTextureVariants.Count; i++)
            {
                AnatomySizeTextureVariant variant = entry.sizeTextureVariants[i];
                if (variant == null || !variant.Matches(size))
                    continue;

                neutral = variant.texPath;
                aroused = variant.texPathAroused;
                return;
            }
        }

        neutral = entry.texPath;
        aroused = entry.texPathAroused;
    }

    private static void ApplyGenericOverrides(Pawn pawn, AnatomyPartDef part, AnatomyPartVariantDef variant, ref AnatomyAppearanceEntry resolvedEntry)
    {
        List<AnatomyAppearanceOverrideDef> defs = DefDatabase<AnatomyAppearanceOverrideDef>.AllDefsListForReading;
        if (defs == null)
            return;

        AnatomyAppearanceEntry bestEntry = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < defs.Count; i++)
        {
            AnatomyAppearanceOverrideDef def = defs[i];
            if (!MatchesAppearance(pawn, variant, def))
                continue;

            List<AnatomyAppearanceEntry> entries = def.parts;
            if (entries == null)
                continue;

            for (int j = 0; j < entries.Count; j++)
            {
                AnatomyAppearanceEntry entry = entries[j];
                if (entry?.part != part)
                    continue;

                int score = (def.priority * 100) + GetSpecificityScore(def.variantDefs, def.geneDefs, def.raceDefs, def.pawnKindDefs, def.bodyTypes, def.genders, def.lifeStages);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestEntry = entry;
            }
        }

        if (bestEntry != null)
            resolvedEntry = bestEntry;
    }

    private static bool MatchesAppearance(Pawn pawn, AnatomyPartVariantDef variant, AnatomyAppearanceOverrideDef def)
    {
        if (pawn == null || def == null)
            return false;

        return MatchesVariant(def.variantDefs, variant)
            && AnatomyResolver.MatchesGenes(def.geneDefs, pawn)
            && AnatomyResolver.MatchesThingDef(def.raceDefs, pawn.def)
            && AnatomyResolver.MatchesPawnKind(def.pawnKindDefs, pawn.kindDef)
            && AnatomyResolver.MatchesBodyType(def.bodyTypes, pawn.story?.bodyType)
            && AnatomyResolver.MatchesGender(def.genders, pawn.gender)
            && AnatomyResolver.MatchesLifeStage(def.lifeStages, pawn.ageTracker?.CurLifeStage);
    }

    private static bool MatchesVariant(List<AnatomyPartVariantDef> targets, AnatomyPartVariantDef current)
    {
        if (targets == null || targets.Count == 0)
            return true;

        if (current == null)
            return false;

        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i] == current)
                return true;
        }

        return false;
    }

    private static int GetSpecificityScore(List<AnatomyPartVariantDef> variantDefs, List<GeneDef> geneDefs, List<ThingDef> raceDefs, List<PawnKindDef> pawnKindDefs, List<BodyTypeDef> bodyTypes, List<Gender> genders, List<LifeStageDef> lifeStages)
    {
        int score = 0;
        if (variantDefs != null && variantDefs.Count > 0)
            score += 128;
        if (geneDefs != null && geneDefs.Count > 0)
            score += 64;
        if (pawnKindDefs != null && pawnKindDefs.Count > 0)
            score += 32;
        if (raceDefs != null && raceDefs.Count > 0)
            score += 16;
        if (lifeStages != null && lifeStages.Count > 0)
            score += 8;
        if (bodyTypes != null && bodyTypes.Count > 0)
            score += 4;
        if (genders != null && genders.Count > 0)
            score += 2;
        return score;
    }

    private static string ChooseExistingTexture(bool isAroused, string neutral, string aroused)
    {
        string preferred = isAroused ? aroused : neutral;
        string fallback = isAroused ? neutral : aroused;

        if (AnatomyTextureHeuristicResolver.HasTexturePath(preferred))
            return preferred;

        if (AnatomyTextureHeuristicResolver.HasTexturePath(fallback))
            return fallback;

        return null;
    }
}
