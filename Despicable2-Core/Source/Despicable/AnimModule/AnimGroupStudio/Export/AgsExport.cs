using RimWorld;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.AnimModule.AnimGroupStudio.Model;
using Despicable.AnimGroupStudio;

namespace Despicable.AnimModule.AnimGroupStudio.Export;
/// <summary>
/// Anim Group Studio exporter:
/// - Validates an editor Project
/// - Writes deterministic Def XML into a RimWorld-loadable Defs/ folder
///   (so the content loads after restart)
///
/// Output types:
/// - AnimGroupDef (one per variation)
/// - AnimRoleDef  (one per variation + role)
/// - AnimationDef (one per variation + role + stage)
/// - AnimationOffsetDef (one per variation + role)
/// </summary>
public sealed partial class AgsExport
{
    public sealed partial class ValidationResult
    {
        public readonly List<string> errors = new();
        public readonly List<string> warnings = new();
        public bool Ok => errors.Count == 0;
    }

    public sealed partial class ExportPlan
    {
        public string rootDir;
        public string baseDefName;
        public List<string> variantIds;

        public string groupsDir;
        public string rolesDir;
        public string animsDir;
        public string offsetsDir;

        public readonly List<string> allTargetFiles = new();
        public readonly List<string> existingTargets = new();
    }

    public sealed partial class ExportResult
    {
        public string exportRootDir;
        public readonly List<string> filesWritten = new();
        public readonly List<string> filesOverwritten = new();
    }

    public ValidationResult Validate(AgsModel.Project project)
    {
        var vr = new ValidationResult();
        ValidateInto(project, vr);
        return vr;
    }

    public ExportPlan BuildPlan(AgsModel.Project project)
    {
        var plan = new ExportPlan();
        plan.baseDefName = AgsExportUtil.MakeSafeDefName(project?.export?.baseDefName ?? project?.label ?? project?.projectId ?? "AGS_Export");
        plan.rootDir = ResolveExportRootDir();
        plan.variantIds = CollectVariantIds(project);

        // Keep exports tidy and deterministic.
        string folderKey = AgsExportUtil.MakeSafeFileName(plan.baseDefName);
        plan.groupsDir = Path.Combine(plan.rootDir, "Defs", "AnimGroupDefs", "AnimGroupStudio", folderKey);
        plan.rolesDir = Path.Combine(plan.rootDir, "Defs", "AnimRoleDefs", "AnimGroupStudio", folderKey);
        plan.animsDir = Path.Combine(plan.rootDir, "Defs", "AnimationDefs", "AnimGroupStudio", folderKey);
        plan.offsetsDir = Path.Combine(plan.rootDir, "Defs", "AnimationOffsetDefs", "AnimGroupStudio", folderKey);

        BuildTargetFiles(project, plan);
        return plan;
    }

    public ExportResult ExportAll(AgsModel.Project project, bool allowOverwrite)
    {
        var vr = Validate(project);
        if (!vr.Ok)
            throw new InvalidOperationException(string.Join("\n", vr.errors));

        // Keep project consistent.
        try { AgsCompile.RebuildPropLibrary(project); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsExport.EmptyCatch:1", "AGS export best-effort step failed and fell back.", e); }

        var plan = BuildPlan(project);
        EnsureDirs(plan);

        var result = new ExportResult { exportRootDir = plan.rootDir };

        if (!allowOverwrite && plan.existingTargets.Count > 0)
            throw new InvalidOperationException("Export would overwrite existing files. Confirmation required.");

        // Export per variation.
        for (int vi = 0; vi < plan.variantIds.Count; vi++)
        {
            string variantId = plan.variantIds[vi];
            string code = VariantIdToCode(variantId);
            string groupDefName = AgsExportUtil.MakeSafeDefName(AgsExportUtil.MakeVariationDefName(plan.baseDefName, code));

            var roleDefNames = new List<string>();

            for (int ri = 0; ri < project.roles.Count; ri++)
            {
                var role = project.roles[ri];
                if (role == null) continue;
                string roleKey = role.roleKey ?? $"role_{ri + 1}";
                string safeRoleKey = AgsExportUtil.MakeSafeDefName(roleKey);

                // Offset def per variation+role.
                string offsetDefName = $"{groupDefName}_{safeRoleKey}_Offsets";
                string offsetPath = Path.Combine(plan.offsetsDir, offsetDefName + ".xml");
                bool existedOffset = File.Exists(offsetPath);
                WriteAnimationOffsetDef(project, roleKey, offsetDefName, offsetPath);
                result.filesWritten.Add(offsetPath);
                if (existedOffset) result.filesOverwritten.Add(offsetPath);

                // AnimationDefs list in stage order.
                var animDefNames = new List<string>();
                for (int si = 0; si < project.stages.Count; si++)
                {
                    var stage = project.stages[si];
                    int dur = Mathf.Max(1, stage?.durationTicks ?? 1);
                    var clip = GetClip(stage, variantId, roleKey);
                    if (clip == null)
                        clip = new AgsModel.ClipSpec { lengthTicks = dur, tracks = new List<AgsModel.Track>() };

                    string animDefName = $"{groupDefName}_S{si}_{safeRoleKey}";
                    string animPath = Path.Combine(plan.animsDir, animDefName + ".xml");
                    bool existedAnim = File.Exists(animPath);
                    WriteAnimationDefXml(clip, animDefName, dur, animPath);
                    result.filesWritten.Add(animPath);
                    if (existedAnim) result.filesOverwritten.Add(animPath);
                    animDefNames.Add(animDefName);
                }

                // Role def.
                string roleDefName = $"{groupDefName}_{safeRoleKey}";
                string rolePath = Path.Combine(plan.rolesDir, roleDefName + ".xml");
                bool existedRole = File.Exists(rolePath);
                WriteAnimRoleDefXml(role, roleDefName, animDefNames, offsetDefName, rolePath);
                result.filesWritten.Add(rolePath);
                if (existedRole) result.filesOverwritten.Add(rolePath);
                roleDefNames.Add(roleDefName);
            }

            // Group def.
            string groupPath = Path.Combine(plan.groupsDir, groupDefName + ".xml");
            bool existedGroup = File.Exists(groupPath);
            WriteAnimGroupDefXml(project, groupDefName, roleDefNames, groupPath);
            result.filesWritten.Add(groupPath);
            if (existedGroup) result.filesOverwritten.Add(groupPath);
        }

        return result;
    }

    // Validation ----------------------------------------------------------


    // XML writing ---------------------------------------------------------


    // Helpers -------------------------------------------------------------


}
