using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimGroupStudio;
/// <summary>
/// Minimal compilation helpers for Anim Group Studio.
///
/// v1 scope in this pass:
/// - Enforce group-scoped prop membership by re-scanning keyframes.
/// - Provide a hook point for a future "compile to AnimationDef" pipeline.
///
/// NOTE: Runtime compilation to AnimationDef is intentionally not implemented here yet;
/// existing gameplay AnimationDefs are used for Preview Existing.
/// </summary>
public static class AgsCompile
{
    /// <summary>
    /// Compile an editor-authored clip to an in-memory AnimationDef using the existing
    /// ExtendedKeyframe + AnimationWorker_ExtendedKeyframes pipeline.
    ///
    /// Notes:
    /// - We do NOT register the resulting def in any DefDatabase. It's intended for live preview.
    /// - Offset/angle interpolation is handled by vanilla AnimationWorker_Keyframes.
    /// - Facing/visible/graphicState are step-sampled by AnimationWorker_ExtendedKeyframes.
    /// </summary>
    public static AnimationDef CompileClipToAnimationDef(AgsModel.ClipSpec clip, string defNameHint, int durationTicks)
    {
        if (clip == null) return null;

        durationTicks = Mathf.Max(1, durationTicks);
        clip.lengthTicks = durationTicks;

        var anim = new AnimationDef
        {
            defName = MakeSafeDefName(defNameHint ?? "AGS_Anim"),
            label = defNameHint ?? "AGS_Anim",
            durationTicks = durationTicks
        };

        // AnimationDef.keyframeParts is a dictionary in-memory.
        var parts = new Dictionary<PawnRenderNodeTagDef, KeyframeAnimationPart>();

        if (!clip.tracks.NullOrEmpty())
        {
            for (int i = 0; i < clip.tracks.Count; i++)
            {
                var t = clip.tracks[i];
                if (t == null) continue;

                var tag = ResolveTag(t.nodeTag);
                if (tag == null) continue;

                var part = new KeyframeAnimationPart
                {
                    workerType = typeof(AnimationWorker_ExtendedKeyframes),
                    keyframes = BuildKeyframes(t)
                };

                // Defensive: merge if multiple tracks target the same tag.
                if (parts.TryGetValue(tag, out var existing) && existing != null)
                {
                    if (existing.keyframes == null) existing.keyframes = new List<Verse.Keyframe>();
                    if (part.keyframes != null) existing.keyframes.AddRange(part.keyframes);
                    existing.keyframes.Sort((a, b) => a.tick.CompareTo(b.tick));
                }
                else
                {
                    parts[tag] = part;
                }
            }
        }

        anim.keyframeParts = parts;
        return anim;
    }

    public static void RebuildPropLibrary(AgsModel.Project project)
    {
        if (project == null) return;

        var set = new HashSet<string>();

        if (project.stages != null)
        {
            for (int s = 0; s < project.stages.Count; s++)
            {
                var stage = project.stages[s];
                if (stage?.variants == null) continue;

                for (int v = 0; v < stage.variants.Count; v++)
                {
                    var variant = stage.variants[v];
                    if (variant == null) continue;

                    if (variant.clips != null)
                    {
                        for (int c = 0; c < variant.clips.Count; c++)
                        {
                            var rc = variant.clips[c];
                            if (rc?.clip == null) continue;
                            ScanClip(rc.clip, set);
                        }
                    }
                }
            }
        }

        project.propLibrary = set;
    }

    private static void ScanClip(AgsModel.ClipSpec clip, HashSet<string> set)
    {
        if (clip?.tracks == null) return;

        for (int t = 0; t < clip.tracks.Count; t++)
        {
            var tr = clip.tracks[t];
            if (tr?.keys == null) continue;
            for (int k = 0; k < tr.keys.Count; k++)
            {
                var key = tr.keys[k];
                var prop = key?.prop;
                if (prop == null) continue;
                if (!prop.propDefName.NullOrEmpty())
                    set.Add(prop.propDefName);
            }
        }
    }

    private static List<Verse.Keyframe> BuildKeyframes(AgsModel.Track track)
    {
        var list = new List<Verse.Keyframe>();
        if (track?.keys.NullOrEmpty() != false) return list;

        for (int i = 0; i < track.keys.Count; i++)
        {
            var k = track.keys[i];
            if (k == null) continue;

            var ek = new ExtendedKeyframe
            {
                tick = k.tick,
                angle = k.angle,
                offset = k.offset,
                rotation = k.rotation,
                visible = k.visible,
                graphicState = k.graphicState,
                layerBias = Mathf.Clamp(k.layerBias, -3, 3)
            };

            // Preserve authored scale (prop nodes use this heavily). Defaults to Vector3.one when unset.
            VerseKeyframeCompat.TrySetScale(ek, k.scale);

            // AgsModel.KeySpec.variant uses -1 as "unset" (not nullable).
            if (k.variant != -1)
                ek.variant = k.variant;

            if (!k.soundDefName.NullOrEmpty())
                ek.sound = DefDatabase<SoundDef>.GetNamedSilentFail(k.soundDefName);

            if (!k.facialAnimDefName.NullOrEmpty())
                ek.facialAnim = DefDatabase<FacialAnimDef>.GetNamedSilentFail(k.facialAnimDefName);

            list.Add(ek);
        }

        list.Sort((a, b) => a.tick.CompareTo(b.tick));
        return list;
    }

    private static PawnRenderNodeTagDef ResolveTag(string tagDefName)
    {
        if (tagDefName.NullOrEmpty()) return null;
        return DefDatabase<PawnRenderNodeTagDef>.GetNamedSilentFail(tagDefName);
    }


    private static string MakeSafeDefName(string hint)
    {
        if (hint.NullOrEmpty()) hint = "AGS_Anim";

        var chars = hint.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (char.IsLetterOrDigit(c) || c == '_') continue;
            chars[i] = '_';
        }

        var cleaned = new string(chars);
        if (char.IsDigit(cleaned[0]))
            cleaned = "AGS_" + cleaned;

        int hash = Math.Abs(hint.GetHashCode());
        return $"{cleaned}_{hash:X}";
    }
}
