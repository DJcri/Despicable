using System;
using RimWorld;
using Verse;
using Verse.AI;

namespace Despicable;

/// <summary>
/// Picks a nearby rendezvous cell for no-bed lovin that feels private enough
/// without becoming so strict that jobs fail constantly on cramped maps.
/// </summary>
public static class LovinPrivacyCellFinder
{
    private const float LocalSearchRadius = 16f;
    private const float ExpandedSearchRadius = 24f;
    private const float MaxDistanceFromEitherPawn = 24f;
    private const float NearbyWitnessRadius = 8.9f;
    private const float VisibleWitnessRadius = 12.9f;
    private const float MinAcceptableScore = 1.5f;

    public static bool TryFindPrivacyCell(Pawn initiator, Pawn partner, out IntVec3 result)
    {
        result = IntVec3.Invalid;

        if (initiator == null || partner == null)
            return false;

        if (!initiator.Spawned || !partner.Spawned)
            return false;

        if (initiator.Map == null || initiator.Map != partner.Map)
            return false;

        Map map = initiator.Map;
        IntVec3 center = new IntVec3(
            (initiator.Position.x + partner.Position.x) / 2,
            0,
            (initiator.Position.z + partner.Position.z) / 2);

        float bestScore;
        if (TryFindBestCellWithinRadius(center, LocalSearchRadius, initiator, partner, map, out result, out bestScore) &&
            bestScore >= MinAcceptableScore)
        {
            return true;
        }

        if (TryFindBestCellWithinRadius(center, ExpandedSearchRadius, initiator, partner, map, out result, out bestScore) &&
            bestScore >= MinAcceptableScore)
        {
            return true;
        }

        result = IntVec3.Invalid;
        return false;
    }

