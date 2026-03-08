using System.Linq;
using RimWorld;
using Verse;

namespace Despicable;
public static class PawnResolver
{
    public static Pawn TryResolveById(string pawnId)
    {
        if (pawnId.NullOrEmpty()) return null;

        if (Find.Maps != null)
        {
            for (int i = 0; i < Find.Maps.Count; i++)
            {
                Map map = Find.Maps[i];
                var pawns = map?.mapPawns?.AllPawns;
                if (pawns == null) continue;

                for (int p = 0; p < pawns.Count; p++)
                {
                    Pawn pawn = pawns[p];
                    if (pawn != null && pawn.GetUniqueLoadID() == pawnId)
                        return pawn;
                }
            }
        }

        var worldPawns = Find.WorldPawns?.AllPawnsAliveOrDead;
        if (worldPawns != null)
        {
            for (int i = 0; i < worldPawns.Count; i++)
            {
                Pawn pawn = worldPawns[i];
                if (pawn != null && pawn.GetUniqueLoadID() == pawnId)
                    return pawn;
            }
        }

        return null;
    }

    public static Faction TryResolveFactionById(int factionId)
    {
        if (factionId <= 0) return null;
        if (Find.FactionManager == null) return null;
        return Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f != null && f.loadID == factionId);
    }
}
