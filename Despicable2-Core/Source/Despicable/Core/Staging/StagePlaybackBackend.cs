using System.Collections.Generic;
using Verse;
using Despicable; // AnimGroupDef, AnimUtil live in root namespace

namespace Despicable.Core.Staging.Backends;
/// <summary>
/// Playback backend that maps StagePlan -> RAF AnimGroupDef/AnimRoleDef system.
/// Uses plan.BackendKey as AnimGroupDef.defName and slotIds as AnimRoleDef.defName.
/// </summary>
public sealed class StagePlaybackBackend : IStagePlaybackBackend
{
    public bool CanPlay(StagePlan plan)
    {
        if (plan == null) return false;
        if (!plan.PlaybackBackendKey.NullOrEmpty() && plan.PlaybackBackendKey != "rafAnimGroup") return false;
        if (plan.BackendKey.NullOrEmpty()) return false;
        return DefDatabase<AnimGroupDef>.GetNamedSilentFail(plan.BackendKey) != null;
    }

    public void Play(StagePlan plan)
    {
        if (plan == null) return;
        if (!plan.PlaybackBackendKey.NullOrEmpty() && plan.PlaybackBackendKey != "rafAnimGroup") return;

        AnimGroupDef group = null;
        if (!plan.BackendKey.NullOrEmpty())
            group = DefDatabase<AnimGroupDef>.GetNamedSilentFail(plan.BackendKey);

        if (group == null) return;

        Thing anchorThing = plan.Anchor.Thing;
        if (anchorThing == null && plan.SlotAssignments.Count > 0)
        {
            // Fallback anchor: first pawn
            foreach (var kv in plan.SlotAssignments)
            {
                anchorThing = kv.Value;
                break;
            }
        }

        // RAF expects roleDefName -> pawn
        var roleAssignments = new Dictionary<string, Pawn>(plan.SlotAssignments);
        AnimUtil.PlayAnimationGroup(group, roleAssignments, anchorThing);
    }
}
