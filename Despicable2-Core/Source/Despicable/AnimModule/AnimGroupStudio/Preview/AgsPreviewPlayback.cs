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
    private int ComputeStageRepeatTarget(int stageIndex)
    {
        if (useRuntimeSource)
        {
            if (runtimeStages == null) return 1;
            if (stageIndex < 0 || stageIndex >= runtimeStages.Count) return 1;
            try
            {
                return Mathf.Max(1, runtimeStages[stageIndex]?.RepeatCount ?? 1);
            }
            catch (System.Exception e)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce(
                    "AgsPreviewSession.RepeatCountFallback",
                    "AGS preview session failed to read repeat count; using fallback.",
                    e);
                return 1;
            }
        }

        if (currentGroup?.loopIndex == null) return 1;
        if (stageIndex < 0 || stageIndex >= currentGroup.loopIndex.Count) return 1;

        try
        {
            return Mathf.Max(1, currentGroup.loopIndex[stageIndex]);
        }
        catch (System.Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "AgsPreviewSession.GroupRepeatFallback",
                "AGS preview session failed to read group repeat count; using fallback.",
                e);
            return 1;
        }
    }

    private int ComputeStageCount()
    {
        if (useRuntimeSource)
            return runtimeStages != null ? runtimeStages.Count : 0;

        if (currentGroup?.animRoles == null || currentGroup.animRoles.Count == 0) return 0;

        int max = 0;
        for (int i = 0; i < currentGroup.animRoles.Count; i++)
        {
            var role = currentGroup.animRoles[i];
            if (role?.anims == null) continue;
            max = Mathf.Max(max, role.anims.Count);
        }
        return max;
    }

    private int ComputeStageDurationTicks(int stageIndex)
    {
        if (useRuntimeSource)
        {
            if (runtimeStages == null || stageIndex < 0 || stageIndex >= runtimeStages.Count) return 60;
            try
            {
                return Mathf.Max(1, runtimeStages[stageIndex]?.DurationTicks ?? 60);
            }
            catch (System.Exception e)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce(
                    "AgsPreviewSession.RuntimeDurationFallback",
                    "AGS preview session failed to read runtime duration; using fallback.",
                    e);
                return 60;
            }
        }

        int dur = 0;
        for (int i = 0; i < slots.Count; i++)
        {
            var st = slots[i];
            var anim =
                st?.RoleDef?.anims != null &&
                stageIndex >= 0 &&
                stageIndex < st.RoleDef.anims.Count
                    ? st.RoleDef.anims[stageIndex]
                    : null;
            if (anim == null) continue;
            try
            {
                dur = Mathf.Max(dur, anim.durationTicks);
            }
            catch (System.Exception e)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce(
                    "AgsPreviewSession.EmptyCatch:14",
                    "AGS preview session best-effort step failed.",
                    e);
            }
        }
        return Mathf.Max(1, dur > 0 ? dur : 60);
    }

    private void ApplyStageAnimations(int stageIndex, int tick)
    {
        using (new WorkshopRenderContext.Scope(active: true, tick: tick))
        {
            for (int i = 0; i < slots.Count; i++)
            {
                var st = slots[i];
                if (st?.Pawn == null) continue;
                try
                {
                    st.Pawn.Drawer?.renderer?.SetAnimation(
                        GetAnimationForSlotAtStage(st, stageIndex));
                }
                catch (System.Exception e)
                {
                    Despicable.Core.DebugLogger.WarnExceptionOnce(
                        "AgsPreviewSession.EmptyCatch:15",
                        "AGS preview session best-effort step failed.",
                        e);
                }
            }
        }
    }

}
