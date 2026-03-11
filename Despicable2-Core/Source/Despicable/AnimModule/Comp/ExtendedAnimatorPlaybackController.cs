using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable;

/// <summary>
/// Owns playback, queue progression, and reset flow for <see cref="CompExtendedAnimator"/>
/// while leaving the comp itself as a thin orchestration shell.
/// </summary>
public sealed class ExtendedAnimatorPlaybackController
{
    public void Step(Pawn pawn, ExtendedAnimatorRuntimeState runtime, ExtendedAnimatorEffectController effectController, ExtendedAnimatorRenderBridge renderBridge)
    {
        if (runtime == null) return;

        if (!runtime.animQueue.NullOrEmpty() && runtime.hasAnimPlaying)
        {
            if (runtime.animationTicks >= (runtime.animQueue[0]?.durationTicks ?? 0))
            {
                runtime.animationTicks = 0;
                runtime.curLoop++;

                int loopCount = 1;
                if (!runtime.loopIndex.NullOrEmpty() && runtime.stage >= 0 && runtime.stage < runtime.loopIndex.Count)
                    loopCount = Mathf.Max(1, runtime.loopIndex[runtime.stage]);

                if (runtime.curLoop <= loopCount)
                    Play(pawn, runtime, renderBridge);
                else
                {
                    runtime.stage++;
                    PlayNext(pawn, runtime, renderBridge);
                }
            }

            if (runtime.usesExtendedAnimationFeatures)
            {
                effectController?.CheckAndPlayFacialAnim(pawn, runtime);
                effectController?.CheckAndPlaySounds(pawn);
            }

            runtime.animationTicks++;
        }
    }

    public void Play(Pawn pawn, ExtendedAnimatorRuntimeState runtime, ExtendedAnimatorRenderBridge renderBridge)
    {
        if (runtime?.animQueue.NullOrEmpty() ?? true)
        {
            CommonUtil.DebugLog("[Despicable] - Animation queue empty; nothing to play.");
            return;
        }

        var anim = runtime.animQueue[0];
        runtime.usesExtendedAnimationFeatures = AnimationUsesExtendedFeatures(anim);
        pawn?.Drawer?.renderer?.SetAnimation(anim);
        runtime.hasAnimPlaying = true;
        VisualActivityTracker.SetExtendedAnimatorActive(pawn, true);
        GameComponent_ExtendedAnimatorRuntime.RegisterActive(pawn?.TryGetComp<CompExtendedAnimator>());

    }


    public void PlayNext(Pawn pawn, ExtendedAnimatorRuntimeState runtime, ExtendedAnimatorRenderBridge renderBridge)
    {
        if (runtime?.animQueue.NullOrEmpty() ?? true)
        {
            CommonUtil.DebugLog("[Despicable] - Animation queue empty; nothing to play next.");
            return;
        }

        runtime.animQueue.RemoveAt(0);

        if (runtime.animQueue.NullOrEmpty())
        {
            Reset(pawn, runtime, renderBridge);
            return;
        }

        runtime.curLoop = 1;
        Play(pawn, runtime, renderBridge);
    }

    public void PlaySingle(Pawn pawn, ExtendedAnimatorRuntimeState runtime, AnimationDef anim, Thing anchor = null, AnimationOffsetDef offsetDef = null, ExtendedAnimatorRenderBridge renderBridge = null)
    {
        if (anim == null || runtime == null) return;

        Reset(pawn, runtime, renderBridge);

        runtime.anchor = anchor;
        ApplyOffsets(pawn, runtime, offsetDef);

        EnsureAnimQueue(runtime);
        runtime.animQueue.Add(anim);

        Play(pawn, runtime, renderBridge);
    }

    public void PlayQueue(Pawn pawn, ExtendedAnimatorRuntimeState runtime, AnimGroupDef animGroupDef, List<AnimationDef> anims, AnimationOffsetDef offsetDef = null, Thing anchor = null, ExtendedAnimatorRenderBridge renderBridge = null)
    {
        if (runtime == null) return;

        Reset(pawn, runtime, renderBridge);

        runtime.anchor = anchor;
        ApplyOffsets(pawn, runtime, offsetDef);

        if (!anims.NullOrEmpty())
        {
            EnsureAnimQueue(runtime);
            runtime.animQueue.AddRange(anims);
        }

        if (animGroupDef?.loopIndex != null)
        {
            EnsureLoopIndex(runtime);
            runtime.loopIndex.AddRange(animGroupDef.loopIndex);
        }

        Play(pawn, runtime, renderBridge);
    }

    public void RefreshExtendedFeatureUsageFromCurrentAnimation(Pawn pawn, ExtendedAnimatorRuntimeState runtime)
    {
        if (runtime == null) return;

        AnimationDef activeAnim = pawn?.Drawer?.renderer?.CurAnimation;
        if (activeAnim == null && !runtime.animQueue.NullOrEmpty())
            activeAnim = runtime.animQueue[0];

        runtime.usesExtendedAnimationFeatures = AnimationUsesExtendedFeatures(activeAnim);
    }

    public void Reset(Pawn pawn, ExtendedAnimatorRuntimeState runtime, ExtendedAnimatorRenderBridge renderBridge)
    {
        if (runtime == null) return;

        GameComponent_ExtendedAnimatorRuntime.UnregisterActive(pawn?.TryGetComp<CompExtendedAnimator>());
        VisualActivityTracker.SetExtendedAnimatorActive(pawn, false);
        pawn?.Drawer?.renderer?.SetAnimation(null);

        runtime.ResetTransientState();

        pawn?.Drawer?.renderer?.SetAllGraphicsDirty();

    }

    private static void ApplyOffsets(Pawn pawn, ExtendedAnimatorRuntimeState runtime, AnimationOffsetDef offsetDef)
    {
        BaseAnimationOffset offsets = null;
        offsetDef?.FindOffset(pawn, out offsets);

        runtime.offset = offsets?.getOffset(pawn) ?? Vector3.zero;
        runtime.rotation = offsets?.getRotation(pawn) ?? 0;
    }

    private static void EnsureAnimQueue(ExtendedAnimatorRuntimeState runtime)
    {
        if (runtime.animQueue == null)
            runtime.animQueue = new List<AnimationDef>();
    }

    private static void EnsureLoopIndex(ExtendedAnimatorRuntimeState runtime)
    {
        if (runtime.loopIndex == null)
            runtime.loopIndex = new List<int>();
    }

    private static bool AnimationUsesExtendedFeatures(AnimationDef anim)
    {
        if (anim?.keyframeParts != null)
        {
            foreach (var kv in anim.keyframeParts)
            {
                if (kv.Value is KeyframeAnimationPart kap && kap.keyframes != null)
                {
                    for (int i = 0; i < kap.keyframes.Count; i++)
                    {
                        if (kap.keyframes[i] is ExtendedKeyframe)
                            return true;
                    }
                }
            }
        }

        return false;
    }
}
