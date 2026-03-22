using Verse;

namespace Despicable;
internal sealed class GameComponent_LegacyAnatomyMigration : GameComponent
{
    public GameComponent_LegacyAnatomyMigration(Game game)
    {
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        LongEventHandler.ExecuteWhenFinished(RunSweep);
    }

    private static void RunSweep()
    {
        int migrated = LegacyAnatomyMigration.SweepLoadedGamePawns();
        if (Prefs.DevMode && migrated > 0)
            Log.Message($"[Despicable NSFW] Legacy anatomy bridge migrated {migrated} pawn(s) from old genital hediffs into anatomy instances.");
    }
}
