using HarmonyLib;
using Verse;

namespace Despicable;
/// <summary>
/// During workshop portrait rendering, we want the render-tree to sample keyframes at the workshop scrubber tick
/// (even when the pawn isn't "playing" an animation in-game). The vanilla render tree uses AnimationTick to
/// decide which keyframes apply.
/// </summary>
[HarmonyPatch(typeof(PawnRenderTree), nameof(PawnRenderTree.AnimationTick), MethodType.Getter)]
public static class HarmonyPatch_PawnRenderTree_AnimationTick
{
    public static bool Prefix(ref int __result)
    {
        if (!WorkshopRenderContext.Active)
            return true;

        __result = WorkshopRenderContext.Tick;
        return false;
    }
}
