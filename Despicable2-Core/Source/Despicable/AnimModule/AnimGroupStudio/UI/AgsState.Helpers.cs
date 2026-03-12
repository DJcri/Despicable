using RimWorld;
// Guardrail-Reason: AGS state helpers stay grouped because clip resolution, validation, and authoring mutations operate on one editor state surface.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.AnimGroupStudio;
using Despicable.AnimModule.AnimGroupStudio.Export;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
public partial class Dialog_AnimGroupStudio
{

    private static AgsModel.ClipSpec GetClip(AgsModel.StageSpec stage, string roleKey)
    {
        if (stage == null) return null;
        if (stage.variants == null) stage.variants = new List<AgsModel.StageVariant>();

        var v = stage.variants.FirstOrDefault(x => x != null && x.variantId == "Base");
        if (v == null)
        {
            v = new AgsModel.StageVariant { variantId = "Base", clips = new List<AgsModel.RoleClip>() };
            stage.variants.Add(v);
        }

        if (v.clips == null) v.clips = new List<AgsModel.RoleClip>();
        if (roleKey.NullOrEmpty()) roleKey = "male_1";

        var clip = v.GetClip(roleKey);
        if (clip == null) clip = v.EnsureClip(roleKey);
        return clip;
    }

    private static void EnsureClip(AgsModel.ClipSpec clip, int durationTicks)
    {
        if (clip == null) return;
        clip.lengthTicks = Mathf.Max(1, durationTicks);
        if (clip.tracks == null) clip.tracks = new List<AgsModel.Track>();
    }

    private AgsModel.Track GetSelectedTrack(AgsModel.StageSpec stage, string roleKey)
    {
        var clip = GetClip(stage, roleKey);
        if (clip?.tracks == null || clip.tracks.Count == 0) return null;
        if (authorTrackIndex < 0 || authorTrackIndex >= clip.tracks.Count) return null;
        return clip.tracks[authorTrackIndex];
    }

