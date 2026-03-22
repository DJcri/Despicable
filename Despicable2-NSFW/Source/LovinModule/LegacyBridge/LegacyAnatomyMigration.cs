using RimWorld;

using Verse;

namespace Despicable;
internal static class LegacyAnatomyMigration
{
    internal static bool NeedsMigration(Pawn pawn)
    {
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return false;

        if (!LegacyAnatomyDefLookup.HasBridgeDefs)
            return false;

        return HasLegacyHediffAnywhere(pawn, LegacyAnatomyDefLookup.PenisHediff)
            || HasLegacyHediffAnywhere(pawn, LegacyAnatomyDefLookup.VaginaHediff);
    }

    internal static int SweepLoadedGamePawns()
    {
        int migrated = 0;
        foreach (Pawn pawn in PawnsFinder.AllMapsAndWorld_Alive)
        {
            if (!NeedsMigration(pawn))
                continue;

            if (AnatomyBootstrapper.TryResolveAndApply(pawn, forceResync: true) == AnatomyBootstrapResult.Resolved)
                migrated++;
        }

        return migrated;
    }

    internal static bool HasLegacyHediffAnywhere(Pawn pawn, HediffDef def)
    {
        if (pawn?.health?.hediffSet?.hediffs == null || def == null)
            return false;

        var hediffs = pawn.health.hediffSet.hediffs;
        for (int i = 0; i < hediffs.Count; i++)
        {
            if (hediffs[i]?.def == def)
                return true;
        }

        return false;
    }
}
