using System.Collections.Generic;

namespace Despicable.Core.Staging;
public static class StagePlaybackBackends
{
    private static readonly List<IStagePlaybackBackend> backends = new();

    public static void Register(IStagePlaybackBackend backend)
    {
        if (backend == null) return;
        if (backends.Contains(backend)) return;
        backends.Add(backend);
    }

    public static bool TryPlay(StagePlan plan)
    {
        if (plan == null) return false;

        for (int i = 0; i < backends.Count; i++)
        {
            var b = backends[i];
            if (b == null) continue;

            if (b.CanPlay(plan))
            {
                b.Play(plan);
                return true;
            }
        }

        return false;
    }
}