    private bool IsPropNodeTag(string nodeTag)
    {
        if (nodeTag.NullOrEmpty()) return false;
        if (isPropTagCache.TryGetValue(nodeTag, out bool cached)) return cached;

        bool isProp = false;
        try
        {
            var defs = DefDatabase<AnimationPropDef>.AllDefsListForReading;
            if (!defs.NullOrEmpty())
            {
                for (int i = 0; i < defs.Count; i++)
                {
                    var d = defs[i];
                    var tag = d?.animPropProperties?.tagDef;
                    if (tag != null && tag.defName == nodeTag)
                    {
                        isProp = true;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            isProp = false;
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "AgsState:2",
                "AgsState ignored a non-fatal editor exception.",
                ex);
        }

        isPropTagCache[nodeTag] = isProp;
        return isProp;
    }

    private static void EnsureTrackDefaults(AgsModel.Track tr, int durationTicks)
    {
        if (tr == null) return;
        if (tr.keys == null) tr.keys = new List<AgsModel.Keyframe>();
        if (tr.keys.Count == 0)
        {
            tr.keys.Add(CreateDefaultKeyframe(0));
            tr.keys.Add(CreateDefaultKeyframe(Mathf.Max(1, durationTicks)));
        }
        SortClampKeys(tr, durationTicks);
    }

    private static void SortClampKeys(AgsModel.Track tr, int durationTicks)
    {
        if (tr?.keys == null) return;
        durationTicks = Mathf.Max(1, durationTicks);
        tr.keys.Sort((a, b) => (a?.tick ?? 0).CompareTo(b?.tick ?? 0));
        for (int i = 0; i < tr.keys.Count; i++)
        {
            if (tr.keys[i] == null) continue;
            tr.keys[i].tick = Mathf.Clamp(tr.keys[i].tick, 0, durationTicks);
        }
    }

    private static AgsModel.Keyframe CreateDefaultKeyframe(int tick)
    {
        return new AgsModel.Keyframe
        {
            tick = Mathf.Max(0, tick),
            angle = 0f,
            offset = Vector3.zero,
            scale = Vector3.one,
            rotation = Rot4.South,
            visible = true,
            variant = -1,
            layerBias = 0
        };
    }

    private static AgsModel.PropRef ClonePropRef(AgsModel.PropRef src)
    {
        if (src == null) return null;
        return new AgsModel.PropRef
        {
            propDefName = src.propDefName,
            propVisible = src.propVisible,
            propOffset = src.propOffset,
            propAngle = src.propAngle,
            propScale = src.propScale
        };
    }

    private static AgsModel.Keyframe CloneKeyframe(AgsModel.Keyframe src)
    {
        if (src == null) return null;
        var clone = CreateDefaultKeyframe(src.tick);
        CopyKeyframeData(src, clone, includeTick: true);
        return clone;
    }

    private static void CopyKeyframeData(AgsModel.Keyframe source, AgsModel.Keyframe destination, bool includeTick)
    {
        if (destination == null) return;

        if (source == null)
        {
            int keepTick = destination.tick;
            var defaults = CreateDefaultKeyframe(keepTick);
            destination.angle = defaults.angle;
            destination.offset = defaults.offset;
            destination.scale = defaults.scale;
            destination.rotation = defaults.rotation;
            destination.visible = defaults.visible;
            destination.graphicState = defaults.graphicState;
            destination.variant = defaults.variant;
            destination.soundDefName = defaults.soundDefName;
            destination.facialAnimDefName = defaults.facialAnimDefName;
            destination.layerBias = defaults.layerBias;
            destination.prop = defaults.prop;
            if (includeTick) destination.tick = defaults.tick;
            return;
        }

        if (includeTick) destination.tick = source.tick;
        destination.angle = source.angle;
        destination.offset = source.offset;
        destination.scale = source.scale == default(Vector3) ? Vector3.one : source.scale;
        destination.rotation = source.rotation;
        destination.visible = source.visible;
        destination.graphicState = source.graphicState;
        destination.variant = source.variant;
        destination.soundDefName = source.soundDefName;
        destination.facialAnimDefName = source.facialAnimDefName;
        destination.layerBias = source.layerBias;
        destination.prop = ClonePropRef(source.prop);
    }

    private static AgsModel.Keyframe GetPreviousKeyframe(AgsModel.Track tr, int keyIndex)
    {
        if (tr?.keys == null || tr.keys.Count == 0 || keyIndex <= 0 || keyIndex >= tr.keys.Count)
            return null;
        return tr.keys[keyIndex - 1];
    }

    private static bool TrackHasKeyAtTick(AgsModel.Track tr, int tick, AgsModel.Keyframe except = null)
    {
        if (tr?.keys == null) return false;
        for (int i = 0; i < tr.keys.Count; i++)
        {
            var key = tr.keys[i];
            if (key == null || ReferenceEquals(key, except)) continue;
            if (key.tick == tick) return true;
        }
        return false;
    }

    private static int FindNextFreeTick(AgsModel.Track tr, int startTick, int stageDurationTicks, AgsModel.Keyframe except = null)
    {
        int maxTick = Mathf.Max(1, stageDurationTicks);
        int tick = Mathf.Clamp(startTick, 0, maxTick);
        while (tick <= maxTick)
        {
            if (!TrackHasKeyAtTick(tr, tick, except))
                return tick;
            tick++;
        }
        return -1;
    }

    private static int ResolveNewKeyframeTick(AgsModel.Track tr, AgsModel.StageSpec stage, AgsModel.Keyframe seed)
    {
        int stageDuration = Mathf.Max(1, stage?.durationTicks ?? 1);
        int desiredTick = Mathf.Clamp((seed?.tick ?? -5) + 5, 0, stageDuration);
        int freeTick = FindNextFreeTick(tr, desiredTick, stageDuration);
        if (freeTick >= 0)
            return freeTick;

        int extendedTick = Mathf.Max(stageDuration + 1, (seed?.tick ?? stageDuration) + 5);
        if (stage != null)
            stage.durationTicks = Mathf.Max(stage.durationTicks, extendedTick);
        return extendedTick;
    }

    private static bool TrySetUniqueKeyframeTick(AgsModel.Track tr, AgsModel.Keyframe key, int desiredTick, AgsModel.StageSpec stage, out string failureReason)
    {
        failureReason = null;
        if (tr == null || key == null)
        {
            failureReason = "No keyframe is selected.";
            return false;
        }

        int normalized = Mathf.Max(0, desiredTick);
        if (TrackHasKeyAtTick(tr, normalized, key))
        {
            failureReason = "That track already has a keyframe at tick " + normalized + ".";
            return false;
        }

        if (stage != null)
            stage.durationTicks = Mathf.Max(Mathf.Max(1, stage.durationTicks), normalized);

        key.tick = normalized;
        SortClampKeys(tr, Mathf.Max(1, stage?.durationTicks ?? normalized));
        if (stage != null)
            RecalculateStageDurationFromKeys(stage);
        return true;
    }

    private static void ResetKeyframeFromPreviousOrDefault(AgsModel.Track tr, int keyIndex)
    {
        if (tr?.keys == null || keyIndex < 0 || keyIndex >= tr.keys.Count) return;
        var key = tr.keys[keyIndex];
        if (key == null) return;

        int keepTick = key.tick;
        CopyKeyframeData(GetPreviousKeyframe(tr, keyIndex), key, includeTick: false);
        key.tick = keepTick;
    }

    private static string GetDefNameValidationHint(string value)
    {
        if (value.NullOrEmpty()) return null;
        string safe = AgsExportUtil.MakeSafeDefName(value);
        return AgsExportUtil.IsExactDefName(value) ? null : "Export will normalize this def prefix to: " + safe;
    }

    private static string GetVariationLabelValidationHint(string value)
    {
        if (value.NullOrEmpty()) return null;
        string safe = AgsExportUtil.MakeVariationCode(value);
        return safe == value.Trim() ? null : "Export will normalize this variation label to: " + safe;
    }

    private static string GetResolvedProjectVariationDefName(AgsModel.Project project)
    {
        string baseDef = project?.export?.baseDefName;
        if (baseDef.NullOrEmpty())
            baseDef = project?.projectId ?? "AGS_Export";
        return AgsExportUtil.MakeGroupDefName(baseDef, project?.label ?? "");
    }

    private static string GetProjectDisplayName(AgsModel.Project project)
    {
        if (project == null)
            return "(select)";

        if (project?.export?.baseDefName.NullOrEmpty() == false || !project.label.NullOrEmpty())
            return GetResolvedProjectVariationDefName(project);

        return project.projectId ?? "(unnamed)";
    }

    private static AgsModel.Keyframe FindKeyframeAtTick(AgsModel.Track tr, int tick)
    {
        if (tr?.keys == null) return null;
        for (int i = 0; i < tr.keys.Count; i++)
        {
            var key = tr.keys[i];
            if (key != null && key.tick == tick)
                return key;
        }
        return null;
    }

    private static AgsModel.Keyframe SampleTrackKeyframeAtTick(AgsModel.Track tr, int tick)
    {
        if (tr?.keys == null || tr.keys.Count == 0)
            return CreateDefaultKeyframe(tick);

        SortClampKeys(tr, int.MaxValue);

        AgsModel.Keyframe prev = null;
        AgsModel.Keyframe next = null;
        for (int i = 0; i < tr.keys.Count; i++)
        {
            var key = tr.keys[i];
            if (key == null) continue;
            if (key.tick <= tick)
                prev = key;
            if (key.tick >= tick)
            {
                next = key;
                break;
            }
        }

        if (prev == null) prev = next;
        if (next == null) next = prev;

        var sampled = CloneKeyframe(prev ?? next) ?? CreateDefaultKeyframe(tick);
        sampled.tick = Mathf.Max(0, tick);

        if (prev != null && next != null && !ReferenceEquals(prev, next) && next.tick > prev.tick)
        {
            float t = Mathf.InverseLerp(prev.tick, next.tick, tick);
            // AGS author angles are stored as cumulative degrees, not wrapped facings.
            sampled.angle = Mathf.Lerp(prev.angle, next.angle, t);
            sampled.offset = Vector3.Lerp(prev.offset, next.offset, t);
            Vector3 prevScale = prev.scale == default(Vector3) ? Vector3.one : prev.scale;
            Vector3 nextScale = next.scale == default(Vector3) ? Vector3.one : next.scale;
            sampled.scale = Vector3.Lerp(prevScale, nextScale, t);
        }

        return sampled;
    }

    private static int GetHighestAuthoredTick(AgsModel.StageSpec stage, int fallback = 1)
    {
        int highest = fallback;
        if (stage?.variants == null)
            return Mathf.Max(1, highest);

        for (int vi = 0; vi < stage.variants.Count; vi++)
        {
            var variant = stage.variants[vi];
            if (variant == null)
                continue;

            if (!variant.clips.NullOrEmpty())
            {
                for (int ci = 0; ci < variant.clips.Count; ci++)
                {
                    var roleClip = variant.clips[ci];
                    var clip = roleClip?.clip;
                    if (clip?.tracks == null)
                        continue;

                    for (int ti = 0; ti < clip.tracks.Count; ti++)
                    {
                        var track = clip.tracks[ti];
                        if (track?.keys == null)
                            continue;

                        for (int ki = 0; ki < track.keys.Count; ki++)
                        {
                            var key = track.keys[ki];
                            if (key != null)
                                highest = Mathf.Max(highest, key.tick);
                        }
                    }
                }
            }
            else
            {
                var legacyClips = new[] { variant.male, variant.female };
                for (int l = 0; l < legacyClips.Length; l++)
                {
                    var clip = legacyClips[l];
                    if (clip?.tracks == null)
                        continue;
                    for (int ti = 0; ti < clip.tracks.Count; ti++)
                    {
                        var track = clip.tracks[ti];
                        if (track?.keys == null)
                            continue;
                        for (int ki = 0; ki < track.keys.Count; ki++)
                        {
                            var key = track.keys[ki];
                            if (key != null)
                                highest = Mathf.Max(highest, key.tick);
                        }
                    }
                }
            }
        }

        return Mathf.Max(1, highest);
    }

    private static bool RecalculateStageDurationFromKeys(AgsModel.StageSpec stage, int fallback = 1)
    {
        if (stage == null)
            return false;

        int newDuration = GetHighestAuthoredTick(stage, fallback);
        bool changed = stage.durationTicks != newDuration;
        stage.durationTicks = newDuration;

        if (stage.variants != null)
        {
            for (int vi = 0; vi < stage.variants.Count; vi++)
            {
                var variant = stage.variants[vi];
                if (variant == null)
                    continue;

                if (!variant.clips.NullOrEmpty())
                {
                    for (int ci = 0; ci < variant.clips.Count; ci++)
                    {
                        var clip = variant.clips[ci]?.clip;
                        if (clip != null)
                            clip.lengthTicks = newDuration;
                    }
                }
                else
                {
                    if (variant.male != null) variant.male.lengthTicks = newDuration;
                    if (variant.female != null) variant.female.lengthTicks = newDuration;
                }
            }
        }

        return changed;
    }

    private static AgsModel.ClipSpec CloneClipSeededFromTerminalKeys(AgsModel.ClipSpec clip, int durationTicks)
    {
        var seeded = new AgsModel.ClipSpec
        {
            lengthTicks = Mathf.Max(1, durationTicks),
            tracks = new List<AgsModel.Track>()
        };

        if (clip?.tracks.NullOrEmpty() != false)
            return seeded;

        for (int ti = 0; ti < clip.tracks.Count; ti++)
        {
            var srcTrack = clip.tracks[ti];
            if (srcTrack == null)
                continue;

            var dstTrack = new AgsModel.Track
            {
                nodeTag = srcTrack.nodeTag,
                keys = new List<AgsModel.Keyframe>()
            };

            AgsModel.Keyframe terminal = null;
            if (!srcTrack.keys.NullOrEmpty())
            {
                for (int ki = 0; ki < srcTrack.keys.Count; ki++)
                {
                    var key = srcTrack.keys[ki];
                    if (key == null)
                        continue;
                    if (terminal == null || key.tick >= terminal.tick)
                        terminal = key;
                }
            }

            var seed = CloneKeyframe(terminal) ?? CreateDefaultKeyframe(0);
            seed.tick = 0;
            dstTrack.keys.Add(seed);
            seeded.tracks.Add(dstTrack);
        }

        return seeded;
    }

    private static AgsModel.StageSpec CloneStageSeededFromTerminalKeys(AgsModel.StageSpec src)
    {
        if (src == null)
        {
            return new AgsModel.StageSpec
            {
                durationTicks = 60,
                repeatCount = 1,
                variants = new List<AgsModel.StageVariant>()
            };
        }

        var dst = new AgsModel.StageSpec
        {
            stageIndex = src.stageIndex,
            label = src.label,
            durationTicks = Mathf.Max(1, src.durationTicks),
            repeatCount = Mathf.Max(1, src.repeatCount),
            loop = src.loop,
            stageTags = src.stageTags != null ? new List<string>(src.stageTags) : null,
            variants = new List<AgsModel.StageVariant>()
        };

        if (!src.variants.NullOrEmpty())
        {
            foreach (var variant in src.variants)
            {
                if (variant == null) continue;
                var newVariant = new AgsModel.StageVariant { variantId = variant.variantId, clips = new List<AgsModel.RoleClip>() };

                if (!variant.clips.NullOrEmpty())
                {
                    foreach (var roleClip in variant.clips)
                    {
                        if (roleClip == null || roleClip.roleKey.NullOrEmpty()) continue;
                        newVariant.clips.Add(new AgsModel.RoleClip
                        {
                            roleKey = roleClip.roleKey,
                            clip = CloneClipSeededFromTerminalKeys(roleClip.clip, dst.durationTicks)
                        });
                    }
                }
                else
                {
                    if (variant.male != null) newVariant.clips.Add(new AgsModel.RoleClip { roleKey = "male_1", clip = CloneClipSeededFromTerminalKeys(variant.male, dst.durationTicks) });
                    if (variant.female != null) newVariant.clips.Add(new AgsModel.RoleClip { roleKey = "female_1", clip = CloneClipSeededFromTerminalKeys(variant.female, dst.durationTicks) });
                }

                dst.variants.Add(newVariant);
            }
        }

        return dst;
    }

    private static AgsModel.StageSpec DeepCloneStage(AgsModel.StageSpec src)
    {
        var dst = new AgsModel.StageSpec
        {
            stageIndex = src.stageIndex,
            label = src.label,
            durationTicks = src.durationTicks,
            repeatCount = src.repeatCount,
            loop = src.loop,
            stageTags = src.stageTags != null ? new List<string>(src.stageTags) : null,
            variants = new List<AgsModel.StageVariant>()
        };

        if (!src.variants.NullOrEmpty())
        {
            foreach (var v in src.variants)
            {
                if (v == null) continue;
                var nv = new AgsModel.StageVariant { variantId = v.variantId, clips = new List<AgsModel.RoleClip>() };

                if (!v.clips.NullOrEmpty())
                {
                    foreach (var rc in v.clips)
                    {
                        if (rc == null || rc.roleKey.NullOrEmpty()) continue;
                        nv.clips.Add(new AgsModel.RoleClip { roleKey = rc.roleKey, clip = CloneClip(rc.clip) });
                    }
                }
                else
                {
                    if (v.male != null) nv.clips.Add(new AgsModel.RoleClip { roleKey = "male_1", clip = CloneClip(v.male) });
                    if (v.female != null) nv.clips.Add(new AgsModel.RoleClip { roleKey = "female_1", clip = CloneClip(v.female) });
                }

                dst.variants.Add(nv);
            }
        }

        return dst;
    }

    private static AgsModel.ClipSpec CloneClip(AgsModel.ClipSpec c)
    {
        if (c == null) return new AgsModel.ClipSpec();
        var nc = new AgsModel.ClipSpec { lengthTicks = c.lengthTicks, tracks = new List<AgsModel.Track>() };
        if (!c.tracks.NullOrEmpty())
        {
            foreach (var t in c.tracks)
            {
                if (t == null) continue;
                var nt = new AgsModel.Track { nodeTag = t.nodeTag, keys = new List<AgsModel.Keyframe>() };
                if (!t.keys.NullOrEmpty())
                {
                    foreach (var k in t.keys)
                    {
                        if (k == null) continue;
                        nt.keys.Add(CloneKeyframe(k));
                    }
                }
                nc.tracks.Add(nt);
            }
        }
        return nc;
    }
}
