using RimWorld;
using Verse;

namespace Despicable;

/// <summary>
/// Lightweight solo spot finder that prefers owned/current beds, then nearby indoor standable cells.
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
        if (bed != null)
        {
            result = bed.Position;
            return true;
        }

        Map map = pawn.Map;
        IntVec3 origin = pawn.Position;

        foreach (IntVec3 cell in GenRadial.RadialCellsAround(origin, 12f, useCenter: true))
        {
            if (!cell.InBounds(map) || !cell.Standable(map))
                continue;

            if (!pawn.CanReach(cell, Verse.AI.PathEndMode.OnCell, Danger.Some))
                continue;

            if (cell.IsForbidden(pawn))
                continue;

            Room room = cell.GetRoom(map);
            if (room != null && room.PsychologicallyOutdoors)
                continue;

            if (cell.GetDoor(map) != null)
                continue;

            result = cell;
            return true;
        }

        result = origin;
        return true;
    }
}
