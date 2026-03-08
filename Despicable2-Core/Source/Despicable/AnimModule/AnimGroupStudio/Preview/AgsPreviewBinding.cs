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
    public void ConfigureFor(AnimGroupDef group)
    {
        Stop();
        currentGroup = group;
        runtimeSourceName = null;
        runtimeStages = null;
        useRuntimeSource = false;
        RebuildRolePawns();
        stageCount = ComputeStageCount();
        selectedStageIndex = 0;
        currentStage = 0;
        try { WorkshopRenderContext.SetTick(0); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewSession.EmptyCatch:2", "AGS preview session best-effort step failed.", e); }
    }

    public void ConfigureForRuntime(string sourceName, List<RuntimeRole> roles, List<RuntimeStage> stages)
    {
        Stop();
        currentGroup = null;
        runtimeSourceName = sourceName ?? "Preview";
        runtimeStages = stages != null ? new List<RuntimeStage>(stages) : new List<RuntimeStage>();
        useRuntimeSource = true;
        RebuildRuntimeRolePawns(roles ?? new List<RuntimeRole>());
        stageCount = ComputeStageCount();
        selectedStageIndex = 0;
        currentStage = 0;
        try { WorkshopRenderContext.SetTick(0); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewSession.EmptyCatch:3", "AGS preview session best-effort step failed.", e); }
    }

    public void Seek(int tick)
    {
        tick = Mathf.Max(0, tick);
        schedulerTick = tick;
        stageStartTick = 0;
        try { WorkshopRenderContext.SetTick(tick); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewSession.EmptyCatch:4", "AGS preview session best-effort step failed.", e); }
    }

    public void ShowSelectedStageAtTick(int tick)
    {
        if (!HasBoundSource() || stageCount <= 0 || slots.Count == 0) return;

        currentStage = Mathf.Clamp(selectedStageIndex, 0, stageCount - 1);
        stageStartTick = 0;
        schedulerTick = Mathf.Max(0, tick);
        stageDurationTicks = ComputeStageDurationTicks(currentStage);
        stageRepeatTarget = ComputeStageRepeatTarget(currentStage);
        stageRepeatCount = 1;

        int clampedTick = Mathf.Clamp(schedulerTick, 0, Mathf.Max(0, stageDurationTicks));
        try { WorkshopRenderContext.SetTick(clampedTick); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewSession.EmptyCatch:5", "AGS preview session best-effort step failed.", e); }
        ApplyStageAnimations(currentStage, clampedTick);
    }

    private bool HasBoundSource()
    {
        if (useRuntimeSource)
            return runtimeStages != null;
        return currentGroup != null;
    }

    private void ResetSlots()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            try { slots[i].Renderer?.Dispose(); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewSession.EmptyCatch:13", "AGS preview session best-effort step failed.", e); }
        }
        slots.Clear();
    }

    public void RebuildRolePawns()
    {
        ResetSlots();

        if (currentGroup?.animRoles == null) return;

        var aliveKeys = new HashSet<string>();

        for (int r = 0; r < currentGroup.animRoles.Count; r++)
        {
            var role = currentGroup.animRoles[r];
            if (role == null) continue;

            string key = role.defName ?? ("Role" + r);
            aliveKeys.Add(key);

            int g = role.gender;
            int gEffective = g == 0 ? 1 : g;
            string defaultBt = gEffective == 2 ? "Female" : "Male";

            var pawn = pawnPool.GetOrCreate(key, gEffective, defaultBt);
            if (pawn == null) continue;

            slots.Add(new SlotState
            {
                RoleIndex = r,
                Key = key,
                Label = role.defName ?? ("Role" + r),
                Pawn = pawn,
                Renderer = new WorkshopPreviewRenderer(380, 520),
                RoleDef = role,
                RuntimeAnimationsByStage = null,
                Queue = null
            });
        }

        pawnPool.ClearUnused(aliveKeys);
    }

    private void RebuildRuntimeRolePawns(List<RuntimeRole> roles)
    {
        ResetSlots();
        if (roles == null) roles = new List<RuntimeRole>();

        var aliveKeys = new HashSet<string>();
        for (int i = 0; i < roles.Count; i++)
        {
            var role = roles[i];
            if (role == null) continue;

            string key = role.Key.NullOrEmpty() ? ("Role" + i) : role.Key;
            aliveKeys.Add(key);

            int g = role.Gender;
            int gEffective = g == 0 ? 1 : g;
            string defaultBt = !role.BodyTypeDefName.NullOrEmpty() ? role.BodyTypeDefName : (gEffective == 2 ? "Female" : "Male");

            var pawn = pawnPool.GetOrCreate(key, gEffective, defaultBt);
            if (pawn == null) continue;

            var anims = new List<AnimationDef>();
            if (runtimeStages != null)
            {
                for (int s = 0; s < runtimeStages.Count; s++)
                {
                    var stage = runtimeStages[s];
                    AnimationDef anim = null;
                    if (stage?.AnimationsByRole != null && i >= 0 && i < stage.AnimationsByRole.Count)
                        anim = stage.AnimationsByRole[i];
                    anims.Add(anim);
                }
            }

            slots.Add(new SlotState
            {
                RoleIndex = i,
                Key = key,
                Label = role.Label ?? key,
                Pawn = pawn,
                Renderer = new WorkshopPreviewRenderer(380, 520),
                RoleDef = null,
                RuntimeAnimationsByStage = anims,
                Queue = null
            });
        }

        pawnPool.ClearUnused(aliveKeys);
    }
}
