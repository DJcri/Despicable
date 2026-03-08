using System.Collections.Generic;
using Verse;

namespace Despicable;
/// <summary>
/// ANIMATION REMOTE CONTROL
/// </summary>
public static class AnimUtil
{
    public static void PlayAnimationGroup(AnimGroupDef animGroupDef, Dictionary<string, Pawn> roleAssignments, Thing anchor = null)
    {
        if (animGroupDef == null || roleAssignments == null) return;

        foreach (string animRoleDefName in roleAssignments.Keys)
        {
            AnimRoleDef animRole = DefDatabase<AnimRoleDef>.GetNamedSilentFail(animRoleDefName);
            Pawn pawn = roleAssignments[animRoleDefName];

            if (animRole == null || pawn == null) continue;

            CompExtendedAnimator animator = pawn.GetComp<CompExtendedAnimator>();
            if (animator == null) continue;

            animator.PlayQueue(animGroupDef, animRole.anims, animRole.offsetDef, anchor);
        }
    }

    public static void ResetAnimatorsForGroup(List<Pawn> pawns)
    {
        if (pawns == null) return;
        foreach (Pawn pawn in pawns)
        {
            pawn?.GetComp<CompExtendedAnimator>()?.Reset();
        }
    }

    /// <summary>
    /// Simple play animation function for debugging/testing purposes
    /// </summary>
    public static void PlayByAnimDefName(Pawn pawn, string defName)
    {
        if (pawn == null || defName.NullOrEmpty()) return;
        pawn.Drawer.renderer.SetAnimation(DefDatabase<AnimationDef>.GetNamed(defName));
    }
}
