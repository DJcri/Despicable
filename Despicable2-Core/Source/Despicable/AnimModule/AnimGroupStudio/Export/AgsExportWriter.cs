using RimWorld;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Verse;
using System.Xml;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimModule.AnimGroupStudio.Export;
public sealed partial class AgsExport
{
    private static void EnsureDirs(ExportPlan plan)
    {
        Directory.CreateDirectory(plan.packageDir);
        Directory.CreateDirectory(plan.stagesDir);
    }

    private static void WriteGroupPackageXml(AgsModel.Project project, ExportPlan plan, string fullPath)
    {
        AgsExportUtil.WriteXmlAtomic(fullPath, w =>
        {
            w.WriteStartDocument();
            w.WriteStartElement("Defs");

            for (int vi = 0; vi < plan.variantIds.Count; vi++)
            {
                string variantId = plan.variantIds[vi];
                string groupDefName = AgsExportUtil.MakeGroupDefName(plan.projectKey, variantId);

                w.WriteStartElement(typeof(AnimGroupDef).FullName);
                AgsExportUtil.WriteElement(w, "defName", groupDefName);

                w.WriteStartElement("stageTags");
                AgsExportUtil.WriteElement(w, "li", plan.projectKey);
                w.WriteEndElement();

                AgsExportUtil.WriteElement(w, "numActors", Mathf.Max(1, project.roles.Count).ToString(CultureInfo.InvariantCulture));

                w.WriteStartElement("loopIndex");
                for (int i = 0; i < project.stages.Count; i++)
                {
                    int repeatCount = Mathf.Max(1, project.stages[i]?.repeatCount ?? 1);
                    AgsExportUtil.WriteElement(w, "li", repeatCount.ToString(CultureInfo.InvariantCulture));
                }
                w.WriteEndElement();

                w.WriteStartElement("animRoles");
                for (int ri = 0; ri < project.roles.Count; ri++)
                {
                    var role = project.roles[ri];
                    if (role == null) continue;
                    string roleKey = role.roleKey ?? $"role_{ri + 1}";
                    AgsExportUtil.WriteElement(w, "li", AgsExportUtil.MakeRoleDefName(plan.projectKey, variantId, roleKey));
                }
                w.WriteEndElement();

                w.WriteEndElement();

                for (int ri = 0; ri < project.roles.Count; ri++)
                {
                    var role = project.roles[ri];
                    if (role == null) continue;
                    WriteAnimRoleDefElement(w, plan.projectKey, variantId, role, ri, project.stages.Count);
                }
            }

            w.WriteEndElement();
            w.WriteEndDocument();
        });
    }

    private static void WriteAnimRoleDefElement(XmlWriter w, string projectKey, string variantId, AgsModel.RoleSpec role, int roleIndex, int stageCount)
    {
        int gender = 0;
        if (role != null)
        {
            if (role.genderReq == AgsModel.RoleGenderReq.Male) gender = 1;
            else if (role.genderReq == AgsModel.RoleGenderReq.Female) gender = 2;
        }

        string roleKey = role?.roleKey ?? $"role_{roleIndex + 1}";
        string roleDefName = AgsExportUtil.MakeRoleDefName(projectKey, variantId, roleKey);
        string offsetDefName = AgsExportUtil.MakeOffsetDefName(projectKey, roleKey);

        w.WriteStartElement(typeof(AnimRoleDef).FullName);
        AgsExportUtil.WriteElement(w, "defName", roleDefName);
        AgsExportUtil.WriteElement(w, "gender", gender.ToString(CultureInfo.InvariantCulture));
        AgsExportUtil.WriteElement(w, "offsetDef", offsetDefName);

        w.WriteStartElement("anims");
        for (int si = 0; si < stageCount; si++)
            AgsExportUtil.WriteElement(w, "li", AgsExportUtil.MakeAnimationDefName(projectKey, roleKey, si, variantId));
        w.WriteEndElement();

        w.WriteEndElement();
    }

    private static void WriteOffsetPackageXml(AgsModel.Project project, ExportPlan plan, string fullPath)
    {
        AgsExportUtil.WriteXmlAtomic(fullPath, w =>
        {
            w.WriteStartDocument();
            w.WriteStartElement("Defs");

            for (int ri = 0; ri < project.roles.Count; ri++)
            {
                var role = project.roles[ri];
                if (role == null) continue;
                string roleKey = role.roleKey ?? $"role_{ri + 1}";
                string defName = AgsExportUtil.MakeOffsetDefName(plan.projectKey, roleKey);
                var bodyOffsets = CollectBodyOffsets(project, roleKey);
                WriteAnimationOffsetDefElement(w, defName, bodyOffsets);
            }

            w.WriteEndElement();
            w.WriteEndDocument();
        });
    }

