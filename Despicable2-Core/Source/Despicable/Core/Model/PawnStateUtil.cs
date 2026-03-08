using Verse;

namespace Despicable;
public static class PawnStateUtil
{
    public static bool IsAsleep(Pawn pawn)
    {
        return PawnQuery.IsAsleep(pawn);
    }

    public static bool IsInfant(Pawn pawn)
    {
        return PawnQuery.IsInfant(pawn);
    }

    // Legacy alias kept so existing call sites in older branches keep compiling.
    public static bool isInfant(Pawn pawn)
    {
        return PawnQuery.IsInfant(pawn);
    }

    public static bool ComparePawnGenderToByte(Pawn pawn, byte otherGender)
    {
        return PawnQuery.CompareGenderToByte(pawn, otherGender);
    }
}
