using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
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
/// Output package:
/// - GroupAnimation_<ProjectKey>.xml (AnimGroupDef + AnimRoleDef entries)
/// - OffsetDefs_<ProjectKey>.xml     (AnimationOffsetDef entries)
/// - Stages/*.xml                    (AnimationDef entries, grouped by stage slice)
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
        public string projectKey;
        public List<string> variantIds;

        public string packageDir;
        public string stagesDir;
        public string groupFilePath;
        public string offsetFilePath;

        public readonly List<StageTarget> stageTargets = new();
        public readonly List<string> allTargetFiles = new();
        public readonly List<string> existingTargets = new();
    }

    public sealed partial class StageTarget
    {
        public string variantId;
        public int stageIndex;
        public string filePath;
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
        plan.projectKey = AgsExportUtil.MakeExportProjectKey(plan.baseDefName);
        plan.rootDir = ResolveExportRootDir();
        plan.variantIds = CollectVariantIds(project);

        plan.packageDir = Path.Combine(plan.rootDir, "Defs", "LovinModule", "Animations", "Exported", "Groups", plan.projectKey);
        plan.stagesDir = Path.Combine(plan.packageDir, "Stages");
        plan.groupFilePath = Path.Combine(plan.packageDir, AgsExportUtil.MakeGroupPackageFileName(plan.projectKey));
        plan.offsetFilePath = Path.Combine(plan.packageDir, AgsExportUtil.MakeOffsetPackageFileName(plan.projectKey));

        BuildTargetFiles(project, plan);
        return plan;
    }

    public ExportResult ExportAll(AgsModel.Project project, bool allowOverwrite)
    {
        var vr = Validate(project);
        if (!vr.Ok)
            throw new InvalidOperationException(string.Join("\n", vr.errors));

        try { AgsCompile.RebuildPropLibrary(project); } catch (System.Exception e) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsExport.EmptyCatch:1", "AGS export best-effort step failed and fell back.", e); }

        var plan = BuildPlan(project);
        EnsureDirs(plan);

        var result = new ExportResult { exportRootDir = plan.packageDir };

        if (!allowOverwrite && plan.existingTargets.Count > 0)
            throw new InvalidOperationException("Export would overwrite existing files. Confirmation required.");

        WriteTracked(plan.groupFilePath, result, path => WriteGroupPackageXml(project, plan, path));
        WriteTracked(plan.offsetFilePath, result, path => WriteOffsetPackageXml(project, plan, path));

        for (int i = 0; i < plan.stageTargets.Count; i++)
        {
            var target = plan.stageTargets[i];
            if (target == null || target.filePath.NullOrEmpty())
                continue;

            string variantId = target.variantId.NullOrEmpty() ? "Base" : target.variantId;
            int stageIndex = Mathf.Clamp(target.stageIndex, 0, Mathf.Max(0, project.stages.Count - 1));
            WriteTracked(target.filePath, result, path => WriteStagePackageXml(project, plan, variantId, stageIndex, path));
        }

        return result;
    }

    private static void WriteTracked(string path, ExportResult result, Action<string> write)
    {
        bool existed = File.Exists(path);
        write(path);
        result.filesWritten.Add(path);
        if (existed)
            result.filesOverwritten.Add(path);
    }
}
