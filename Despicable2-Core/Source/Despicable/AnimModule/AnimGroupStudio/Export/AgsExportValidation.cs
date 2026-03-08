using RimWorld;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimModule.AnimGroupStudio.Export;
public sealed partial class AgsExport
{
    private static void ValidateInto(AgsModel.Project project, ValidationResult vr)
    {
        if (project == null)
        {
            vr.errors.Add("No project loaded.");
            return;
        }

        if (project.export == null) project.export = new AgsModel.ExportSpec();
        if (project.export.baseDefName.NullOrEmpty())
            vr.errors.Add("Base defName is empty. Set a Base def value in the left panel.");

        if (project.roles.NullOrEmpty())
            vr.errors.Add("Project has no roles.");

        if (project.stages.NullOrEmpty())
            vr.errors.Add("Project has no stages.");

        if (!vr.Ok) return;

        var variantIds = CollectVariantIds(project);
        if (variantIds.Count == 0)
            vr.errors.Add("No variants found. Each stage must contain at least one variant (e.g. 'Base').");

        for (int si = 0; si < project.stages.Count; si++)
        {
            var s = project.stages[si];
            if (s == null)
            {
                vr.errors.Add($"Stage {si} is null.");
                continue;
            }

            if (s.durationTicks < 1)
                vr.errors.Add($"Stage {si} durationTicks must be >= 1.");
            if (s.repeatCount < 1)
                vr.errors.Add($"Stage {si} repeatCount must be >= 1.");
            if (s.variants.NullOrEmpty())
            {
                vr.errors.Add($"Stage {si} has no variants.");
                continue;
            }

            // Each stage must contain all variants.
            for (int vi = 0; vi < variantIds.Count; vi++)
            {
                string vid = variantIds[vi];
                var v = s.variants.FirstOrDefault(x => x != null && (x.variantId.NullOrEmpty() ? "Base" : x.variantId) == vid);
                if (v == null)
                {
                    vr.errors.Add($"Stage {si} is missing variant '{vid}'.");
                    continue;
                }

                for (int ri = 0; ri < project.roles.Count; ri++)
                {
                    var role = project.roles[ri];
                    if (role == null) continue;
                    if (role.roleKey.NullOrEmpty())
                    {
                        vr.errors.Add($"Role {ri} is missing roleKey.");
                        continue;
                    }

                    var clip = v.GetClip(role.roleKey);
                    if (clip == null)
                    {
                        vr.errors.Add($"Stage {si} variant '{vid}' is missing clip for role '{role.roleKey}'.");
                        continue;
                    }

                    ValidateClip(vr, clip, s.durationTicks, $"Stage {si} '{s.label ?? "(unnamed)"}' variant '{vid}' role '{role.roleKey}'");
                }
            }
        }

        // Validate referenced defs.
        for (int si = 0; si < project.stages.Count; si++)
        {
            var s = project.stages[si];
            if (s?.variants == null) continue;
            for (int vi = 0; vi < s.variants.Count; vi++)
            {
                var v = s.variants[vi];
                if (v?.clips == null) continue;
                for (int ci = 0; ci < v.clips.Count; ci++)
                {
                    var rc = v.clips[ci];
                    if (rc?.clip?.tracks == null) continue;
                    for (int ti = 0; ti < rc.clip.tracks.Count; ti++)
                    {
                        var tr = rc.clip.tracks[ti];
                        if (tr == null) continue;

                        if (!tr.nodeTag.NullOrEmpty() && DefDatabase<PawnRenderNodeTagDef>.GetNamedSilentFail(tr.nodeTag) == null)
                            vr.errors.Add($"Missing PawnRenderNodeTagDef '{tr.nodeTag}' (stage {si} variant '{v.variantId}' role '{rc.roleKey}').");

                        if (tr.keys == null) continue;
                        for (int ki = 0; ki < tr.keys.Count; ki++)
                        {
                            var k = tr.keys[ki];
                            if (k == null) continue;
                            if (!k.soundDefName.NullOrEmpty() && DefDatabase<SoundDef>.GetNamedSilentFail(k.soundDefName) == null)
                                vr.errors.Add($"Missing SoundDef '{k.soundDefName}' (stage {si} variant '{v.variantId}' role '{rc.roleKey}', tick {k.tick}).");
                            if (!k.facialAnimDefName.NullOrEmpty() && DefDatabase<FacialAnimDef>.GetNamedSilentFail(k.facialAnimDefName) == null)
                                vr.errors.Add($"Missing FacialAnimDef '{k.facialAnimDefName}' (stage {si} variant '{v.variantId}' role '{rc.roleKey}', tick {k.tick}).");
                            if (k.prop != null && !k.prop.propDefName.NullOrEmpty() && DefDatabase<ThingDef>.GetNamedSilentFail(k.prop.propDefName) == null)
                                vr.errors.Add($"Missing ThingDef for prop '{k.prop.propDefName}' (stage {si} variant '{v.variantId}' role '{rc.roleKey}', tick {k.tick}).");
                        }
                    }
                }
            }
        }

        // Export root existence/writability.
        var root = ResolveExportRootDir();
        if (root.NullOrEmpty() || !Directory.Exists(root))
            vr.errors.Add("Export root directory does not exist: " + (root ?? "(null)"));
        else if (!AgsExportUtil.CanWriteToDirectory(root, out var err))
            vr.warnings.Add("Export root may not be writable: " + err);
    }

