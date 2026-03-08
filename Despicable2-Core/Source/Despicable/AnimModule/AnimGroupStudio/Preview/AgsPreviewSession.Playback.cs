using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable;

namespace Despicable.AnimGroupStudio.Preview;
public sealed partial class AgsPreviewSession
{
    public void Pause()
    {
        isPlaying = false;
        hasPlayback = true;
    }

    public void Resume()
    {
        if (hasPlayback && HasBoundSource())
            isPlaying = true;
    }


    public void SetSpeed(float speed)
    {
        this.speed = Mathf.Clamp(speed, 0.1f, 4f);
    }


    public void Stop()
    {
        isPlaying = false;
        hasPlayback = false;
        loopSequence = false;
        tickAccumulator = 0f;

        playStartStage = 0;
        playEndStage = 0;
        currentStage = 0;
        stageStartTick = 0;
        stageDurationTicks = 0;
        schedulerTick = 0;
        stageRepeatTarget = 1;
        stageRepeatCount = 1;

        for (int i = 0; i < slots.Count; i++)
            slots[i].Queue = null;

        try
        {
            WorkshopRenderContext.SetTick(0);
        }
        catch (System.Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "AgsPreviewSession.EmptyCatch:6",
                "AGS preview session best-effort step failed.",
                e);
        }
        using (new WorkshopRenderContext.Scope(active: true, tick: 0))
        {
            for (int i = 0; i < slots.Count; i++)
            {
                try
                {
                    slots[i]?.Pawn?.Drawer?.renderer?.SetAnimation(null);
                }
                catch (System.Exception e)
                {
                    Despicable.Core.DebugLogger.WarnExceptionOnce(
                        "AgsPreviewSession.EmptyCatch:7",
                        "AGS preview session best-effort step failed.",
                        e);
                }
            }
        }
    }

    public void Play(bool fromStageToEnd, bool loopSelectedStage)
    {
        Play(fromStageToEnd, loopSelectedStage, false);
    }

    public void Play(bool fromStageToEnd, bool loopSelectedStage, bool loopSequence)
    {
        if (!HasBoundSource() || stageCount <= 0 || slots.Count == 0) return;

        Stop();
        isPlaying = true;
        hasPlayback = true;
        this.loopCurrentStage = loopSelectedStage;
        this.loopSequence = loopSequence;

        playStartStage = Mathf.Clamp(selectedStageIndex, 0, stageCount - 1);
        playEndStage = fromStageToEnd ? stageCount - 1 : playStartStage;

        currentStage = playStartStage;
        stageStartTick = 0;
        schedulerTick = 0;
        stageDurationTicks = ComputeStageDurationTicks(currentStage);
        stageRepeatTarget = ComputeStageRepeatTarget(currentStage);
        stageRepeatCount = 1;

        for (int i = 0; i < slots.Count; i++)
        {
            var st = slots[i];
            st.Queue = null;

            if (useRuntimeSource)
            {
                if (st?.RuntimeAnimationsByStage == null) continue;
                var list = new List<AnimationDef>();
                for (int s = playStartStage; s <= playEndStage; s++)
                {
                    if (s < 0 || s >= st.RuntimeAnimationsByStage.Count) continue;
                    var anim = st.RuntimeAnimationsByStage[s];
                    if (anim != null) list.Add(anim);
                }
                st.Queue = list;
                continue;
            }

            if (st?.RoleDef?.anims == null) continue;

            var queue = new List<AnimationDef>();
            for (int s = playStartStage; s <= playEndStage; s++)
            {
                if (s < 0 || s >= st.RoleDef.anims.Count) continue;
                var anim = st.RoleDef.anims[s];
                if (anim != null) queue.Add(anim);
            }
            st.Queue = queue;
        }

        ApplyStageAnimations(stageIndex: currentStage, tick: 0);
    }

    public void Update(float deltaTime)
    {
        if (!isPlaying) return;

        const float TicksPerSecond = 60f;
        tickAccumulator += deltaTime * TicksPerSecond * speed;

        while (tickAccumulator >= 1f)
        {
            tickAccumulator -= 1f;

            schedulerTick++;
            int stageLocalTick = Mathf.Max(0, schedulerTick - stageStartTick);
            try
            {
                WorkshopRenderContext.SetTick(stageLocalTick);
            }
            catch (System.Exception e)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce(
                    "AgsPreviewSession.EmptyCatch:8",
                    "AGS preview session best-effort step failed.",
                    e);
            }

            if (stageDurationTicks <= 0) stageDurationTicks = 60;

            int elapsed = schedulerTick - stageStartTick;
            if (elapsed >= stageDurationTicks)
            {
                if (loopCurrentStage)
                {
                    stageStartTick = schedulerTick;
                    try
                    {
                        WorkshopRenderContext.SetTick(0);
                    }
                    catch (System.Exception e)
                    {
                        Despicable.Core.DebugLogger.WarnExceptionOnce(
                            "AgsPreviewSession.EmptyCatch:9",
                            "AGS preview session best-effort step failed.",
                            e);
                    }
                    ApplyStageAnimations(currentStage, 0);
                    continue;
                }

                if (stageRepeatCount < stageRepeatTarget)
                {
                    stageRepeatCount++;
                    stageStartTick = schedulerTick;
                    try
                    {
                        WorkshopRenderContext.SetTick(0);
                    }
                    catch (System.Exception e)
                    {
                        Despicable.Core.DebugLogger.WarnExceptionOnce(
                            "AgsPreviewSession.EmptyCatch:10",
                            "AGS preview session best-effort step failed.",
                            e);
                    }
                    ApplyStageAnimations(currentStage, 0);
                    continue;
                }

                int next = currentStage + 1;
                if (next > playEndStage)
                {
                    if (loopSequence)
                    {
                        next = playStartStage;
                    }
                    else
                    {
                        isPlaying = false;
                        hasPlayback = true;
                        break;
                    }
                }

                currentStage = next;
                stageStartTick = schedulerTick;
                stageDurationTicks = ComputeStageDurationTicks(currentStage);
                stageRepeatTarget = ComputeStageRepeatTarget(currentStage);
                stageRepeatCount = 1;
                try
                {
                    WorkshopRenderContext.SetTick(0);
                }
                catch (System.Exception e)
                {
                    Despicable.Core.DebugLogger.WarnExceptionOnce(
                        "AgsPreviewSession.EmptyCatch:11",
                        "AGS preview session best-effort step failed.",
                        e);
                }
                ApplyStageAnimations(currentStage, 0);
            }
        }
    }

}
