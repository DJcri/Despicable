using Verse;

namespace Despicable.NSFW.Runtime;
internal sealed class GameComponent_DespicableNsfwRuntimeReset : GameComponent
{
    public GameComponent_DespicableNsfwRuntimeReset(Game game)
    {
    }

    public override void StartedNewGame()
    {
        base.StartedNewGame();
        DespicableNsfwRuntimeState.ResetRuntimeState();
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        DespicableNsfwRuntimeState.ResetRuntimeState();
        LongEventHandler.ExecuteWhenFinished(global::Despicable.CompLovinParts.RehydrateAllLovinPartsAfterRuntimeReset);
    }
}
