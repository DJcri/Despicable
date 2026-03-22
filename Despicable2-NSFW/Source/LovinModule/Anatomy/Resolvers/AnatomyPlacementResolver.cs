using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
internal static class AnatomyPlacementResolver
{
    internal static Vector3 ResolveOffset(Pawn pawn, AnatomyPartDef part, Rot4 facing)
    {
        Vector3 offset = ResolveModernOffset(pawn, part);
        return ApplyFacing(offset, facing);
    }

    private static Vector3 ResolveModernOffset(Pawn pawn, AnatomyPartDef part)
    {
        if (pawn == null || part == null)
            return Vector3.zero;

        List<AnatomyPlacementDef> defs = DefDatabase<AnatomyPlacementDef>.AllDefsListForReading;
        if (defs == null || defs.Count == 0)
            return Vector3.zero;

        AnatomyPlacementDef bestDef = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < defs.Count; i++)
        {
            AnatomyPlacementDef def = defs[i];
            if (!MatchesPlacement(pawn, part, def))
                continue;

            int score = def.priority * 100 + GetSpecificityScore(def);
            if (score > bestScore)
            {
                bestScore = score;
                bestDef = def;
            }
        }

        return bestDef?.offset ?? Vector3.zero;
    }

    private static bool MatchesPlacement(Pawn pawn, AnatomyPartDef part, AnatomyPlacementDef def)
    {
        if (pawn == null || part == null || def == null)
            return false;

        if (def.part != null && def.part != part)
            return false;

        return AnatomyResolver.MatchesThingDef(def.raceDefs, pawn.def)
            && AnatomyResolver.MatchesPawnKind(def.pawnKindDefs, pawn.kindDef)
            && AnatomyResolver.MatchesBodyType(def.bodyTypes, pawn.story?.bodyType)
            && AnatomyResolver.MatchesGender(def.genders, pawn.gender)
            && AnatomyResolver.MatchesLifeStage(def.lifeStages, pawn.ageTracker?.CurLifeStage);
    }

    private static int GetSpecificityScore(AnatomyPlacementDef def)
    {
        int score = 0;
        if (def.part != null)
            score += 64;
        if (def.pawnKindDefs != null && def.pawnKindDefs.Count > 0)
            score += 32;
        if (def.raceDefs != null && def.raceDefs.Count > 0)
            score += 16;
        if (def.lifeStages != null && def.lifeStages.Count > 0)
            score += 8;
        if (def.bodyTypes != null && def.bodyTypes.Count > 0)
            score += 4;
        if (def.genders != null && def.genders.Count > 0)
            score += 2;
        return score;
    }

    private static Vector3 ApplyFacing(Vector3 source, Rot4 facing)
    {
        Vector3 result = Vector3.zero;
        if (facing == Rot4.East)
            result += Vector3.right * source.x;
        else if (facing == Rot4.West)
            result += Vector3.left * source.x;

        result += Vector3.up * source.y;
        result += Vector3.forward * source.z;
        return result;
    }
}
