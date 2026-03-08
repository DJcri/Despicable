using System.Collections.Generic;
using Verse;

namespace Despicable.Core.Staging;
/// <summary>
/// Provides opaque string tags describing a pawn for staging/slot assignment.
/// Tags are interpreted by content modules, not by Core.
/// </summary>
public interface IStagePawnTagProvider
{
    /// <summary>
    /// Higher runs first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Add tags for the pawn into <paramref name="into"/>.
    /// Implementations should avoid heavy allocations.
    /// </summary>
    void AddTags(Pawn pawn, StageTagContext ctx, HashSet<string> into);
}