    private static bool TryFindBestCellWithinRadius(IntVec3 center, float radius, Pawn initiator, Pawn partner, Map map, out IntVec3 result, out float bestScore)
    {
        result = IntVec3.Invalid;
        bestScore = float.MinValue;
        bool found = false;

        foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, useCenter: true))
        {
            if (!cell.InBounds(map))
                continue;

            if (!IsCandidateValid(cell, initiator, partner, map))
                continue;

            float score = ScoreCandidate(cell, initiator, partner, map);
            if (score > bestScore)
            {
                bestScore = score;
                result = cell;
                found = true;
            }
        }

        return found;
    }

    private static bool IsCandidateValid(IntVec3 cell, Pawn initiator, Pawn partner, Map map)
    {
        if (!cell.Standable(map))
            return false;

        if (cell.GetDoor(map) != null)
            return false;

        if (cell.DistanceTo(initiator.Position) > MaxDistanceFromEitherPawn)
            return false;

        if (cell.DistanceTo(partner.Position) > MaxDistanceFromEitherPawn)
            return false;

        if (!initiator.CanReach(cell, PathEndMode.OnCell, Danger.Some))
            return false;

        if (!partner.CanReach(cell, PathEndMode.Touch, Danger.Some))
            return false;

        if (cell.IsForbidden(initiator) || cell.IsForbidden(partner))
            return false;

        Building edifice = cell.GetEdifice(map);
        if (edifice is Building_Bed)
            return false;

        Pawn occupant = cell.GetFirstPawn(map);
        if (occupant != null && occupant != initiator && occupant != partner)
            return false;

        return true;
    }

    private static float ScoreCandidate(IntVec3 cell, Pawn initiator, Pawn partner, Map map)
    {
        float score = 0f;

        Room room = cell.GetRoom(map);
        bool indoors = room != null && !room.PsychologicallyOutdoors;

        score += indoors ? 12f : -10f;
        score += ScoreRoomRole(room);
        score += ScoreRoomSize(room);

        float initiatorDist = cell.DistanceTo(initiator.Position);
        float partnerDist = cell.DistanceTo(partner.Position);

        // Prefer fairly local cells and slightly prefer a balanced midpoint.
        score -= (initiatorDist + partnerDist) * 0.70f;
        score -= (float)Math.Abs(initiatorDist - partnerDist) * 0.35f;

        // Corners and edges feel more private than open room centers.
        score -= CountOpenNeighbors(cell, map) * 0.90f;

        if (IsAdjacentToDoor(cell, map))
            score -= 8f;

        int nearbyWitnesses = CountNearbyAwakeWitnesses(cell, initiator, partner, map);
        int visibleWitnesses = CountVisibleAwakeWitnesses(cell, initiator, partner, map);
        int sameRoomWitnesses = CountSameRoomAwakeWitnesses(room, initiator, partner, map);

        score -= nearbyWitnesses * 3.5f;
        score -= visibleWitnesses * 5.0f;
        score -= sameRoomWitnesses * 4.5f;

        // If both pawns would still be in a crowded public room, keep pushing away.
        if (room != null && sameRoomWitnesses >= 3)
            score -= 6f;

        return score;
    }

    private static float ScoreRoomRole(Room room)
    {
        if (room == null)
            return 0f;

        string roleName = room.Role?.defName ?? string.Empty;
        if (roleName.Length == 0)
            return 0f;

        if (roleName.IndexOf("Barracks", StringComparison.OrdinalIgnoreCase) >= 0)
            return -14f;

        if (roleName.IndexOf("Hospital", StringComparison.OrdinalIgnoreCase) >= 0)
            return -13f;

        if (roleName.IndexOf("Prison", StringComparison.OrdinalIgnoreCase) >= 0)
            return -12f;

        if (roleName.IndexOf("Dining", StringComparison.OrdinalIgnoreCase) >= 0)
            return -12f;

        if (roleName.IndexOf("Rec", StringComparison.OrdinalIgnoreCase) >= 0)
            return -11f;

        if (roleName.IndexOf("Workshop", StringComparison.OrdinalIgnoreCase) >= 0 ||
            roleName.IndexOf("Laboratory", StringComparison.OrdinalIgnoreCase) >= 0 ||
            roleName.IndexOf("Lab", StringComparison.OrdinalIgnoreCase) >= 0)
            return -9f;

        if (roleName.IndexOf("Bedroom", StringComparison.OrdinalIgnoreCase) >= 0)
            return 6f;

        if (roleName.IndexOf("Storage", StringComparison.OrdinalIgnoreCase) >= 0)
            return 1f;

        return 0f;
    }

    private static float ScoreRoomSize(Room room)
    {
        if (room == null)
            return 0f;

        int cellCount = room.CellCount;

        if (cellCount <= 20)
            return 3f;

        if (cellCount <= 40)
            return 1.5f;

        if (cellCount >= 140)
            return -5f;

        if (cellCount >= 90)
            return -3f;

        return 0f;
    }

    private static int CountNearbyAwakeWitnesses(IntVec3 cell, Pawn initiator, Pawn partner, Map map)
    {
        int count = 0;

        foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
        {
            if (!CountsAsWitness(p, initiator, partner))
                continue;

            if (p.Position.DistanceTo(cell) <= NearbyWitnessRadius)
                count++;
        }

        return count;
    }

    private static int CountVisibleAwakeWitnesses(IntVec3 cell, Pawn initiator, Pawn partner, Map map)
    {
        int count = 0;

        foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
        {
            if (!CountsAsWitness(p, initiator, partner))
                continue;

            if (p.Position.DistanceTo(cell) > VisibleWitnessRadius)
                continue;

            if (GenSight.LineOfSight(p.Position, cell, map))
                count++;
        }

        return count;
    }

    private static int CountSameRoomAwakeWitnesses(Room room, Pawn initiator, Pawn partner, Map map)
    {
        if (room == null)
            return 0;

        int count = 0;

        foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
        {
            if (!CountsAsWitness(p, initiator, partner))
                continue;

            if (p.GetRoom() == room)
                count++;
        }

        return count;
    }

    private static bool CountsAsWitness(Pawn p, Pawn initiator, Pawn partner)
    {
        if (p == null || p == initiator || p == partner)
            return false;

        if (!p.Spawned || p.Dead || p.Downed)
            return false;

        if (!p.Awake())
            return false;

        if (p.RaceProps == null || !p.RaceProps.Humanlike)
            return false;

        return true;
    }

    private static int CountOpenNeighbors(IntVec3 cell, Map map)
    {
        int open = 0;

        for (int i = 0; i < 8; i++)
        {
            IntVec3 adj = cell + GenAdj.AdjacentCellsAndInside[i];
            if (!adj.InBounds(map))
                continue;

            if (adj.Standable(map))
                open++;
        }

        return open;
    }

    private static bool IsAdjacentToDoor(IntVec3 cell, Map map)
    {
        for (int i = 0; i < 8; i++)
        {
            IntVec3 adj = cell + GenAdj.AdjacentCellsAndInside[i];
            if (!adj.InBounds(map))
                continue;

            if (adj.GetDoor(map) != null)
                return true;
        }

        return false;
    }
}
