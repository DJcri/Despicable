using Verse;
using Verse.Sound;

namespace Despicable;

/// <summary>
/// Handles extended side-effect triggers (facial animations and sounds) for the current
/// animation tick without owning playback state.
/// </summary>
public sealed class ExtendedAnimatorEffectController
{
    private static bool TryGetExtendedKeyframeContext(
        Pawn pawn,
        out AnimationWorker_ExtendedKeyframes animWorker,
        out AnimationPart animPart,
        out int animationTick)
    {
        animWorker = null;
        animPart = null;
        animationTick = 0;

        PawnRenderNode rootNode = pawn?.Drawer?.renderer?.renderTree?.rootNode;
        if (!(rootNode?.AnimationWorker is AnimationWorker_ExtendedKeyframes worker))
            return false;

        if (rootNode.tree == null)
            return false;

        if (!rootNode.tree.TryGetAnimationPartForNode(rootNode, out AnimationPart part) || part == null)
            return false;

        animWorker = worker;
        animPart = part;
        animationTick = rootNode.tree.AnimationTick;
        return true;
    }

    public void CheckAndPlayFacialAnim(Pawn pawn, ExtendedAnimatorRuntimeState runtime)
    {
        if (pawn == null || runtime == null || ModMain.IsNlFacialInstalled)
            return;

        if (!TryGetExtendedKeyframeContext(pawn, out AnimationWorker_ExtendedKeyframes animWorker, out AnimationPart animPart, out int animationTick))
            return;

        AnimationDef currentAnimation = pawn.Drawer?.renderer?.CurAnimation;
        if (runtime.lastCheckedFacialAnimation != currentAnimation)
        {
            runtime.lastCheckedFacialAnimation = currentAnimation;
            runtime.lastCheckedFacialTick = -1;
        }

        // Detect animation loop or reset: if the current tick went backwards,
        // restart the scan window from the beginning of the animation.
        if (animationTick < runtime.lastCheckedFacialTick)
            runtime.lastCheckedFacialTick = -1;

        FacialAnimDef facialAnim = ScanFacialAnimInRange(runtime.lastCheckedFacialTick, animationTick, animPart);
        runtime.lastCheckedFacialTick = animationTick;

        if (facialAnim == null)
            return;

        CompFaceParts facePartsComp = pawn.TryGetComp<CompFaceParts>();
        facePartsComp?.PlayFacialAnim(facialAnim);
    }

    // Returns the last FacialAnimDef whose keyframe tick falls in (fromExclusive, toInclusive].
    // If multiple triggers land in the same scan window (e.g. after a large tick skip) we fire
    // the latest one — same as seeing them arrive in sequence.
    private static FacialAnimDef ScanFacialAnimInRange(int fromExclusive, int toInclusive, AnimationPart part)
    {
        if (!(part is KeyframeAnimationPart kap) || kap.keyframes.NullOrEmpty())
            return null;

        FacialAnimDef result = null;
        for (int i = 0; i < kap.keyframes.Count; i++)
        {
            int kTick = kap.keyframes[i].tick;
            if (kTick > fromExclusive && kTick <= toInclusive)
            {
                FacialAnimDef candidate = (kap.keyframes[i] as ExtendedKeyframe)?.facialAnim;
                if (candidate != null)
                    result = candidate;
            }
        }
        return result;
    }

    public void CheckAndPlaySounds(Pawn pawn)
    {
        if (pawn == null)
            return;

        if (!TryGetExtendedKeyframeContext(pawn, out AnimationWorker_ExtendedKeyframes animWorker, out AnimationPart animPart, out int animationTick))
            return;

        float volumeSetting = CommonUtil.GetSettings()?.soundVolume ?? 1f;

        SoundDef sound = animWorker.SoundAtTick(animationTick, animPart, pawn);
        if (sound == null)
            return;

        SoundInfo soundInfo = new TargetInfo(pawn.Position, pawn.Map);
        soundInfo.volumeFactor = volumeSetting;
        sound.PlayOneShot(soundInfo);
    }
}
