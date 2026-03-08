using Verse;

namespace Despicable;
public sealed class GameComponent_DespicableRuntimeReset : GameComponent
{
    public GameComponent_DespicableRuntimeReset(Game game)
    {
    }

    public override void StartedNewGame()
    {
        base.StartedNewGame();
        DespicableRuntimeState.ResetRuntimeState();
        GameComponent_ExtendedAnimatorRuntime.ResetRuntimeState();
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        DespicableRuntimeState.ResetRuntimeState();
        GameComponent_ExtendedAnimatorRuntime.ResetRuntimeState();
        LongEventHandler.ExecuteWhenFinished(GameComponent_ExtendedAnimatorRuntime.NotifyLoadedGame);
    }
}
