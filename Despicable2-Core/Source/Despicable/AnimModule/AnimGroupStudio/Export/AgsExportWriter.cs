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
    private static void EnsureDirs(ExportPlan plan)
    {
        Directory.CreateDirectory(plan.groupsDir);
        Directory.CreateDirectory(plan.rolesDir);
        Directory.CreateDirectory(plan.animsDir);
        Directory.CreateDirectory(plan.offsetsDir);
    }

    private static void WriteAnimGroupDefXml(AgsModel.Project project, string groupDefName, List<string> roleDefNames, string fullPath)
    {
        var loop = new List<int>();
        for (int i = 0; i < project.stages.Count; i++)
            loop.Add(Mathf.Max(1, project.stages[i]?.repeatCount ?? 1));

        AgsExportUtil.WriteXmlAtomic(fullPath, w =>
        {
            w.WriteStartDocument();
            w.WriteStartElement("Defs");
            w.WriteStartElement("AnimGroupDef");

            AgsExportUtil.WriteElement(w, "defName", groupDefName);
            AgsExportUtil.WriteElement(w, "label", project.label ?? groupDefName);
            AgsExportUtil.WriteElement(w, "numActors", Mathf.Max(1, project.roles.Count).ToString(CultureInfo.InvariantCulture));

            w.WriteStartElement("animRoles");
            for (int i = 0; i < roleDefNames.Count; i++)
                AgsExportUtil.WriteElement(w, "li", roleDefNames[i]);
            w.WriteEndElement();

            w.WriteStartElement("loopIndex");
            for (int i = 0; i < loop.Count; i++)
                AgsExportUtil.WriteElement(w, "li", loop[i].ToString(CultureInfo.InvariantCulture));
            w.WriteEndElement();

            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndDocument();
        });
    }

    private static void WriteAnimRoleDefXml(AgsModel.RoleSpec role, string roleDefName, List<string> animDefNames, string offsetDefName, string fullPath)
    {
        int gender = 0;
        if (role != null)
        {
            if (role.genderReq == AgsModel.RoleGenderReq.Male) gender = 1;
            else if (role.genderReq == AgsModel.RoleGenderReq.Female) gender = 2;
        }

        AgsExportUtil.WriteXmlAtomic(fullPath, w =>
        {
            w.WriteStartDocument();
            w.WriteStartElement("Defs");
            w.WriteStartElement("AnimRoleDef");

            AgsExportUtil.WriteElement(w, "defName", roleDefName);
            AgsExportUtil.WriteElement(w, "label", role?.displayName ?? roleDefName);
            AgsExportUtil.WriteElement(w, "gender", gender.ToString(CultureInfo.InvariantCulture));

            w.WriteStartElement("anims");
            for (int i = 0; i < animDefNames.Count; i++)
                AgsExportUtil.WriteElement(w, "li", animDefNames[i]);
            w.WriteEndElement();

            if (!offsetDefName.NullOrEmpty())
                AgsExportUtil.WriteElement(w, "offsetDef", offsetDefName);

            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndDocument();
        });
    }

    private static void WriteAnimationDefXml(AgsModel.ClipSpec clip, string defName, int durationTicks, string fullPath)
    {
        durationTicks = Mathf.Max(1, durationTicks);

        AgsExportUtil.WriteXmlAtomic(fullPath, w =>
        {
            w.WriteStartDocument();
            w.WriteStartElement("Defs");
            w.WriteStartElement("AnimationDef");

            AgsExportUtil.WriteElement(w, "defName", defName);
            AgsExportUtil.WriteElement(w, "label", defName);
            AgsExportUtil.WriteElement(w, "durationTicks", durationTicks.ToString(CultureInfo.InvariantCulture));

            w.WriteStartElement("keyframeParts");

            if (clip?.tracks != null)
            {
                for (int ti = 0; ti < clip.tracks.Count; ti++)
                {
                    var track = clip.tracks[ti];
                    if (track == null) continue;
                    if (track.nodeTag.NullOrEmpty()) continue;

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

                            AgsExportUtil.WriteElement(w, "rotation", k.rotation.AsInt.ToString(CultureInfo.InvariantCulture));
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

                    w.WriteEndElement(); // keyframes
                    w.WriteEndElement(); // value
                    w.WriteEndElement(); // li
                }
            }

            w.WriteEndElement(); // keyframeParts
            w.WriteEndElement(); // AnimationDef
            w.WriteEndElement(); // Defs
            w.WriteEndDocument();
        });
    }

    private static void WriteAnimationOffsetDef(AgsModel.Project project, string roleKey, string defName, string fullPath)
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

        AgsExportUtil.WriteXmlAtomic(fullPath, w =>
        {
            w.WriteStartDocument();
            w.WriteStartElement("Defs");

            // Use full type name (matches Workshop exporter style).
            w.WriteStartElement(typeof(AnimationOffsetDef).FullName);
            AgsExportUtil.WriteElement(w, "defName", defName);
            AgsExportUtil.WriteElement(w, "label", defName);

            w.WriteStartElement("offsets");
            w.WriteStartElement("li");
            w.WriteAttributeString("Class", typeof(AnimationOffset_BodyType).FullName);
            w.WriteStartElement("offsets");
            for (int i = 0; i < bodyOffsets.Count; i++)
            {
                var bo = bodyOffsets[i];
                if (bo?.bodyType == null) continue;
                w.WriteStartElement("li");
                AgsExportUtil.WriteElement(w, "bodyType", bo.bodyType.defName);
                AgsExportUtil.WriteElement(w, "rotation", bo.rotation.ToString(CultureInfo.InvariantCulture));
                if (bo.offset != Vector3.zero) AgsExportUtil.WriteElement(w, "offset", AgsExportUtil.Vec3ToString(bo.offset));
                if (bo.scale != Vector3.one) AgsExportUtil.WriteElement(w, "scale", AgsExportUtil.Vec3ToString(bo.scale));
                w.WriteEndElement();
            }
            w.WriteEndElement(); // offsets
            w.WriteEndElement(); // li
            w.WriteEndElement(); // offsets

            w.WriteEndElement(); // AnimationOffsetDef
            w.WriteEndElement(); // Defs
            w.WriteEndDocument();
        });
    }

    public static string VariantIdToCode(string variantId)
    {
        if (variantId.NullOrEmpty() || variantId == "Base") return "";
        return AgsExportUtil.NormalizeTag(variantId);
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
                return modRoot;
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
        if (project == null || plan.variantIds.NullOrEmpty()) return;

        for (int vi = 0; vi < plan.variantIds.Count; vi++)
        {
            string variantId = plan.variantIds[vi];
            string code = VariantIdToCode(variantId);
            string groupDefName = AgsExportUtil.MakeSafeDefName(AgsExportUtil.MakeVariationDefName(plan.baseDefName, code));
            AddTarget(plan, Path.Combine(plan.groupsDir, groupDefName + ".xml"));

            for (int ri = 0; ri < project.roles.Count; ri++)
            {
                var role = project.roles[ri];
                if (role == null) continue;
                string safeRoleKey = AgsExportUtil.MakeSafeDefName(role.roleKey ?? $"role_{ri + 1}");

                string offsetDefName = $"{groupDefName}_{safeRoleKey}_Offsets";
                AddTarget(plan, Path.Combine(plan.offsetsDir, offsetDefName + ".xml"));

                string roleDefName = $"{groupDefName}_{safeRoleKey}";
                AddTarget(plan, Path.Combine(plan.rolesDir, roleDefName + ".xml"));

                for (int si = 0; si < project.stages.Count; si++)
                {
                    string animDefName = $"{groupDefName}_S{si}_{safeRoleKey}";
                    AddTarget(plan, Path.Combine(plan.animsDir, animDefName + ".xml"));
                }
            }
        }
    }

    private static void AddTarget(ExportPlan plan, string path)
    {
        if (path.NullOrEmpty()) return;
        plan.allTargetFiles.Add(path);
        if (File.Exists(path)) plan.existingTargets.Add(path);
    }
}
