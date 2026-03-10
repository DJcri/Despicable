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
    private bool DrawAuthorSourceSection(UIContext scrollCtx, ref VStack v)
    {
        if (sourceMode != SourceMode.ExistingDef)
            return false;

        DrawAuthorExistingSection(scrollCtx, ref v);
        return true;
    }

    private void DrawAuthorExistingSection(UIContext scrollCtx, ref VStack v)
    {
        D2Widgets.Label(scrollCtx, v.NextLine(UIRectTag.Label, "Existing/FamilyLabel"), "Project", "Existing/FamilyLabel");
        string famLabel = selectedFamilyKey.NullOrEmpty() ? "(select)" : selectedFamilyKey;
        if (D2Widgets.ButtonText(scrollCtx, v.NextRow(UIRectTag.Input, "Existing/FamilyButton"), famLabel, "Existing/FamilyButton"))
        {
            var opts = new List<FloatMenuOption>();
            if (familyKeysSorted != null)
            {
                foreach (var key in familyKeysSorted)
                {
                    string captured = key;
                    opts.Add(new FloatMenuOption(captured, () => SelectFamily(captured)));
                }
            }

            if (opts.Count == 0)
                opts.Add(new FloatMenuOption("(none)", null));

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        if (selectedFamilyKey.NullOrEmpty() || families == null || !families.TryGetValue(selectedFamilyKey, out var fam) || fam.variationsSorted.NullOrEmpty())
        {
            v.NextTextBlock(scrollCtx, "Pick a family to browse its variations.", GameFont.Small, padding: 2f, label: "Existing/NoFamily");
            return;
        }

        D2Widgets.Label(scrollCtx, v.NextLine(UIRectTag.Label, "Existing/VariationLabel"), "Variation (AnimGroupDef)", "Existing/VariationLabel");
        string varLabel = selectedGroup == null ? "(select)" : selectedGroup.defName;
        if (D2Widgets.ButtonText(scrollCtx, v.NextRow(UIRectTag.Input, "Existing/VariationButton"), varLabel, "Existing/VariationButton"))
        {
            var opts = new List<FloatMenuOption>();
            for (int i = 0; i < fam.variationsSorted.Count; i++)
            {
                var def = fam.variationsSorted[i];
                if (def == null)
                    continue;

                int idx = i;
                opts.Add(new FloatMenuOption(def.defName, () =>
                {
                    fam.selectedVariationIndex = idx;
                    SelectGroup(def);
                }));
            }

            if (opts.Count == 0)
                opts.Add(new FloatMenuOption("(none)", null));

            Find.WindowStack.Add(new FloatMenu(opts));
        }

        if (selectedGroup == null)
        {
            v.NextTextBlock(scrollCtx, "Pick a variation to preview.", GameFont.Small, padding: 2f, label: "Existing/NoVariation");
            return;
        }

        int stageCount = preview.StageCount;
        if (stageCount <= 0)
        {
            v.NextTextBlock(scrollCtx, "This AnimGroupDef has no playable stages.", GameFont.Small, padding: 2f, label: "Existing/NoStages");
            return;
        }

        D2Widgets.Label(scrollCtx, v.NextLine(UIRectTag.Label, "Existing/StagesLabel"), "Stages", "Existing/StagesLabel");

        float rowHeight = 26f;
        float rowGap = 3f;
        float listPad = Mathf.Clamp(scrollCtx.Style.Pad * 0.45f, 4f, 8f);
        float listHeight = MeasureExistingStageListHeight(stageCount, rowHeight, rowGap, listPad);
        Rect listRect = v.Next(listHeight, UIRectTag.None, "Existing/StagesListPanel");
        DrawExistingStageList(scrollCtx, listRect, stageCount, rowHeight, rowGap, listPad);

        int stageNumber = Mathf.Clamp(selectedStageIndex, 0, Mathf.Max(0, stageCount - 1));
        int rep = GetExistingStageRepeatCount(stageNumber);
        v.NextTextBlock(scrollCtx, $"Stage {stageNumber:00} repeat count: {rep}", GameFont.Small, padding: 2f, label: "Existing/RepeatInfo");
        v.NextTextBlock(scrollCtx, "While playback is running, the selected stage follows the stage currently being previewed.", GameFont.Small, padding: 2f, label: "Existing/SyncHint");

        if (D2Widgets.ButtonText(scrollCtx, v.NextButton(UIRectTag.Button, "Existing/ImportButton"), "Import as new player project", "Existing/ImportButton"))
            ImportSelectedExistingAsProject();
    }

    private float MeasureExistingStageListHeight(int stageCount, float rowHeight, float rowGap, float listPad)
    {
        int clampedCount = Mathf.Max(1, stageCount);
        float rowsHeight = (clampedCount * rowHeight) + (Mathf.Max(0, clampedCount - 1) * rowGap);
        return rowsHeight + (listPad * 2f);
    }

    private void DrawExistingStageList(UIContext ctx, Rect rect, int stageCount, float rowHeight, float rowGap, float listPad)
    {
        using var listPanel = ctx.GroupPanel("Existing/StagesListPanel", rect, soft: true, pad: true, padOverride: listPad, drawBackground: true, label: "Existing/StagesListPanel");
        Rect inner = listPanel.Inner;
        float y = inner.yMin;

        for (int i = 0; i < stageCount; i++)
        {
            Rect rowRect = new Rect(inner.xMin, y, inner.width, rowHeight);
            bool selected = i == Mathf.Clamp(selectedStageIndex, 0, stageCount - 1);
            ctx.RecordRect(rowRect, selected ? UIRectTag.ListRowSelected : UIRectTag.ListRow, $"Existing/Stages/List/Row[{i}]");

            if (ctx.Pass == UIPass.Draw && SelectableRowButton(rowRect, BuildExistingStageListLabel(i), selected))
                SelectExistingStage(i);

            y += rowHeight + rowGap;
        }
    }

    private string BuildExistingStageListLabel(int index)
    {
        return $"{index:00}  Stage {index}";
    }

    private void SelectExistingStage(int index)
    {
        int clamped = Mathf.Clamp(index, 0, Mathf.Max(0, preview.StageCount - 1));
        selectedStageIndex = clamped;
        preview.SelectedStageIndex = clamped;

        if (!preview.IsPlaying)
            preview.ShowSelectedStageAtTick(0);
    }

    private int GetExistingStageRepeatCount(int index)
    {
        int rep = 1;
        try
        {
            if (selectedGroup?.loopIndex != null && index >= 0 && index < selectedGroup.loopIndex.Count)
                rep = Mathf.Max(1, selectedGroup.loopIndex[index]);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("AgsDrawLeftPanel:ExistingRepeat", "AgsDrawLeftPanel ignored a non-fatal editor exception.", ex);
        }

        return rep;
    }
}
