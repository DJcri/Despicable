using RimWorld;
using System;
using System.Collections.Generic;
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
            vr.errors.Add("Base def prefix is empty. Set a Def prefix value in the left panel.");
        else if (!AgsExportUtil.IsExactDefName(project.export.baseDefName))
            vr.errors.Add("Base def prefix must use only letters, digits, and underscores, and cannot start with a digit.");

        if (project.roles.NullOrEmpty())
            vr.errors.Add("Project has no roles.");

        if (project.stages.NullOrEmpty())
            vr.errors.Add("Project has no stages.");

        if (!vr.Ok) return;

        var roleNameMap = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int ri = 0; ri < project.roles.Count; ri++)
        {
            var role = project.roles[ri];
            if (role == null || role.roleKey.NullOrEmpty())
                continue;

            string safeRoleKey = AgsExportUtil.MakeSafeDefName(role.roleKey);
            if (roleNameMap.TryGetValue(safeRoleKey, out var otherRoleKey) && otherRoleKey != role.roleKey)
                vr.errors.Add($"Role keys '{otherRoleKey}' and '{role.roleKey}' normalize to the same exported name '{safeRoleKey}'.");
            else
                roleNameMap[safeRoleKey] = role.roleKey;
        }

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

            var baseVariant = s.variants.FirstOrDefault(x => x != null && (x.variantId.NullOrEmpty() ? "Base" : x.variantId) == "Base");
            if (baseVariant == null)
                vr.errors.Add($"Stage {si} is missing the Base variant.");

            for (int vi = 0; vi < s.variants.Count; vi++)
            {
                var variant = s.variants[vi];
                if (variant == null)
                    continue;

                string variantId = variant.variantId.NullOrEmpty() ? "Base" : variant.variantId;
                if (variantId != "Base")
                    vr.errors.Add($"Stage {si} uses legacy variant '{variantId}'. AGS export now expects one variation per project; import or rebuild this variation as its own project.");
            }

            if (baseVariant == null)
                continue;

            for (int ri = 0; ri < project.roles.Count; ri++)
            {
                var role = project.roles[ri];
                if (role == null) continue;
                if (role.roleKey.NullOrEmpty())
                {
                    vr.errors.Add($"Role {ri} is missing roleKey.");
                    continue;
                }

                var clip = baseVariant.GetClip(role.roleKey);
                if (clip == null)
                {
                    vr.errors.Add($"Stage {si} is missing the Base clip for role '{role.roleKey}'.");
                    continue;
                }

                ValidateClip(vr, clip, s.durationTicks, $"Stage {si} '{s.label ?? "(unnamed)"}' role '{role.roleKey}'");
            }
        }

        for (int si = 0; si < project.stages.Count; si++)
        {
            var s = project.stages[si];
            var baseVariant = s?.variants?.FirstOrDefault(x => x != null && (x.variantId.NullOrEmpty() ? "Base" : x.variantId) == "Base");
            if (baseVariant?.clips == null)
                continue;

            for (int ci = 0; ci < baseVariant.clips.Count; ci++)
            {
                var rc = baseVariant.clips[ci];
                if (rc?.clip?.tracks == null) continue;
                for (int ti = 0; ti < rc.clip.tracks.Count; ti++)
                {
                    var tr = rc.clip.tracks[ti];
                    if (tr == null) continue;

                    if (!tr.nodeTag.NullOrEmpty() && DefDatabase<PawnRenderNodeTagDef>.GetNamedSilentFail(tr.nodeTag) == null)
                        vr.errors.Add($"Missing PawnRenderNodeTagDef '{tr.nodeTag}' (stage {si} role '{rc.roleKey}').");

                    if (tr.keys == null) continue;
                    for (int ki = 0; ki < tr.keys.Count; ki++)
                    {
                        var k = tr.keys[ki];
                        if (k == null) continue;
                        if (!k.soundDefName.NullOrEmpty() && DefDatabase<SoundDef>.GetNamedSilentFail(k.soundDefName) == null)
                            vr.errors.Add($"Missing SoundDef '{k.soundDefName}' (stage {si} role '{rc.roleKey}', tick {k.tick}).");
                        if (!k.facialAnimDefName.NullOrEmpty() && DefDatabase<FacialAnimDef>.GetNamedSilentFail(k.facialAnimDefName) == null)
                            vr.errors.Add($"Missing FacialAnimDef '{k.facialAnimDefName}' (stage {si} role '{rc.roleKey}', tick {k.tick}).");
                        if (k.prop != null && !k.prop.propDefName.NullOrEmpty() && DefDatabase<ThingDef>.GetNamedSilentFail(k.prop.propDefName) == null)
                            vr.errors.Add($"Missing ThingDef for prop '{k.prop.propDefName}' (stage {si} role '{rc.roleKey}', tick {k.tick}).");
                    }
                }
            }
        }

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
                else if (k.tick == prev)
                    vr.errors.Add(context + $": track '{tr.nodeTag}' has more than one keyframe at tick {k.tick}.");
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

    private static AgsModel.ClipSpec GetClip(AgsModel.StageSpec stage, string roleKey)
    {
        if (stage?.variants == null) return null;
        for (int i = 0; i < stage.variants.Count; i++)
        {
            var v = stage.variants[i];
            if (v == null) continue;
            string id = v.variantId.NullOrEmpty() ? "Base" : v.variantId;
            if (id != "Base") continue;
            return v.GetClip(roleKey);
        }
        return null;
    }
}
