using System.Collections.Generic;
using Verse;

namespace Despicable.Core.Staging;
// Input to the staging planner.
public sealed class StageRequest
{
    public List<Pawn> Participants = new();
    public string StageTag;
    public LocalTargetInfo Anchor = LocalTargetInfo.Invalid;
}
