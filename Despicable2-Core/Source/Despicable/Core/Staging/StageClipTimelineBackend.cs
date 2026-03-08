using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Despicable.Core.Staging.Backends;
/// <summary>
/// Playback backend for StageClipDef v2 timeline clips.
///
/// - plan.BackendKey is treated as StageClipDef.defName
/// - Slot ids are arbitrary (ex: "initiator", "receiver")
/// - Each timeline stage explicitly maps slotId -> AnimationDef
/// - Loop counts are compiled into CompExtendedAnimator.loopIndex
///
/// This backend does NOT require AnimGroupDef/AnimRoleDef and is intended as the
/// author-facing format that an in-game builder can emit.
/// </summary>
public sealed class StageClipTimelineBackend : IStagePlaybackBackend
{
    public const string BackendId = "stageClipTimeline";

    public bool CanPlay(StagePlan plan)
    {
        if (plan == null) return false;
        if (!plan.PlaybackBackendKey.NullOrEmpty() && plan.PlaybackBackendKey != BackendId) return false;
        if (plan.BackendKey.NullOrEmpty()) return false;

        var clip = DefDatabase<StageClipDef>.GetNamedSilentFail(plan.BackendKey);
        if (clip == null) return false;
        return clip.stages != null && clip.stages.Count > 0;
    }

    public void Play(StagePlan plan)
    {
        if (plan == null) return;
        if (!plan.PlaybackBackendKey.NullOrEmpty() && plan.PlaybackBackendKey != BackendId) return;

        var clip = DefDatabase<StageClipDef>.GetNamedSilentFail(plan.BackendKey);
        if (clip == null) return;
        if (clip.stages == null || clip.stages.Count == 0) return;

        Thing anchorThing = plan.Anchor.Thing;
        if (anchorThing == null && plan.SlotAssignments.Count > 0)
        {
            foreach (var kv in plan.SlotAssignments)
            {
                anchorThing = kv.Value;
                break;
            }
        }

        // Resolve variant overrides (optional).
        var resolvedStages = ResolveStagesWithVariant(clip, plan);
        if (resolvedStages == null || resolvedStages.Count == 0) return;

        // Build per-slot animation queues.
        var perSlotQueue = new Dictionary<string, List<AnimationDef>>(plan.SlotAssignments.Count);
        foreach (var slotId in plan.SlotAssignments.Keys)
            perSlotQueue[slotId] = new List<AnimationDef>(resolvedStages.Count);

        // Loop index is shared across all pawns (stage-aligned).
        var loopIndex = new List<int>(resolvedStages.Count);

        for (int s = 0; s < resolvedStages.Count; s++)
        {
            var stage = resolvedStages[s];
            if (stage == null) continue;

            int loops = stage.loop?.ResolveLoopCount() ?? 1;
            if (loops <= 0) loops = 1;
            loopIndex.Add(loops);

            // Build a quick map for this stage.
            Dictionary<string, AnimationDef> stageMap = null;
            if (stage.tracks != null)
            {
                stageMap = new Dictionary<string, AnimationDef>(stage.tracks.Count);
                for (int t = 0; t < stage.tracks.Count; t++)
                {
                    var tr = stage.tracks[t];
                    if (tr == null || tr.slotId.NullOrEmpty()) continue;
                    stageMap[tr.slotId] = tr.animation;
                }
            }

            foreach (var slotId in perSlotQueue.Keys.ToList())
            {
                if (stageMap == null || !stageMap.TryGetValue(slotId, out var anim) || anim == null)
                {
                    // Missing track for a required slot: fail fast to avoid desync.
                    Log.Warning($"[Despicable] StageClip '{clip.defName}' stage '{stage.stageId ?? s.ToString()}' missing animation for slot '{slotId}'. Clip will not play.");
                    return;
                }

                perSlotQueue[slotId].Add(anim);
            }
        }

        // Create a lightweight AnimGroupDef-like loop container.
        var loopCarrier = new Despicable.AnimGroupDef { loopIndex = loopIndex };

        // Play each slot pawn.
        foreach (var kv in plan.SlotAssignments)
        {
            var slotId = kv.Key;
            var pawn = kv.Value;
            if (pawn == null) continue;

            if (!perSlotQueue.TryGetValue(slotId, out var queue) || queue.NullOrEmpty())
                continue;

            var animator = pawn.TryGetComp<Despicable.CompExtendedAnimator>();
            if (animator == null) continue;

            var slot = clip.slots?.FirstOrDefault(s => s != null && s.slotId == slotId);

            // Prefer conditional offsetDef if present.
            var offsetDef = slot?.offsetDef;

            animator.PlayQueue(loopCarrier, queue, offsetDef, anchorThing);

            // If no offsetDef, fall back to raw offset (if present).
            if (offsetDef == null && slot?.offset != null)
            {
                animator.SetOffset(slot.offset.ToVector3());
            }

            // Fixed facing support (optional). Most content drives facing through animation worker.
            if (slot != null && slot.facing == StageFacingRule.Fixed)
            {
                animator.SetRotation((int)slot.fixedRot);
            }
        }
    }

