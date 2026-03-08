using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.AnimGroupStudio;
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
            tr.keys.Add(new AgsModel.Keyframe { tick = 0, rotation = Rot4.South, visible = true });
            tr.keys.Add(new AgsModel.Keyframe { tick = Mathf.Max(1, durationTicks), rotation = Rot4.South, visible = true });
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

    private static AgsModel.StageSpec DeepCloneStage(AgsModel.StageSpec src)
    {
        // Minimal deep clone without relying on Scribe.
        var dst = new AgsModel.StageSpec
        {
            stageIndex = src.stageIndex,
            label = src.label,
            durationTicks = src.durationTicks,
            repeatCount = src.repeatCount,
            loop = src.loop,
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
                    // Legacy fallback
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
                        nt.keys.Add(new AgsModel.Keyframe
                        {
                            tick = k.tick,
                            angle = k.angle,
                            offset = k.offset,
                            rotation = k.rotation,
                            visible = k.visible,
                            graphicState = k.graphicState,
                            variant = k.variant,
                            soundDefName = k.soundDefName,
                            facialAnimDefName = k.facialAnimDefName,
                            layerBias = k.layerBias,
                            prop = k.prop // shallow ok for now
                        });
                    }
                }
                nc.tracks.Add(nt);
            }
        }
        return nc;
            }
}
