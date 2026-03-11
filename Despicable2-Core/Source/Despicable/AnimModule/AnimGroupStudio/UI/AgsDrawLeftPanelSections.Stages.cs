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

        float rowHeight = 26f;
        float rowGap = 3f;
        float listPad = Mathf.Clamp(scrollCtx.Style.Pad * 0.45f, 4f, 8f);
        float listHeight = MeasureStageListHeight(project.stages, rowHeight, rowGap, listPad);
        Rect listOuter = v.Next(listHeight, UIRectTag.None, "Stages/ListPanel");
        DrawAuthorStageList(scrollCtx, listOuter, rowHeight, rowGap, listPad);

        var curStage = GetStage(project, authorStageIndex);

        Rect stageNameRow = v.NextRow(UIRectTag.Input, "Stages/NameRow");
        var stageNameH = new HRow(scrollCtx, stageNameRow);
        D2Widgets.Label(scrollCtx, stageNameH.NextFixed(60f, UIRectTag.Label, "Stages/NameLabel"), "Name", "Stages/NameLabel");
        if (curStage != null)
            curStage.label = D2Widgets.TextField(scrollCtx, stageNameH.Remaining(UIRectTag.TextField, "Stages/NameField"), curStage.label ?? "", 256, "Stages/NameField");
        else
            scrollCtx.Record(stageNameH.Remaining(UIRectTag.TextField, "Stages/NameFieldDisabled"), UIRectTag.TextField, "Stages/NameFieldDisabled");

        Rect actionsRow = v.NextRow(UIRectTag.Input, "Stages/Actions");
        var actionsH = new HRow(scrollCtx, actionsRow);
        float iconSize = scrollCtx.Style.RowHeight;

        if (DrawIconButton(scrollCtx, actionsH.NextFixed(iconSize, UIRectTag.Button, "Stages/Actions/Add"), D2VanillaTex.Plus, "Add stage", "Stages/Actions/Add"))
            AddAuthorStage();

        if (DrawIconButton(scrollCtx, actionsH.NextFixed(iconSize, UIRectTag.Button, "Stages/Actions/Duplicate"), D2VanillaTex.Copy, "Duplicate selected stage", "Stages/Actions/Duplicate"))
            DuplicateAuthorStage();

        bool canDeleteStage = project.stages != null && project.stages.Count > 1 && authorStageIndex >= 0 && authorStageIndex < project.stages.Count;
        Rect deleteRect = actionsH.NextFixed(iconSize, UIRectTag.Button, "Stages/Actions/Delete");
        if (canDeleteStage)
        {
            if (DrawIconButton(scrollCtx, deleteRect, D2VanillaTex.Delete, "Delete selected stage", "Stages/Actions/Delete"))
                DeleteAuthorStage();
        }
        else
        {
            DrawIconButton(scrollCtx, deleteRect, D2VanillaTex.Delete, "Delete selected stage", "Stages/Actions/DeleteDisabled", enabled: false, disabledReason: "A project needs at least one stage.");
        }

        actionsH.NextFixed(Mathf.Max(6f, scrollCtx.Style.Gap), UIRectTag.None, "Stages/Actions/Spacer");

        Rect moveUpRect = actionsH.NextFixed(iconSize, UIRectTag.Button, "Stages/Actions/MoveUp");
        if (authorStageIndex > 0)
        {
            if (DrawIconButton(scrollCtx, moveUpRect, D2VanillaTex.ReorderUp, "Move stage up", "Stages/Actions/MoveUp"))
                MoveAuthorStageUp();
        }
        else
        {
            DrawIconButton(scrollCtx, moveUpRect, D2VanillaTex.ReorderUp, "Move stage up", "Stages/Actions/MoveUpDisabled", enabled: false, disabledReason: "This stage is already first.");
        }

        bool canMoveDown = project.stages != null && authorStageIndex >= 0 && authorStageIndex < project.stages.Count - 1;
        Rect moveDownRect = actionsH.NextFixed(iconSize, UIRectTag.Button, "Stages/Actions/MoveDown");
        if (canMoveDown)
        {
            if (DrawIconButton(scrollCtx, moveDownRect, D2VanillaTex.ReorderDown, "Move stage down", "Stages/Actions/MoveDown"))
                MoveAuthorStageDown();
        }
        else
        {
            DrawIconButton(scrollCtx, moveDownRect, D2VanillaTex.ReorderDown, "Move stage down", "Stages/Actions/MoveDownDisabled", enabled: false, disabledReason: "This stage is already last.");
        }
    }

    private float MeasureStageListHeight(List<AgsModel.StageSpec> stages, float rowHeight, float rowGap, float listPad)
    {
        int count = Mathf.Max(1, stages?.Count ?? 0);
        float rowsH = (count * rowHeight) + (Mathf.Max(0, count - 1) * rowGap);
        return rowsH + (listPad * 2f);
    }

    private void DrawAuthorStageList(UIContext ctx, Rect rect, float rowHeight, float rowGap, float listPad)
    {
        using var listPanel = ctx.GroupPanel("Stages/ListPanel", rect, soft: true, pad: true, padOverride: listPad, drawBackground: true, label: "Stages/ListPanel");
        Rect inner = listPanel.Inner;
        float y = inner.yMin;

        for (int i = 0; i < project.stages.Count; i++)
        {
            var stage = project.stages[i];
            Rect rowRect = new Rect(inner.xMin, y, inner.width, rowHeight);
            bool selected = i == authorStageIndex;
            ctx.RecordRect(rowRect, selected ? UIRectTag.ListRowSelected : UIRectTag.ListRow, $"Stages/List/Row[{i}]");

            string label = BuildStageListLabel(i, stage);
            if (ctx.Pass == UIPass.Draw && SelectableRowButton(rowRect, label, selected))
                SelectAuthorStage(i);

            y += rowHeight + rowGap;
        }
    }

    private string BuildStageListLabel(int index, AgsModel.StageSpec stage)
    {
        string label = stage?.label;
        if (label.NullOrEmpty())
            label = "Stage " + index;
        return $"{index:00}  {label}";
    }

    private void SelectAuthorStage(int index)
    {
        EnsureStages(project);
        int clamped = Mathf.Clamp(index, 0, project.stages.Count - 1);
        authorStageIndex = clamped;
        authorTrackIndex = -1;
        authorKeyIndex = -1;
        MarkAuthorPreviewSelectionDirty();

        if (sourceMode == SourceMode.AuthorProject)
        {
            var stage = GetStage(project, clamped);
            int previewTick = Mathf.Clamp(authorPreviewTick, 0, Mathf.Max(1, stage?.durationTicks ?? 1));
            ShowAuthorStageAtTick(clamped, previewTick, seekIfPlaying: false);
        }
    }

    private void AddAuthorStage()
    {
        EnsureStages(project);
        int idx = project.stages.Count;
        AgsModel.StageSpec newStage;
        if (!project.stages.NullOrEmpty())
        {
            var src = project.stages[project.stages.Count - 1];
            newStage = src != null
                ? CloneStageSeededFromTerminalKeys(src)
                : new AgsModel.StageSpec { durationTicks = 60, repeatCount = 1, variants = new List<AgsModel.StageVariant>() };
        }
        else
        {
            newStage = new AgsModel.StageSpec { durationTicks = 60, repeatCount = 1, variants = new List<AgsModel.StageVariant>() };
        }

        ApplyAuthorEdit(() =>
        {
            newStage.stageIndex = idx;
            newStage.label = "Stage " + idx;
            project.stages.Add(newStage);
            authorStageIndex = idx;
            authorTrackIndex = -1;
            authorKeyIndex = -1;
        }, structureDirty: true, selectionDirty: true, dirtyStageIndex: idx);
        ShowAuthorStageAtTick(idx, 0, seekIfPlaying: false);
    }

    private void DuplicateAuthorStage()
    {
        EnsureStages(project);
        var src = GetStage(project, authorStageIndex);
        if (src == null)
            return;

        int cloneIndex = project.stages.Count;
        var clone = DeepCloneStage(src);
        ApplyAuthorEdit(() =>
        {
            clone.stageIndex = cloneIndex;
            clone.label = (src.label ?? ("Stage " + authorStageIndex)) + " Copy";
            project.stages.Add(clone);
            authorStageIndex = cloneIndex;
            authorTrackIndex = -1;
            authorKeyIndex = -1;
        }, structureDirty: true, selectionDirty: true, dirtyStageIndex: cloneIndex);
        ShowAuthorStageAtTick(cloneIndex, 0, seekIfPlaying: false);
    }

    private void DeleteAuthorStage()
    {
        EnsureStages(project);
        if (project.stages.Count <= 1 || authorStageIndex < 0 || authorStageIndex >= project.stages.Count)
            return;

        int nextIndex = Mathf.Clamp(authorStageIndex, 0, project.stages.Count - 2);
        ApplyAuthorEdit(() =>
        {
            project.stages.RemoveAt(authorStageIndex);
            for (int i = 0; i < project.stages.Count; i++)
                if (project.stages[i] != null) project.stages[i].stageIndex = i;
            authorStageIndex = nextIndex;
            authorTrackIndex = -1;
            authorKeyIndex = -1;
        }, structureDirty: true, selectionDirty: true, dirtyStageIndex: nextIndex);
        ShowAuthorStageAtTick(nextIndex, 0, seekIfPlaying: false);
    }

    private void MoveAuthorStageUp()
    {
        EnsureStages(project);
        if (authorStageIndex <= 0 || authorStageIndex >= project.stages.Count)
            return;

        int oldIndex = authorStageIndex;
        int newIndex = authorStageIndex - 1;
        ApplyAuthorEdit(() =>
        {
            var tmp = project.stages[newIndex];
            project.stages[newIndex] = project.stages[oldIndex];
            project.stages[oldIndex] = tmp;
            for (int i = 0; i < project.stages.Count; i++)
                if (project.stages[i] != null) project.stages[i].stageIndex = i;
            authorStageIndex = newIndex;
            authorTrackIndex = -1;
            authorKeyIndex = -1;
        }, structureDirty: true, selectionDirty: true, dirtyStageIndex: newIndex);
        ShowAuthorStageAtTick(newIndex, 0, seekIfPlaying: false);
    }

    private void MoveAuthorStageDown()
    {
        EnsureStages(project);
        if (authorStageIndex < 0 || authorStageIndex >= project.stages.Count - 1)
            return;

        int oldIndex = authorStageIndex;
        int newIndex = authorStageIndex + 1;
        ApplyAuthorEdit(() =>
        {
            var tmp = project.stages[newIndex];
            project.stages[newIndex] = project.stages[oldIndex];
            project.stages[oldIndex] = tmp;
            for (int i = 0; i < project.stages.Count; i++)
                if (project.stages[i] != null) project.stages[i].stageIndex = i;
            authorStageIndex = newIndex;
            authorTrackIndex = -1;
            authorKeyIndex = -1;
        }, structureDirty: true, selectionDirty: true, dirtyStageIndex: newIndex);
        ShowAuthorStageAtTick(newIndex, 0, seekIfPlaying: false);
    }
}
