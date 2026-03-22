using RimWorld;
using Verse;

namespace Despicable;

/// <summary>
/// Lightweight solo spot finder that prefers the pawn's current bed if already lying down,
/// otherwise a reachable indoor standable cell near their bed before falling back to nearby indoor cells.
/// </summary>
public static class SelfLovinSpotFinder
{
    public static bool TryFindSpot(Pawn pawn, out IntVec3 result)
    {
        result = IntVec3.Invalid;

        if (pawn == null || !pawn.Spawned || pawn.Map == null)
            return false;

        if (pawn.InBed())
        {
            result = pawn.Position;
            return true;
        }

        Building_Bed bed = RestUtility.FindBedFor(pawn, pawn, checkSocialProperness: true);
        if (bed != null && TryFindSpotNearBed(pawn, bed, out result))
            return true;

        return TryFindNearbyIndoorSpot(pawn, pawn.Position, out result);
    }

    private static bool TryFindSpotNearBed(Pawn pawn, Building_Bed bed, out IntVec3 result)
    {
        result = IntVec3.Invalid;

        if (pawn == null || bed == null)
            return false;

        Map map = pawn.Map;
        if (map == null || bed.Map != map)
            return false;

        Room bedRoom = bed.GetRoom();
        float bestScore = float.MinValue;
        bool found = false;

        foreach (IntVec3 cell in GenRadial.RadialCellsAround(bed.Position, 4.5f, useCenter: true))
        {
            if (!IsValidCandidate(pawn, map, cell))
                continue;

            Room room = cell.GetRoom(map);
            float score = 0f;

            if (bedRoom != null && room == bedRoom)
                score += 100f;

            score -= cell.DistanceToSquared(bed.Position);
            score -= cell.DistanceToSquared(pawn.Position) * 0.25f;

            if (cell == pawn.Position)
                score += 5f;

            if (score > bestScore)
            {
                bestScore = score;
                result = cell;
                found = true;
            }
        }

        return found;
    }

    private static bool TryFindNearbyIndoorSpot(Pawn pawn, IntVec3 origin, out IntVec3 result)
    {
        result = IntVec3.Invalid;

        if (pawn == null || !pawn.Spawned || pawn.Map == null)
            return false;

        Map map = pawn.Map;

        foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, 12f, useCenter: true))
        {
            if (!IsValidCandidate(pawn, map, cell))
                continue;

            result = cell;
            return true;
        }

        result = origin;
        return true;
    }

    private static bool IsValidCandidate(Pawn pawn, Map map, IntVec3 cell)
    {
        if (!cell.InBounds(map) || !cell.Standable(map))
            return false;

        if (!pawn.CanReach(cell, Verse.AI.PathEndMode.OnCell, Danger.Some))
            return false;

        if (cell.IsForbidden(pawn))
            return false;

        Room room = cell.GetRoom(map);
        if (room != null && room.PsychologicallyOutdoors)
            return false;

        if (cell.GetDoor(map) != null)
            return false;

        Pawn occupant = cell.GetFirstPawn(map);
        if (occupant != null && occupant != pawn)
            return false;

        return true;
    }
}
