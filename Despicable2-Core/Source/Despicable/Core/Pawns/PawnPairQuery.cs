using RimWorld;

using Verse;

namespace Despicable;
public static class PawnPairQuery
{
    public static bool AreHostile(Pawn initiator, Pawn recipient)
    {
        return initiator != null && recipient != null && initiator.HostileTo(recipient);
    }
}