    private static void ValidateClip(ValidationResult vr, AgsModel.ClipSpec clip, int durationTicks, string context)
    {
        if (clip == null)
        {
            vr.errors.Add(context + ": clip is null.");
            return;
        }

        durationTicks = Mathf.Max(1, durationTicks);

        if (clip.tracks == null)
        {
            vr.errors.Add(context + ": tracks list is null.");
            return;
        }

        if (clip.tracks.Count == 0)
        {
            vr.warnings.Add(context + ": has no tracks (export will produce an empty animation).");
            return;
        }

        for (int ti = 0; ti < clip.tracks.Count; ti++)
        {
            var tr = clip.tracks[ti];
            if (tr == null)
            {
                vr.errors.Add(context + $": track {ti} is null.");
                continue;
            }
            if (tr.nodeTag.NullOrEmpty())
            {
                vr.errors.Add(context + $": track {ti} has empty nodeTag.");
                continue;
            }
            if (tr.keys == null)
            {
                vr.errors.Add(context + $": track '{tr.nodeTag}' keys list is null.");
                continue;
            }

            int prev = int.MinValue;
            for (int ki = 0; ki < tr.keys.Count; ki++)
            {
                var k = tr.keys[ki];
                if (k == null)
                {
                    vr.errors.Add(context + $": track '{tr.nodeTag}' keyframe {ki} is null.");
                    continue;
                }
                if (k.tick < 0 || k.tick > durationTicks)
                    vr.errors.Add(context + $": track '{tr.nodeTag}' keyframe tick {k.tick} is outside 0..{durationTicks}.");
                if (k.tick < prev)
                    vr.errors.Add(context + $": track '{tr.nodeTag}' keyframes are not sorted by tick (tick {k.tick} after {prev}).");
                prev = k.tick;

                if (!AgsExportUtil.IsFinite(k.angle))
                    vr.errors.Add(context + $": invalid angle at tick {k.tick} (NaN/Infinity).");
                if (!AgsExportUtil.IsFinite(k.offset))
                    vr.errors.Add(context + $": invalid offset at tick {k.tick} (NaN/Infinity).");
                if (!AgsExportUtil.IsFinite(k.scale))
                    vr.errors.Add(context + $": invalid scale at tick {k.tick} (NaN/Infinity).");
                if (k.layerBias < -3 || k.layerBias > 3)
                    vr.errors.Add(context + $": layerBias {k.layerBias} at tick {k.tick} is outside -3..3.");
            }
        }
    }

    private static List<string> CollectVariantIds(AgsModel.Project project)
    {
        var set = new HashSet<string>();
        if (project?.stages != null)
        {
            for (int si = 0; si < project.stages.Count; si++)
            {
                var s = project.stages[si];
                if (s?.variants == null) continue;
                for (int vi = 0; vi < s.variants.Count; vi++)
                {
                    var v = s.variants[vi];
                    if (v == null) continue;
                    set.Add(v.variantId.NullOrEmpty() ? "Base" : v.variantId);
                }
            }
        }

        var list = set.ToList();
        list.Sort(StringComparer.Ordinal);
        if (list.Remove("Base")) list.Insert(0, "Base");
        return list;
    }

    private static AgsModel.ClipSpec GetClip(AgsModel.StageSpec stage, string variantId, string roleKey)
    {
        if (stage?.variants == null) return null;
        for (int i = 0; i < stage.variants.Count; i++)
        {
            var v = stage.variants[i];
            if (v == null) continue;
            string id = v.variantId.NullOrEmpty() ? "Base" : v.variantId;
            if (id != variantId) continue;
            return v.GetClip(roleKey);
        }
        return null;
    }
}
