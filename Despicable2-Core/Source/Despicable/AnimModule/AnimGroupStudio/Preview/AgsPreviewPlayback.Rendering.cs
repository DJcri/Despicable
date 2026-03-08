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
    private static Rot4? SampleRootRotation(AnimationDef anim, int tick)
    {
        if (anim?.keyframeParts == null) return null;
        if (tick < 0) tick = 0;

        PawnRenderNodeTagDef rootTag = null;
        try { rootTag = DefDatabase<PawnRenderNodeTagDef>.GetNamedSilentFail("Root"); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewSession.EmptyCatch:16", "AGS preview session best-effort step failed.", e); }
        if (rootTag == null) return null;

        if (!anim.keyframeParts.TryGetValue(rootTag, out var part) || part?.keyframes == null || part.keyframes.Count == 0)
            return null;

        Verse.Keyframe prev = part.keyframes[0];
        Verse.Keyframe next = prev;

        for (int i = 0; i < part.keyframes.Count; i++)
        {
            var k = part.keyframes[i];
            if (k == null) continue;
            if (k.tick <= tick) prev = k;
            if (k.tick >= tick) { next = k; break; }
        }

        Rot4 rPrev = (prev as ExtendedKeyframe)?.rotation ?? Rot4.South;
        Rot4 rNext = (next as ExtendedKeyframe)?.rotation ?? Rot4.South;

        if (prev.tick == next.tick) return rPrev;

        float t = 0f;
        try { t = Mathf.InverseLerp(prev.tick, next.tick, tick); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewSession.EmptyCatch:17", "AGS preview session best-effort step failed.", e); }
        return t < 0.5f ? rPrev : rNext;
    }

    private static void ApplyPreviewFacial(Pawn pawn, AnimationDef anim, int tick)
    {
        var faceParts = pawn?.TryGetComp<CompFaceParts>();
        if (faceParts == null)
            return;

        if (!TryGetRootFacialCue(anim, tick, out FacialAnimDef facialAnim, out int triggerTick) || facialAnim == null)
        {
            faceParts.ClearPreviewFacialOverride();
            return;
        }

        int localTick = Mathf.Max(0, tick - triggerTick);
        if (localTick > facialAnim.durationTicks)
        {
            faceParts.ClearPreviewFacialOverride();
            return;
        }

        faceParts.ApplyPreviewFacialAt(facialAnim, localTick);
    }

    private static bool TryGetRootFacialCue(AnimationDef anim, int tick, out FacialAnimDef facialAnim, out int triggerTick)
    {
        facialAnim = null;
        triggerTick = 0;

        if (anim?.keyframeParts == null)
            return false;

        PawnRenderNodeTagDef rootTag = null;
        try { rootTag = DefDatabase<PawnRenderNodeTagDef>.GetNamedSilentFail("Root"); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsPreviewSession.EmptyCatch:18", "AGS preview session best-effort step failed.", e); }
        if (rootTag == null)
            return false;

        if (!anim.keyframeParts.TryGetValue(rootTag, out var part) || part?.keyframes == null || part.keyframes.Count == 0)
            return false;

        for (int i = 0; i < part.keyframes.Count; i++)
        {
            var ek = part.keyframes[i] as ExtendedKeyframe;
            if (ek == null || ek.tick > tick || ek.facialAnim == null)
                continue;

            facialAnim = ek.facialAnim;
            triggerTick = ek.tick;
        }

        return facialAnim != null;
    }

    private static AnimationDef GetAnimationForSlotAtStage(SlotState st, int stageIndex)
    {
        if (st == null) return null;

        if (st.RuntimeAnimationsByStage != null)
        {
            if (stageIndex >= 0 && stageIndex < st.RuntimeAnimationsByStage.Count)
                return st.RuntimeAnimationsByStage[stageIndex];
            return null;
        }

        if (st.RoleDef?.anims != null && stageIndex >= 0 && stageIndex < st.RoleDef.anims.Count)
            return st.RoleDef.anims[stageIndex];

        return null;
    }
}
