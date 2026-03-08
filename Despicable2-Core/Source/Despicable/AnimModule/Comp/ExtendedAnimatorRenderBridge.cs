using Verse;

namespace Despicable;

/// <summary>
/// Handles render-tree invalidation for <see cref="CompExtendedAnimator"/> while preserving
/// per-comp transition dedupe through <see cref="ExtendedAnimatorRuntimeState"/>.
/// </summary>
public sealed class ExtendedAnimatorRenderBridge
{
    public void MarkRenderTreeDirtyIfAnimationChanged(Pawn pawn, ExtendedAnimatorRuntimeState runtime, AnimationDef newAnim)
    {
        if (runtime == null) return;
        if (runtime.lastSetAnimation == newAnim) return;

        runtime.lastSetAnimation = newAnim;

        PawnRenderTree tree = pawn?.Drawer?.renderer?.renderTree;
        tree?.SetDirty();
    }
}
