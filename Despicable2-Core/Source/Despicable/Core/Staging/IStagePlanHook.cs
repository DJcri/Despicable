using System.Collections.Generic;

namespace Despicable.Core.Staging;
/// <summary>
/// Optional extension point: allow/score stage candidates and assignments.
/// Intended for content modules (ex: NSFW) to filter/score without Core knowing semantics.
/// </summary>
public interface IStagePlanHook
{
    /// <summary>
    /// Higher runs first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Return false to remove this candidate entirely.
    /// </summary>
    bool AllowCandidate(StagePlanner.StageCandidate candidate, StagePlanContext ctx, out string reason);

    /// <summary>
    /// Additive score. Higher wins. Called only after a valid assignment is found.
    /// </summary>
    float Score(StagePlanner.StageCandidate candidate, Dictionary<string, Verse.Pawn> assignment, StagePlanContext ctx);
}
