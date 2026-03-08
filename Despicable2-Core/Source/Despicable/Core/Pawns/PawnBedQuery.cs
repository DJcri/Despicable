using RimWorld;

using Verse;

namespace Despicable;
public static class PawnBedQuery
{
    public static bool IsInBed(Pawn pawn)
    {
        return pawn != null && pawn.InBed();
    }
}
