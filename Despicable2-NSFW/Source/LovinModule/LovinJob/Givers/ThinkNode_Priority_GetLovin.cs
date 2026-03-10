using RimWorld;
using Verse;
using Verse.AI;
using Despicable.NSFW.Integrations;

namespace Despicable;

/// <summary>
/// Priority-bearing non-bed autonomous lovin node for MainColonistBehaviorCore.
/// Reuses the existing JobGiver_GetLovin job issuance logic, but exposes
/// a real priority contract so ThinkNode_PrioritySorter can consider it safely.
/// </summary>
public class ThinkNode_Priority_GetLovin : JobGiver_GetLovin
{
    public float priority = 4.5f;

    public override float GetPriority(Pawn pawn)
    {
        if (pawn == null)
            return 0f;

        if (IntegrationGuards.ShouldDeferLovinToIntimacy())
            return 0f;

        if (!CommonUtil.GetSettings().lovinExtensionEnabled)
            return 0f;

        if (!LovinUtil.CouldUseSomeLovin(pawn))
            return 0f;

        Pawn partner = LovinUtil.FindPartner(pawn);
        if (partner == null)
            return 0f;

        if (!pawn.CanReserveAndReach(partner, PathEndMode.Touch, Danger.Deadly, 1, -1, null, false))
            return 0f;

        if (LovinUtil.FindAutonomousLovinType(pawn, partner) == null)
            return 0f;

        return priority;
    }
}
