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
        DrawGroupedHeader(scrollCtx, ref v, "Left/Existing", "Browse Existing", topPadding: true);
        D2Widgets.Label(scrollCtx, v.NextLine(UIRectTag.Label, "Existing/FamilyLabel"), "AnimGroup Family", "Existing/FamilyLabel");
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
            if (opts.Count == 0) opts.Add(new FloatMenuOption("(none)", null));
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
                if (def == null) continue;
                int idx = i;
                opts.Add(new FloatMenuOption(def.defName, () =>
                {
                    fam.selectedVariationIndex = idx;
                    SelectGroup(def);
                }));
            }
            if (opts.Count == 0) opts.Add(new FloatMenuOption("(none)", null));
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

        Rect stageRow = v.NextRow(UIRectTag.Input, "Existing/StageRow");
        var stageH = new HRow(scrollCtx, stageRow);
        D2Widgets.Label(scrollCtx, stageH.NextFixed(60f, UIRectTag.Label, "Existing/StageLabel"), "Stage", "Existing/StageLabel");
        string stageLabel = "Stage " + Mathf.Clamp(selectedStageIndex, 0, stageCount - 1);
        if (D2Widgets.ButtonText(scrollCtx, stageH.Remaining(UIRectTag.Input, "Existing/StageButton"), stageLabel, "Existing/StageButton"))
        {
            var opts = new List<FloatMenuOption>();
            for (int i = 0; i < stageCount; i++)
            {
                int idx = i;
                opts.Add(new FloatMenuOption("Stage " + idx, () =>
                {
                    selectedStageIndex = idx;
                    preview.SelectedStageIndex = idx;
                }));
            }
            Find.WindowStack.Add(new FloatMenu(opts));
        }

        int rep = 1;
        try
        {
            if (selectedGroup?.loopIndex != null && selectedStageIndex >= 0 && selectedStageIndex < selectedGroup.loopIndex.Count)
                rep = Mathf.Max(1, selectedGroup.loopIndex[selectedStageIndex]);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("AgsDrawLeftPanel:1", "AgsDrawLeftPanel ignored a non-fatal editor exception.", ex);
        }
        v.NextTextBlock(scrollCtx, $"Repeat count (loopIndex): {rep}", GameFont.Small, padding: 2f, label: "Existing/RepeatInfo");

        if (D2Widgets.ButtonText(scrollCtx, v.NextButton(UIRectTag.Button, "Existing/ImportButton"), "Import as new player project", "Existing/ImportButton"))
            ImportSelectedExistingAsProject();
    }
}
