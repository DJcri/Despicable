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
        LongEventHandler.ExecuteWhenFinished(GameComponent_FacePartsTick.NotifyRuntimeReset);
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        DespicableRuntimeState.ResetRuntimeState();
        LongEventHandler.ExecuteWhenFinished(GameComponent_FacePartsTick.NotifyRuntimeReset);
        LongEventHandler.ExecuteWhenFinished(GameComponent_ExtendedAnimatorRuntime.NotifyLoadedGame);
    }

    public override void GameComponentTick()
    {
        base.GameComponentTick();
        AutoEyePatchRuntime.ProcessQueuedGenerationBudget();
    }
}
