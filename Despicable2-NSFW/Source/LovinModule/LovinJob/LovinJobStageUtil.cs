using RimWorld;
using Verse;
using Verse.AI;

namespace Despicable;

internal static class LovinJobStageUtil
{
    internal static string GetResolvedStageTag(Job job, Map map, string fallbackStageTag = null)
    {
        if (job == null)
            return fallbackStageTag;

        string stageTag = null;

        try
        {
            var store = Despicable.Core.InteractionInstanceStore.Get(map);
            if (store != null && store.TryGetStage(job.loadID, out var stageId) && !stageId.NullOrEmpty())
                stageTag = stageId;
        }
        catch
        {
        }

        if (stageTag.NullOrEmpty())
            stageTag = fallbackStageTag;

        if (stageTag.NullOrEmpty())
            stageTag = job.def?.GetModExtension<ModExtension_LovinType>()?.lovinType?.defName;

        return stageTag;
    }

    internal static LovinTypeDef GetChosenLovinType(Job job, Map map)
    {
        string stageTag = GetResolvedStageTag(job, map);
        return stageTag.NullOrEmpty() ? null : DefDatabase<LovinTypeDef>.GetNamedSilentFail(stageTag);
    }
}
