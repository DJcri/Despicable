using RimWorld;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using Despicable.UIFramework;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
public partial class Dialog_AnimGroupStudio
{

    private void DrawAuthorStagesSection(UIContext scrollCtx, ref VStack v)
    {
        DrawGroupedHeader(scrollCtx, ref v, "Left/Stages", "Stages", topPadding: true);
        EnsureStages(project);
        authorStageIndex = Mathf.Clamp(authorStageIndex, 0, project.stages.Count - 1);
        var curStage = GetStage(project, authorStageIndex);
        string curStageLabel = curStage?.label ?? ("Stage " + authorStageIndex);
        if (D2Widgets.ButtonText(scrollCtx, v.NextRow(UIRectTag.Button, "Stages/Picker"), $"{authorStageIndex}: {curStageLabel}", "Stages/Picker"))
        {
            var opts = new List<FloatMenuOption>();
            for (int i = 0; i < project.stages.Count; i++)
    {
                int idx = i;
                var s = project.stages[i];
                if (s == null) continue;
                string lbl = $"{idx}: {s.label ?? ("Stage " + idx)}";
                opts.Add(new FloatMenuOption(lbl, () =>
                {
                    authorStageIndex = idx;
                    authorTrackIndex = -1;
                    authorKeyIndex = -1;
                }));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("(none)", null));
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        Rect stageNameRow = v.NextRow(UIRectTag.Input, "Stages/NameRow");
        var stageNameH = new HRow(scrollCtx, stageNameRow);
        D2Widgets.Label(scrollCtx, stageNameH.NextFixed(60f, UIRectTag.Label, "Stages/NameLabel"), "Name", "Stages/NameLabel");
        if (curStage != null)
            curStage.label = D2Widgets.TextField(scrollCtx, stageNameH.Remaining(UIRectTag.TextField, "Stages/NameField"), curStage.label ?? "", 256, "Stages/NameField");
        else
            scrollCtx.Record(stageNameH.Remaining(UIRectTag.TextField, "Stages/NameFieldDisabled"), UIRectTag.TextField, "Stages/NameFieldDisabled");

        var stageActions1 = new List<D2ActionBar.Item>
        {
            new D2ActionBar.Item("AddStage", "+") { MinWidthOverride = 56f },
            new D2ActionBar.Item("DuplicateStage", "Duplicate") { MinWidthOverride = 98f },
            new D2ActionBar.Item("DeleteStage", "Delete")
    {
                MinWidthOverride = 88f,
                Disabled = project.stages == null || project.stages.Count <= 1,
                DisabledReason = "A project needs at least one stage."
            },
        };
        float stageActions1H = D2ActionBar.MeasureHeight(scrollCtx, new Rect(0f, 0f, v.Bounds.width, 9999f), stageActions1);
        var stageActions1Res = D2ActionBar.Draw(scrollCtx, v.Next(stageActions1H, UIRectTag.Input, "Stages/Actions1"), stageActions1, "Stages/Actions1");
        if (stageActions1Res.Clicked)
        {
            switch (stageActions1Res.ActivatedId)
    {
                case "AddStage":
                {
                    EnsureStages(project);
                    int idx = project.stages.Count;
                    project.stages.Add(new AgsModel.StageSpec { stageIndex = idx, label = "Stage " + idx, durationTicks = 60, repeatCount = 1, variants = new List<AgsModel.StageVariant>() });
                    authorStageIndex = idx;
                    authorTrackIndex = -1;
                    authorKeyIndex = -1;
                    TrySaveProjects();
                    break;
                }
                case "DuplicateStage":
                {
                    EnsureStages(project);
                    var src = GetStage(project, authorStageIndex);
                    if (src != null)
                    {
                        var clone = DeepCloneStage(src);
                        clone.stageIndex = project.stages.Count;
                        clone.label = (src.label ?? ("Stage " + authorStageIndex)) + " Copy";
                        project.stages.Add(clone);
                        authorStageIndex = project.stages.Count - 1;
                        authorTrackIndex = -1;
                        authorKeyIndex = -1;
                        TrySaveProjects();
                    }
                    break;
                }
                case "DeleteStage":
                {
                    EnsureStages(project);
                    if (project.stages.Count > 1 && authorStageIndex >= 0 && authorStageIndex < project.stages.Count)
                    {
                        project.stages.RemoveAt(authorStageIndex);
                        for (int i = 0; i < project.stages.Count; i++)
                            if (project.stages[i] != null) project.stages[i].stageIndex = i;
                        authorStageIndex = Mathf.Clamp(authorStageIndex, 0, project.stages.Count - 1);
                        authorTrackIndex = -1;
                        authorKeyIndex = -1;
                        TrySaveProjects();
                    }
                    break;
                }
            }
        }

        var stageActions2 = new List<D2ActionBar.Item>
        {
            new D2ActionBar.Item("MoveUp", "Move up")
    {
                MinWidthOverride = 96f,
                Disabled = authorStageIndex <= 0,
                DisabledReason = "This stage is already first."
            },
            new D2ActionBar.Item("MoveDown", "Move down")
    {
                MinWidthOverride = 96f,
                Disabled = project.stages == null || authorStageIndex < 0 || authorStageIndex >= project.stages.Count - 1,
                DisabledReason = "This stage is already last."
            },
        };
        float stageActions2H = D2ActionBar.MeasureHeight(scrollCtx, new Rect(0f, 0f, v.Bounds.width, 9999f), stageActions2);
        var stageActions2Res = D2ActionBar.Draw(scrollCtx, v.Next(stageActions2H, UIRectTag.Input, "Stages/Actions2"), stageActions2, "Stages/Actions2");
        if (stageActions2Res.Clicked)
        {
            if (stageActions2Res.ActivatedId == "MoveUp")
    {
                EnsureStages(project);
                if (authorStageIndex > 0 && authorStageIndex < project.stages.Count)
                {
                    var tmp = project.stages[authorStageIndex - 1];
                    project.stages[authorStageIndex - 1] = project.stages[authorStageIndex];
                    project.stages[authorStageIndex] = tmp;
                    for (int i = 0; i < project.stages.Count; i++)
                        if (project.stages[i] != null) project.stages[i].stageIndex = i;
                    authorStageIndex = Mathf.Max(0, authorStageIndex - 1);
                    TrySaveProjects();
                }
            }
            else if (stageActions2Res.ActivatedId == "MoveDown")
    {
                EnsureStages(project);
                if (authorStageIndex >= 0 && authorStageIndex < project.stages.Count - 1)
                {
                    var tmp = project.stages[authorStageIndex + 1];
                    project.stages[authorStageIndex + 1] = project.stages[authorStageIndex];
                    project.stages[authorStageIndex] = tmp;
                    for (int i = 0; i < project.stages.Count; i++)
                        if (project.stages[i] != null) project.stages[i].stageIndex = i;
                    authorStageIndex = Mathf.Min(project.stages.Count - 1, authorStageIndex + 1);
                    TrySaveProjects();
                }
            }
        }
    }
}
