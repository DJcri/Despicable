using RimWorld;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Safe resolvers for cross-references stored as IDs.
/// Keeps token application and UI resilient to missing maps / despawned pawns.
/// </summary>
public static class HKResolve
{
    public static Pawn TryResolvePawnById(string pawnId)
    {
        return global::Despicable.PawnResolver.TryResolveById(pawnId);
    }

    public static Faction TryResolveFactionById(int factionId)
    {
        return global::Despicable.PawnResolver.TryResolveFactionById(factionId);
    }
}
