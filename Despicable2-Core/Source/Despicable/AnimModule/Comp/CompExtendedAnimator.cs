using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable;
// Guardrail-Reason: Extended animator remains the single orchestration point for playback, render-node hooks, and effect triggers.
/// <summary>
/// Handles playing pawn animations with optional anchor alignment and per-pawn offsets.
/// Uses a single AnimationDef queue path. Extended extras activate only when the active clip
/// contains ExtendedKeyframes.
/// </summary>
public class CompExtendedAnimator : ThingComp
{
    private readonly ExtendedAnimatorRuntimeState _runtime = new();
    private static readonly ExtendedAnimatorRenderBridge RenderBridge = new();
    private static readonly ExtendedAnimatorEffectController EffectController = new();
    private static readonly ExtendedAnimatorPropNodeBuilder PropNodeBuilder = new();
    private static readonly ExtendedAnimatorPlaybackController PlaybackController = new();

    public List<AnimationDef> animQueue => _runtime.animQueue;

    public bool hasAnimPlaying { get => _runtime.hasAnimPlaying; private set => _runtime.hasAnimPlaying = value; }
    public int animationTicks { get => _runtime.animationTicks; private set => _runtime.animationTicks = value; }

    // Anchor + staging transforms (used by DrawPos patch and render-tree patches)
    public Thing anchor { get => _runtime.anchor; private set => _runtime.anchor = value; }
    public int rotation { get => _runtime.rotation; private set => _runtime.rotation = value; }
    public Vector3 offset { get => _runtime.offset; private set => _runtime.offset = value; }

    private bool usesExtendedAnimationFeatures
    {
        get => _runtime.usesExtendedAnimationFeatures;
        set => _runtime.usesExtendedAnimationFeatures = value;
    }

    private Pawn pawn => parent as Pawn;

    public bool UsesExtendedAnimationFeatures => usesExtendedAnimationFeatures;


    public override void PostExposeData()
    {
        base.PostExposeData();

        _runtime.ExposeData();

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            PlaybackController.RefreshExtendedFeatureUsageFromCurrentAnimation(pawn, _runtime);
            VisualActivityTracker.SetExtendedAnimatorActive(pawn, hasAnimPlaying);
        }
    }

    public void Play()
    {
        PlaybackController.Play(pawn, _runtime, RenderBridge);
    }

    public void PlayNext()
    {
        PlaybackController.PlayNext(pawn, _runtime, RenderBridge);
    }

    public void PlaySingle(AnimationDef anim, Thing anchor = null, AnimationOffsetDef offsetDef = null)
    {
        PlaybackController.PlaySingle(pawn, _runtime, anim, anchor, offsetDef, RenderBridge);
    }

    public void PlayQueue(AnimGroupDef animGroupDef, List<AnimationDef> anims, AnimationOffsetDef offsetDef = null, Thing anchor = null)
    {
        PlaybackController.PlayQueue(pawn, _runtime, animGroupDef, anims, offsetDef, anchor, RenderBridge);
    }

    public void SetOffset(Vector3 newOffset)
    {
        offset = newOffset;
    }

    public void SetRotation(int newRotation)
    {
        rotation = newRotation;
    }

    public void Reset()
    {
        PlaybackController.Reset(pawn, _runtime, RenderBridge);
    }

    public override List<PawnRenderNode> CompRenderNodes()
    {
        return PropNodeBuilder.BuildNodes(pawn, _runtime);
    }

    public bool AnimationMakesUseOfProp(AnimationPropDef animationProp)
    {
        return PropNodeBuilder.AnimationMakesUseOfProp(_runtime, animationProp);
    }

    public void CheckAndPlayFacialAnim()
    {
        EffectController.CheckAndPlayFacialAnim(pawn, _runtime);
    }

    public void CheckAndPlaySounds()
    {
        EffectController.CheckAndPlaySounds(pawn);
    }


    internal void StepPlayback()
    {
        PlaybackController.Step(pawn, _runtime, EffectController, RenderBridge);
    }

    internal void RegisterWithRuntimeIfActive()
    {
        if (hasAnimPlaying)
            GameComponent_ExtendedAnimatorRuntime.RegisterActive(this);
    }

    internal void UnregisterFromRuntime()
    {
        GameComponent_ExtendedAnimatorRuntime.UnregisterActive(this);
    }

    internal void RehydrateAfterRuntimeReset()
    {
        PlaybackController.RefreshExtendedFeatureUsageFromCurrentAnimation(pawn, _runtime);
        VisualActivityTracker.SetExtendedAnimatorActive(pawn, hasAnimPlaying);
        if (hasAnimPlaying)
            GameComponent_ExtendedAnimatorRuntime.RegisterActive(this);
        else
            GameComponent_ExtendedAnimatorRuntime.UnregisterActive(this);
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        RegisterWithRuntimeIfActive();
    }

    public override void PostDeSpawn(Map map, DestroyMode mode)
    {
        UnregisterFromRuntime();
        VisualActivityTracker.SetExtendedAnimatorActive(pawn, false);
        base.PostDeSpawn(map, mode);
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        UnregisterFromRuntime();
        VisualActivityTracker.SetExtendedAnimatorActive(pawn, false);
        base.PostDestroy(mode, previousMap);
    }

}
