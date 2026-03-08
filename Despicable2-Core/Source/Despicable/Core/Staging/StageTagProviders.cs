using System.Collections.Generic;
using Verse;

namespace Despicable.Core.Staging;
public static class StageTagProviders
{
    private static readonly List<IStagePawnTagProvider> providers = new();

    public static void Register(IStagePawnTagProvider provider)
    {
        if (provider == null) return;
        if (providers.Contains(provider)) return;
        providers.Add(provider);
        providers.Sort((a, b) => b.Priority.CompareTo(a.Priority));
    }

    public static void GetTagsForPawn(Pawn pawn, StageTagContext ctx, HashSet<string> into)
    {
        if (into == null) return;
        into.Clear();
        for (int i = 0; i < providers.Count; i++)
        {
            var p = providers[i];
            if (p == null) continue;
            p.AddTags(pawn, ctx, into);
        }
    }
}
