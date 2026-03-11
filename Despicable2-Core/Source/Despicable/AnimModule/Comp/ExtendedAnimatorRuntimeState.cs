using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable;

/// <summary>
/// Mutable per-instance runtime state for <see cref="CompExtendedAnimator"/>.
/// Extracted so playback, render, and side-effect collaborators can share one state bag
/// without storing orchestration fields directly on the comp shell.
/// </summary>
public sealed class ExtendedAnimatorRuntimeState
{
    public List<AnimationDef> animQueue;
    public bool hasAnimPlaying;
    public List<int> loopIndex;
    public int stage;
    public int curLoop = 1;
    public int animationTicks;

    public Thing anchor;
    public int rotation;
    public Vector3 offset = Vector3.zero;

    public bool usesExtendedAnimationFeatures;
    public AnimationDef lastSetAnimation;

    // Runtime-owned facial trigger scan state. This must stay per animator/pawn
    // so pair animations do not share a single trigger cursor.
    public int lastCheckedFacialTick = -1;
    public AnimationDef lastCheckedFacialAnimation;


    public void ExposeData()
    {
        Scribe_Collections.Look(ref animQueue, "animQueue");
        Scribe_Values.Look(ref hasAnimPlaying, "hasAnimPlaying", false);
        Scribe_Collections.Look(ref loopIndex, "loopIndex");
        Scribe_Values.Look(ref stage, "stage", 0);
        Scribe_Values.Look(ref curLoop, "curLoop", 1);
        Scribe_Values.Look(ref animationTicks, "animationTicks", 0);
        Scribe_References.Look(ref anchor, "anchor");
        Scribe_Values.Look(ref rotation, "rotation", 0);
        Scribe_Values.Look(ref offset, "offset", Vector3.zero);
        Scribe_Values.Look(ref usesExtendedAnimationFeatures, "usesExtendedAnimationFeatures", false);
    }

    public void ResetTransientState()
    {
        hasAnimPlaying = false;

        stage = 0;
        curLoop = 1;
        animationTicks = 0;

        rotation = 0;
        anchor = null;
        offset = Vector3.zero;

        usesExtendedAnimationFeatures = false;
        lastSetAnimation = null;
        lastCheckedFacialTick = -1;
        lastCheckedFacialAnimation = null;

        loopIndex?.Clear();
        animQueue?.Clear();
    }
}
