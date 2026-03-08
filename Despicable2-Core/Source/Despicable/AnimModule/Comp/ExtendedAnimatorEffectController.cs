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

    public void CheckAndPlayFacialAnim(Pawn pawn)
    {
        if (pawn == null || ModMain.IsNlFacialInstalled)
            return;

        if (!TryGetExtendedKeyframeContext(pawn, out AnimationWorker_ExtendedKeyframes animWorker, out AnimationPart animPart, out int animationTick))
            return;

        FacialAnimDef facialAnim = animWorker.FacialAnimAtTick(animationTick, animPart);
        if (facialAnim == null)
            return;

        CompFaceParts facePartsComp = pawn.TryGetComp<CompFaceParts>();
        facePartsComp?.PlayFacialAnim(facialAnim);
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
