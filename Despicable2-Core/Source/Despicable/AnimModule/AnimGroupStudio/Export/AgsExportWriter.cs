using RimWorld;
using System;
// Guardrail-Reason: AGS export writing stays co-located because package merge decisions, emitted file layout, and fallback paths share one export pipeline.
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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

    private static void CaptureExistingVariationRefs(ExportPlan plan)
    {
        plan.existingOwnedRoleDefNames.Clear();
        plan.existingOwnedOffsetDefNames.Clear();

        if (plan == null || plan.groupFilePath.NullOrEmpty() || !File.Exists(plan.groupFilePath))
            return;

        try
        {
            var doc = LoadDefsDocument(plan.groupFilePath);
            var root = doc.DocumentElement;
            if (root == null)
                return;

            var groupEl = FindDefElement(root, plan.variationDefName);
            if (groupEl == null)
                return;

            foreach (string roleDefName in ReadListValues(groupEl, "animRoles"))
            {
                if (roleDefName.NullOrEmpty() || plan.existingOwnedRoleDefNames.Contains(roleDefName))
                    continue;

                plan.existingOwnedRoleDefNames.Add(roleDefName);
                var roleEl = FindDefElement(root, roleDefName);
                if (roleEl == null)
                    continue;

                string offsetDefName = GetChildText(roleEl, "offsetDef");
                if (!offsetDefName.NullOrEmpty() && !plan.existingOwnedOffsetDefNames.Contains(offsetDefName))
                    plan.existingOwnedOffsetDefNames.Add(offsetDefName);
            }
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("AgsExport.CaptureExistingVariationRefs", "AGS export could not inspect the existing family package; continuing with append-only merge.", e);
        }
    }

    private static void DeleteStaleStageFiles(ExportPlan plan, ExportResult result)
    {
        if (plan?.staleStageFiles == null)
            return;

        for (int i = 0; i < plan.staleStageFiles.Count; i++)
        {
            string stale = plan.staleStageFiles[i];
            if (stale.NullOrEmpty() || !File.Exists(stale))
                continue;

            try
            {
                File.Delete(stale);
                result.filesDeleted.Add(stale);
            }
            catch (Exception e)
            {
                throw new IOException("Failed to delete stale stage export file: " + stale, e);
            }
        }
    }

    private static void WriteGroupPackageXml(AgsModel.Project project, ExportPlan plan, string fullPath)
    {
        var doc = LoadOrCreateDefsDocument(fullPath);
        var root = doc.DocumentElement ?? doc.AppendChild(doc.CreateElement("Defs")) as XmlElement;

        RemoveDefElement(root, plan.variationDefName);
        RemoveDefElements(root, plan.existingOwnedRoleDefNames);
        for (int ri = 0; ri < project.roles.Count; ri++)
        {
            var role = project.roles[ri];
            if (role == null) continue;
            RemoveDefElement(root, AgsExportUtil.MakeRoleDefName(plan.baseDefName, project.label, role.roleKey ?? $"role_{ri + 1}"));
        }

        root.AppendChild(BuildAnimGroupDefElement(doc, project, plan));
        for (int ri = 0; ri < project.roles.Count; ri++)
        {
            var role = project.roles[ri];
            if (role == null) continue;
            root.AppendChild(BuildAnimRoleDefElement(doc, plan, project.label, role, ri, project.stages.Count));
        }

        SaveXmlDocumentAtomic(fullPath, doc);
    }

    private static XmlElement BuildAnimGroupDefElement(XmlDocument doc, AgsModel.Project project, ExportPlan plan)
    {
        var groupEl = doc.CreateElement(typeof(AnimGroupDef).FullName);
        AppendTextElement(doc, groupEl, "defName", plan.variationDefName);

        var tagsEl = doc.CreateElement("stageTags");
        var emittedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (project?.groupTags != null)
        {
            for (int i = 0; i < project.groupTags.Count; i++)
            {
                string tag = project.groupTags[i]?.Trim();
                if (tag.NullOrEmpty() || !emittedTags.Add(tag))
                    continue;
                AppendTextElement(doc, tagsEl, "li", tag);
            }
        }
        groupEl.AppendChild(tagsEl);

        AppendTextElement(doc, groupEl, "numActors", Mathf.Max(1, project.roles.Count).ToString(CultureInfo.InvariantCulture));

        var loopEl = doc.CreateElement("loopIndex");
        for (int i = 0; i < project.stages.Count; i++)
        {
            int repeatCount = Mathf.Max(1, project.stages[i]?.repeatCount ?? 1);
            AppendTextElement(doc, loopEl, "li", repeatCount.ToString(CultureInfo.InvariantCulture));
        }
        groupEl.AppendChild(loopEl);

        var rolesEl = doc.CreateElement("animRoles");
        for (int ri = 0; ri < project.roles.Count; ri++)
        {
            var role = project.roles[ri];
            if (role == null) continue;
            string roleKey = role.roleKey ?? $"role_{ri + 1}";
            AppendTextElement(doc, rolesEl, "li", AgsExportUtil.MakeRoleDefName(plan.baseDefName, project.label, roleKey));
        }
        groupEl.AppendChild(rolesEl);

        return groupEl;
    }

    private static XmlElement BuildAnimRoleDefElement(XmlDocument doc, ExportPlan plan, string variationLabel, AgsModel.RoleSpec role, int roleIndex, int stageCount)
    {
        int gender = 0;
        if (role != null)
        {
            if (role.genderReq == AgsModel.RoleGenderReq.Male) gender = 1;
            else if (role.genderReq == AgsModel.RoleGenderReq.Female) gender = 2;
        }

        string roleKey = role?.roleKey ?? $"role_{roleIndex + 1}";
        string roleDefName = AgsExportUtil.MakeRoleDefName(plan.baseDefName, variationLabel, roleKey);
        string offsetDefName = AgsExportUtil.MakeOffsetDefName(plan.baseDefName, variationLabel, roleKey);

        var roleEl = doc.CreateElement(typeof(AnimRoleDef).FullName);
        AppendTextElement(doc, roleEl, "defName", roleDefName);
        AppendTextElement(doc, roleEl, "gender", gender.ToString(CultureInfo.InvariantCulture));
        AppendTextElement(doc, roleEl, "offsetDef", offsetDefName);

        var animsEl = doc.CreateElement("anims");
        for (int si = 0; si < stageCount; si++)
            AppendTextElement(doc, animsEl, "li", AgsExportUtil.MakeAnimationDefName(plan.baseDefName, variationLabel, roleKey, si));
        roleEl.AppendChild(animsEl);

        return roleEl;
    }

    private static void WriteOffsetPackageXml(AgsModel.Project project, ExportPlan plan, string fullPath)
    {
        var doc = LoadOrCreateDefsDocument(fullPath);
        var root = doc.DocumentElement ?? doc.AppendChild(doc.CreateElement("Defs")) as XmlElement;

        RemoveDefElements(root, plan.existingOwnedOffsetDefNames);
        for (int ri = 0; ri < project.roles.Count; ri++)
        {
            var role = project.roles[ri];
            if (role == null) continue;
            RemoveDefElement(root, AgsExportUtil.MakeOffsetDefName(plan.baseDefName, project.label, role.roleKey ?? $"role_{ri + 1}"));
        }

        for (int ri = 0; ri < project.roles.Count; ri++)
        {
            var role = project.roles[ri];
            if (role == null) continue;
            string roleKey = role.roleKey ?? $"role_{ri + 1}";
            string defName = AgsExportUtil.MakeOffsetDefName(plan.baseDefName, project.label, roleKey);
            var bodyOffsets = CollectBodyOffsets(project, roleKey);
            root.AppendChild(BuildAnimationOffsetDefElement(doc, defName, bodyOffsets));
        }

        SaveXmlDocumentAtomic(fullPath, doc);
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

    private static XmlElement BuildAnimationOffsetDefElement(XmlDocument doc, string defName, List<Despicable.BodyTypeOffset> bodyOffsets)
    {
        var defEl = doc.CreateElement(typeof(AnimationOffsetDef).FullName);
        AppendTextElement(doc, defEl, "defName", defName);

        var offsetsEl = doc.CreateElement("offsets");
        var entryEl = doc.CreateElement("li");
        var classAttr = doc.CreateAttribute("Class");
        classAttr.Value = typeof(AnimationOffset_BodyType).FullName;
        entryEl.Attributes.Append(classAttr);

        var racesEl = doc.CreateElement("races");
        AppendTextElement(doc, racesEl, "li", "Human");
        entryEl.AppendChild(racesEl);

        var listEl = doc.CreateElement("offsets");
        for (int i = 0; i < bodyOffsets.Count; i++)
        {
            var bo = bodyOffsets[i];
            if (bo?.bodyType == null) continue;
            var bodyEl = doc.CreateElement("li");
            AppendTextElement(doc, bodyEl, "bodyType", bo.bodyType.defName);
            if (bo.rotation != 0)
                AppendTextElement(doc, bodyEl, "rotation", bo.rotation.ToString(CultureInfo.InvariantCulture));
            if (bo.offset != Vector3.zero)
                AppendTextElement(doc, bodyEl, "offset", AgsExportUtil.Vec3ToString(bo.offset));
            if (bo.scale != Vector3.one)
                AppendTextElement(doc, bodyEl, "scale", AgsExportUtil.Vec3ToString(bo.scale));
            listEl.AppendChild(bodyEl);
        }
        entryEl.AppendChild(listEl);

        offsetsEl.AppendChild(entryEl);
        defEl.AppendChild(offsetsEl);
        return defEl;
    }

    private static void WriteStagePackageXml(AgsModel.Project project, ExportPlan plan, int stageIndex, string fullPath)
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
                var clip = GetClip(stage, roleKey);
                if (clip == null)
                    clip = new AgsModel.ClipSpec { lengthTicks = durationTicks, tracks = new List<AgsModel.Track>() };

                string animDefName = AgsExportUtil.MakeAnimationDefName(plan.baseDefName, project.label, roleKey, stageIndex);
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
                        string graphicState = !k.graphicState.NullOrEmpty() ? k.graphicState : (k.variant >= 0 ? "variant_" + k.variant : null);
                        if (!graphicState.NullOrEmpty())
                            AgsExportUtil.WriteElement(w, "graphicState", graphicState);
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
        plan.staleStageFiles.Clear();

        if (project == null)
            return;

        AddTarget(plan, plan.groupFilePath);
        AddTarget(plan, plan.offsetFilePath);

        for (int si = 0; si < project.stages.Count; si++)
        {
            string filePath = Path.Combine(plan.stagesDir, AgsExportUtil.MakeStageFileName(plan.baseDefName, si, plan.variationLabel));
            plan.stageTargets.Add(new StageTarget
            {
                stageIndex = si,
                filePath = filePath
            });
            AddTarget(plan, filePath);
        }

        if (!Directory.Exists(plan.stagesDir))
            return;

        var liveTargets = new HashSet<string>(plan.stageTargets.Select(x => x.filePath), StringComparer.OrdinalIgnoreCase);
        foreach (string existing in Directory.GetFiles(plan.stagesDir, "*.xml", SearchOption.TopDirectoryOnly))
        {
            string fileName = Path.GetFileName(existing);
            if (!AgsExportUtil.IsStageFileForVariation(fileName, plan.baseDefName, plan.variationLabel))
                continue;
            if (liveTargets.Contains(existing))
                continue;

            plan.staleStageFiles.Add(existing);
            AddTarget(plan, existing);
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

    private static XmlDocument LoadOrCreateDefsDocument(string path)
    {
        if (!path.NullOrEmpty() && File.Exists(path))
            return LoadDefsDocument(path);

        var doc = new XmlDocument();
        doc.AppendChild(doc.CreateXmlDeclaration("1.0", "utf-8", null));
        doc.AppendChild(doc.CreateElement("Defs"));
        return doc;
    }

    private static XmlDocument LoadDefsDocument(string path)
    {
        var doc = new XmlDocument();
        doc.PreserveWhitespace = false;
        doc.Load(path);
        if (doc.DocumentElement == null)
            doc.AppendChild(doc.CreateElement("Defs"));
        return doc;
    }

    private static void SaveXmlDocumentAtomic(string fullPath, XmlDocument doc)
    {
        AgsExportUtil.WriteXmlAtomic(fullPath, w => doc.WriteTo(w));
    }

    private static XmlElement FindDefElement(XmlElement root, string defName)
    {
        if (root == null || defName.NullOrEmpty())
            return null;

        foreach (XmlNode node in root.ChildNodes)
        {
            if (node is not XmlElement el)
                continue;
            if (string.Equals(GetChildText(el, "defName"), defName, StringComparison.Ordinal))
                return el;
        }

        return null;
    }

    private static void RemoveDefElement(XmlElement root, string defName)
    {
        var el = FindDefElement(root, defName);
        if (el != null)
            root.RemoveChild(el);
    }

    private static void RemoveDefElements(XmlElement root, IEnumerable<string> defNames)
    {
        if (root == null || defNames == null)
            return;

        foreach (string defName in defNames)
        {
            if (defName.NullOrEmpty())
                continue;
            RemoveDefElement(root, defName);
        }
    }

    private static string GetChildText(XmlElement parent, string childName)
    {
        if (parent == null || childName.NullOrEmpty())
            return null;
        foreach (XmlNode node in parent.ChildNodes)
        {
            if (node is XmlElement child && child.Name == childName)
                return child.InnerText;
        }
        return null;
    }

    private static IEnumerable<string> ReadListValues(XmlElement parent, string listName)
    {
        if (parent == null || listName.NullOrEmpty())
            yield break;

        XmlElement listEl = null;
        foreach (XmlNode node in parent.ChildNodes)
        {
            if (node is XmlElement child && child.Name == listName)
            {
                listEl = child;
                break;
            }
        }
        if (listEl == null)
            yield break;

        foreach (XmlNode node in listEl.ChildNodes)
        {
            if (node is XmlElement li && li.Name == "li" && !li.InnerText.NullOrEmpty())
                yield return li.InnerText;
        }
    }

    private static void AppendTextElement(XmlDocument doc, XmlElement parent, string name, string value)
    {
        var el = doc.CreateElement(name);
        el.InnerText = value ?? string.Empty;
        parent.AppendChild(el);
    }
}