    private static List<Despicable.BodyTypeOffset> CollectBodyOffsets(AgsModel.Project project, string roleKey)
    {
        var bodyOffsets = new List<Despicable.BodyTypeOffset>();
        if (project?.offsetsByRoleKey != null && !roleKey.NullOrEmpty() && project.offsetsByRoleKey.TryGetValue(roleKey, out var dict) && dict != null)
        {
            foreach (var kv in dict)
            {
                var bodyTypeDefName = kv.Key;
                var val = kv.Value;
                if (bodyTypeDefName.NullOrEmpty() || val == null) continue;
                var bodyType = DefDatabase<BodyTypeDef>.GetNamedSilentFail(bodyTypeDefName);
                if (bodyType == null) continue;

                bodyOffsets.Add(new Despicable.BodyTypeOffset
                {
                    bodyType = bodyType,
                    rotation = val.rotation,
                    offset = new Vector3(val.rootOffset.x, 0f, val.rootOffset.y),
                    scale = val.scale == default ? Vector3.one : val.scale
                });
            }
        }
        return bodyOffsets;
    }

    private static void WriteAnimationOffsetDefElement(XmlWriter w, string defName, List<Despicable.BodyTypeOffset> bodyOffsets)
    {
        w.WriteStartElement(typeof(AnimationOffsetDef).FullName);
        AgsExportUtil.WriteElement(w, "defName", defName);

        w.WriteStartElement("offsets");
        w.WriteStartElement("li");
        w.WriteAttributeString("Class", typeof(AnimationOffset_BodyType).FullName);

        w.WriteStartElement("races");
        AgsExportUtil.WriteElement(w, "li", "Human");
        w.WriteEndElement();

        w.WriteStartElement("offsets");
        for (int i = 0; i < bodyOffsets.Count; i++)
        {
            var bo = bodyOffsets[i];
            if (bo?.bodyType == null) continue;
            w.WriteStartElement("li");
            AgsExportUtil.WriteElement(w, "bodyType", bo.bodyType.defName);
            if (bo.rotation != 0)
                AgsExportUtil.WriteElement(w, "rotation", bo.rotation.ToString(CultureInfo.InvariantCulture));
            if (bo.offset != Vector3.zero)
                AgsExportUtil.WriteElement(w, "offset", AgsExportUtil.Vec3ToString(bo.offset));
            if (bo.scale != Vector3.one)
                AgsExportUtil.WriteElement(w, "scale", AgsExportUtil.Vec3ToString(bo.scale));
            w.WriteEndElement();
        }
        w.WriteEndElement();

        w.WriteEndElement();
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void WriteStagePackageXml(AgsModel.Project project, ExportPlan plan, string variantId, int stageIndex, string fullPath)
    {
        var stage = project?.stages != null && stageIndex >= 0 && stageIndex < project.stages.Count ? project.stages[stageIndex] : null;
        int durationTicks = Mathf.Max(1, stage?.durationTicks ?? 1);

        AgsExportUtil.WriteXmlAtomic(fullPath, w =>
        {
            w.WriteStartDocument();
            w.WriteStartElement("Defs");

            for (int ri = 0; ri < project.roles.Count; ri++)
            {
                var role = project.roles[ri];
                if (role == null) continue;
                string roleKey = role.roleKey ?? $"role_{ri + 1}";
                var clip = GetClip(stage, variantId, roleKey);
                if (clip == null)
                    clip = new AgsModel.ClipSpec { lengthTicks = durationTicks, tracks = new List<AgsModel.Track>() };

                string animDefName = AgsExportUtil.MakeAnimationDefName(plan.projectKey, roleKey, stageIndex, variantId);
                WriteAnimationDefElement(w, clip, animDefName, durationTicks);
            }

            w.WriteEndElement();
            w.WriteEndDocument();
        });
    }

    private static void WriteAnimationDefElement(XmlWriter w, AgsModel.ClipSpec clip, string defName, int durationTicks)
    {
        durationTicks = Mathf.Max(1, durationTicks);

        w.WriteStartElement("AnimationDef");
        AgsExportUtil.WriteElement(w, "defName", defName);
        AgsExportUtil.WriteElement(w, "durationTicks", durationTicks.ToString(CultureInfo.InvariantCulture));

        w.WriteStartElement("keyframeParts");

        if (clip?.tracks != null)
        {
            for (int ti = 0; ti < clip.tracks.Count; ti++)
            {
                var track = clip.tracks[ti];
                if (track == null || track.nodeTag.NullOrEmpty()) continue;

                w.WriteStartElement("li");
                AgsExportUtil.WriteElement(w, "key", track.nodeTag);
                w.WriteStartElement("value");
                AgsExportUtil.WriteElement(w, "workerType", typeof(AnimationWorker_ExtendedKeyframes).FullName);
                w.WriteStartElement("keyframes");

                if (track.keys != null)
                {
                    for (int ki = 0; ki < track.keys.Count; ki++)
                    {
                        var k = track.keys[ki];
                        if (k == null) continue;

                        w.WriteStartElement("li");
                        w.WriteAttributeString("Class", typeof(ExtendedKeyframe).FullName);

                        AgsExportUtil.WriteElement(w, "tick", Mathf.Clamp(k.tick, 0, durationTicks).ToString(CultureInfo.InvariantCulture));
                        AgsExportUtil.WriteElement(w, "angle", k.angle.ToString(CultureInfo.InvariantCulture));
                        AgsExportUtil.WriteElement(w, "visible", k.visible ? "true" : "false");

                        if (k.offset != Vector3.zero)
                            AgsExportUtil.WriteElement(w, "offset", AgsExportUtil.Vec3ToString(k.offset));
                        if (k.scale != Vector3.one)
                            AgsExportUtil.WriteElement(w, "scale", AgsExportUtil.Vec3ToString(k.scale));

                        AgsExportUtil.WriteElement(w, "rotation", Rot4ToExportString(k.rotation));
                        if (!k.graphicState.NullOrEmpty())
                            AgsExportUtil.WriteElement(w, "graphicState", k.graphicState);
                        if (k.variant != -1)
                            AgsExportUtil.WriteElement(w, "variant", k.variant.ToString(CultureInfo.InvariantCulture));
                        if (!k.soundDefName.NullOrEmpty())
                            AgsExportUtil.WriteElement(w, "sound", k.soundDefName);
                        if (!k.facialAnimDefName.NullOrEmpty())
                            AgsExportUtil.WriteElement(w, "facialAnim", k.facialAnimDefName);
                        if (k.layerBias != 0)
                            AgsExportUtil.WriteElement(w, "layerBias", Mathf.Clamp(k.layerBias, -3, 3).ToString(CultureInfo.InvariantCulture));

                        w.WriteEndElement();
                    }
                }

                w.WriteEndElement();
                w.WriteEndElement();
                w.WriteEndElement();
            }
        }

        w.WriteEndElement();
        w.WriteEndElement();
    }


    private static string Rot4ToExportString(Rot4 rot)
    {
        switch (rot.AsInt)
        {
            case 0: return "North";
            case 1: return "East";
            case 2: return "South";
            case 3: return "West";
            default: return "South";
        }
    }

    public static string VariantIdToCode(string variantId)
    {
        return AgsExportUtil.MakeVariantCode(variantId);
    }

    private static string ResolveExportRootDir()
    {
        try
        {
            string settingsRoot = ModMain.Instance?.settings?.workshopExportRootPath;
            if (!settingsRoot.NullOrEmpty() && Directory.Exists(settingsRoot))
                return settingsRoot;
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsExport.EmptyCatch:2", "AGS export best-effort step failed and fell back.", e); }

        try
        {
            string modRoot = ModMain.Instance?.Content?.RootDir;
            if (!modRoot.NullOrEmpty() && Directory.Exists(modRoot))
            {
                string nsfwPath = Path.Combine(modRoot, "Defs", "LovinModule", "Animations");
                if (Directory.Exists(nsfwPath))
                    return modRoot;

                var parentDir = Directory.GetParent(modRoot);
                if (parentDir != null)
                {
                    string siblingNsfwRoot = Path.Combine(parentDir.FullName, "Despicable2-NSFW");
                    string siblingNsfwPath = Path.Combine(siblingNsfwRoot, "Defs", "LovinModule", "Animations");
                    if (Directory.Exists(siblingNsfwPath))
                        return siblingNsfwRoot;
                }

                return modRoot;
            }
        }
        catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsExport.EmptyCatch:3", "AGS export best-effort step failed and fell back.", e); }

        string fallback = Path.Combine(GenFilePaths.ConfigFolderPath, "Despicable", "AnimGroupStudio", "Exports");
        try { Directory.CreateDirectory(fallback); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsExport.EmptyCatch:4", "AGS export best-effort step failed and fell back.", e); }
        return fallback;
    }

    private static void BuildTargetFiles(AgsModel.Project project, ExportPlan plan)
    {
        plan.allTargetFiles.Clear();
        plan.existingTargets.Clear();
        plan.stageTargets.Clear();

        if (project == null)
            return;

        AddTarget(plan, plan.groupFilePath);
        AddTarget(plan, plan.offsetFilePath);

        if (plan.variantIds.NullOrEmpty())
            return;

        for (int vi = 0; vi < plan.variantIds.Count; vi++)
        {
            string variantId = plan.variantIds[vi];
            for (int si = 0; si < project.stages.Count; si++)
            {
                string filePath = Path.Combine(plan.stagesDir, AgsExportUtil.MakeStageFileName(plan.projectKey, si, variantId));
                plan.stageTargets.Add(new StageTarget
                {
                    variantId = variantId,
                    stageIndex = si,
                    filePath = filePath
                });
                AddTarget(plan, filePath);
            }
        }
    }

    private static void AddTarget(ExportPlan plan, string path)
    {
        if (path.NullOrEmpty()) return;
        if (!plan.allTargetFiles.Contains(path))
            plan.allTargetFiles.Add(path);
        if (File.Exists(path) && !plan.existingTargets.Contains(path))
            plan.existingTargets.Add(path);
    }
}