    private static List<StageTimelineStageDef> ResolveStagesWithVariant(StageClipDef clip, StagePlan plan)
    {
        // Base stages
        var baseStages = clip.stages;
        if (clip.variants == null || clip.variants.Count == 0)
            return baseStages;

        // Compute tags per assigned pawn (contextless; providers may still add useful tags).
        var tagCtx = new StageTagContext(null, null, null);
        var tagsBySlot = new Dictionary<string, HashSet<string>>(plan.SlotAssignments.Count);
        foreach (var kv in plan.SlotAssignments)
        {
            var set = new HashSet<string>();
            StageTagProviders.GetTagsForPawn(kv.Value, tagCtx, set);
            tagsBySlot[kv.Key] = set;
        }

        // Filter variants by requirements.
        var matching = new List<StageVariantDef>();
        for (int i = 0; i < clip.variants.Count; i++)
        {
            var v = clip.variants[i];
            if (v == null) continue;

            if (VariantMatches(v, tagsBySlot))
                matching.Add(v);
        }

        if (matching.Count == 0) return baseStages;

        // Weighted pick.
        float total = 0f;
        for (int i = 0; i < matching.Count; i++)
            total += Math.Max(0f, matching[i].weight);

        StageVariantDef chosen = matching[0];
        if (total > 0f)
        {
            float pick = Rand.Value * total;
            float acc = 0f;
            for (int i = 0; i < matching.Count; i++)
            {
                acc += Math.Max(0f, matching[i].weight);
                if (pick <= acc)
                {
                    chosen = matching[i];
                    break;
                }
            }
        }

        if (chosen?.overrides == null || chosen.overrides.Count == 0)
            return baseStages;

        // Apply overrides by cloning stage objects shallowly.
        var cloned = new List<StageTimelineStageDef>(baseStages.Count);
        for (int i = 0; i < baseStages.Count; i++)
        {
            var s = baseStages[i];
            if (s == null) { cloned.Add(null); continue; }

            cloned.Add(new StageTimelineStageDef
            {
                stageId = s.stageId,
                durationTicks = s.durationTicks,
                loop = s.loop,
                tracks = s.tracks != null ? new List<StageTrackDef>(s.tracks) : null
            });
        }

        for (int o = 0; o < chosen.overrides.Count; o++)
        {
            var ov = chosen.overrides[o];
            if (ov == null || ov.stageId.NullOrEmpty() || ov.tracks == null) continue;

            var stage = cloned.FirstOrDefault(s => s != null && s.stageId == ov.stageId);
            if (stage == null) continue;

            // Replace or add tracks.
            var map = new Dictionary<string, StageTrackDef>();
            if (stage.tracks != null)
            {
                for (int i = 0; i < stage.tracks.Count; i++)
                {
                    var tr = stage.tracks[i];
                    if (tr == null || tr.slotId.NullOrEmpty()) continue;
                    map[tr.slotId] = tr;
                }
            }
            else
            {
                stage.tracks = new List<StageTrackDef>();
            }

            for (int i = 0; i < ov.tracks.Count; i++)
            {
                var tr = ov.tracks[i];
                if (tr == null || tr.slotId.NullOrEmpty()) continue;
                map[tr.slotId] = tr;
            }

            stage.tracks = map.Values.ToList();
        }

        return cloned;
    }

    private static bool VariantMatches(StageVariantDef v, Dictionary<string, HashSet<string>> tagsBySlot)
    {
        var req = v.requirements;
        if (req?.slotTagRules == null || req.slotTagRules.Count == 0) return true;

        for (int i = 0; i < req.slotTagRules.Count; i++)
        {
            var rule = req.slotTagRules[i];
            if (rule == null || rule.slotId.NullOrEmpty()) continue;

            if (!tagsBySlot.TryGetValue(rule.slotId, out var tags))
                return false;

            if (rule.requiredTags != null)
            {
                for (int t = 0; t < rule.requiredTags.Count; t++)
                {
                    var tag = rule.requiredTags[t];
                    if (tag.NullOrEmpty()) continue;
                    if (!tags.Contains(tag)) return false;
                }
            }

            if (rule.forbiddenTags != null)
            {
                for (int t = 0; t < rule.forbiddenTags.Count; t++)
                {
                    var tag = rule.forbiddenTags[t];
                    if (tag.NullOrEmpty()) continue;
                    if (tags.Contains(tag)) return false;
                }
            }
        }

        return true;
    }
}
