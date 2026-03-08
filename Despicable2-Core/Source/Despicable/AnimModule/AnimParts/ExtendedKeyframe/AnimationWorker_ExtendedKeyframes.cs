using Verse;

namespace Despicable;
public class AnimationWorker_ExtendedKeyframes : AnimationWorker_Keyframes
{
    public override bool Enabled(AnimationDef def, PawnRenderNode node, AnimationPart part, PawnDrawParms parms) => true;

    public FacialAnimDef FacialAnimAtTick(int tick, AnimationPart part)
    {
        if (!(part is KeyframeAnimationPart kap) || kap.keyframes.NullOrEmpty()) return null;
        for (int i = 0; i < kap.keyframes.Count; i++)
        {
            if (tick == kap.keyframes[i].tick)
                return (kap.keyframes[i] as ExtendedKeyframe)?.facialAnim;
        }
        return null;
    }

    public SoundDef SoundAtTick(int tick, AnimationPart part, Pawn pawn)
    {
        if (pawn?.TryGetComp<CompExtendedAnimator>()?.hasAnimPlaying != true) return null;
        if (!(part is KeyframeAnimationPart kap) || kap.keyframes.NullOrEmpty()) return null;
        for (int i = 0; i < kap.keyframes.Count; i++)
        {
            if (tick == kap.keyframes[i].tick)
                return (kap.keyframes[i] as ExtendedKeyframe)?.sound;
        }
        return null;
    }

    public Rot4 FacingAtTick(int tick, AnimationPart part)
    {
        if (!(part is KeyframeAnimationPart kap) || kap.keyframes.NullOrEmpty()) return Rot4.South;

        var first = kap.keyframes[0] as ExtendedKeyframe;
        var last = kap.keyframes[kap.keyframes.Count - 1] as ExtendedKeyframe;

        if (tick <= kap.keyframes[0].tick) return first?.rotation ?? Rot4.South;
        if (tick >= kap.keyframes[kap.keyframes.Count - 1].tick) return last?.rotation ?? Rot4.South;

        Verse.Keyframe prev = kap.keyframes[0];
        for (int i = 1; i < kap.keyframes.Count; i++)
        {
            var cur = kap.keyframes[i];
            if (tick < cur.tick) break;
            prev = cur;
        }
        return (prev as ExtendedKeyframe)?.rotation ?? Rot4.South;
    }

    public bool VisibleAtTick(int tick, AnimationPart part)
    {
        if (!(part is KeyframeAnimationPart kap) || kap.keyframes.NullOrEmpty()) return false;

        var first = kap.keyframes[0] as ExtendedKeyframe;
        var last = kap.keyframes[kap.keyframes.Count - 1] as ExtendedKeyframe;

        if (tick <= kap.keyframes[0].tick) return first?.visible ?? false;
        if (tick >= kap.keyframes[kap.keyframes.Count - 1].tick) return last?.visible ?? false;

        Verse.Keyframe prev = kap.keyframes[0];
        for (int i = 1; i < kap.keyframes.Count; i++)
        {
            var cur = kap.keyframes[i];
            if (tick < cur.tick) break;
            prev = cur;
        }
        return (prev as ExtendedKeyframe)?.visible ?? false;
    }

    public string GraphicStateAtTick(int tick, AnimationPart part)
    {
        if (!(part is KeyframeAnimationPart kap) || kap.keyframes.NullOrEmpty()) return null;

        ExtendedKeyframe kf;
        if (tick <= kap.keyframes[0].tick) kf = kap.keyframes[0] as ExtendedKeyframe;
        else if (tick >= kap.keyframes[kap.keyframes.Count - 1].tick) kf = kap.keyframes[kap.keyframes.Count - 1] as ExtendedKeyframe;
        else
        {
            Verse.Keyframe prev = kap.keyframes[0];
            for (int i = 1; i < kap.keyframes.Count; i++)
            {
                var cur = kap.keyframes[i];
                if (tick < cur.tick) break;
                prev = cur;
            }
            kf = prev as ExtendedKeyframe;
        }

        if (kf == null) return null;
        if (!kf.graphicState.NullOrEmpty()) return kf.graphicState;
        if (kf.variant != null) return "variant_" + kf.variant.Value;
        return null;
    }

    public int? VariantTexPathOnTick(int tick, AnimationPart part)
    {
        if (!(part is KeyframeAnimationPart kap) || kap.keyframes.NullOrEmpty()) return null;

        if (tick <= kap.keyframes[0].tick) return (kap.keyframes[0] as ExtendedKeyframe)?.variant;
        if (tick >= kap.keyframes[kap.keyframes.Count - 1].tick) return (kap.keyframes[kap.keyframes.Count - 1] as ExtendedKeyframe)?.variant;

        Verse.Keyframe prev = kap.keyframes[0];
        for (int i = 1; i < kap.keyframes.Count; i++)
        {
            var cur = kap.keyframes[i];
            if (tick < cur.tick) break;
            prev = cur;
        }
        return (prev as ExtendedKeyframe)?.variant;
    }

    public int LayerBiasAtTick(int tick, AnimationPart part)
    {
        if (!(part is KeyframeAnimationPart kap) || kap.keyframes.NullOrEmpty()) return 0;

        if (tick <= kap.keyframes[0].tick) return (kap.keyframes[0] as ExtendedKeyframe)?.layerBias ?? 0;
        if (tick >= kap.keyframes[kap.keyframes.Count - 1].tick) return (kap.keyframes[kap.keyframes.Count - 1] as ExtendedKeyframe)?.layerBias ?? 0;

        Verse.Keyframe prev = kap.keyframes[0];
        for (int i = 1; i < kap.keyframes.Count; i++)
        {
            var cur = kap.keyframes[i];
            if (tick < cur.tick) break;
            prev = cur;
        }
        return (prev as ExtendedKeyframe)?.layerBias ?? 0;
    }

    public virtual bool ShouldRecache(int tick, AnimationPart part)
    {
        if (tick <= 0) return true;
        return FacingAtTick(tick, part) != FacingAtTick(tick - 1, part)
            || VisibleAtTick(tick, part) != VisibleAtTick(tick - 1, part)
            || GraphicStateAtTick(tick, part) != GraphicStateAtTick(tick - 1, part)
            || VariantTexPathOnTick(tick, part) != VariantTexPathOnTick(tick - 1, part)
            || LayerBiasAtTick(tick, part) != LayerBiasAtTick(tick - 1, part);
    }
}
