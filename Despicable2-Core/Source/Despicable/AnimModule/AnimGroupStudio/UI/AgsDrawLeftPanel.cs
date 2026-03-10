using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.AnimGroupStudio.Preview;
using Despicable.AnimGroupStudio;
using Verse.Sound;
using Despicable.UIFramework;
using Despicable.UIFramework.Controls;
using Despicable.UIFramework.Layout;
using Despicable.AnimModule.AnimGroupStudio.Model;

namespace Despicable.AnimModule.AnimGroupStudio.UI;
public partial class Dialog_AnimGroupStudio
{
            private void DrawAuthorLeft(Rect rect)
    {
        var ctx = frameworkCtx;
        var parts = D2Section.Layout(ctx, rect, new D2Section.Spec("AuthorLeft", headerHeight: SectionHeaderHeight, soft: true, pad: true, drawBackground: true, padOverride: ctx.Style.Pad));
        string headerTitle = sourceMode == SourceMode.ExistingDef ? "Browse Existing" : "Project & Structure";
        D2Section.DrawCaptionStrip(ctx, parts.Header, headerTitle, "AuthorLeft/Header", GameFont.Medium);

        Rect scrollRect = parts.Body;

        var authorLeftScrollLocal = authorLeftScroll;
        var authorLeftContentHeightLocal = authorLeftContentHeight;
        D2ScrollView.Draw(ctx, scrollRect, ref authorLeftScrollLocal, ref authorLeftContentHeightLocal, (UIContext scrollCtx, ref VStack v) =>
        {
            if (DrawAuthorSourceSection(scrollCtx, ref v))
                return;

            if (!DrawAuthorProjectSection(scrollCtx, ref v))
                return;

            DrawAuthorRolesSection(scrollCtx, ref v);
            DrawAuthorStagesSection(scrollCtx, ref v);
        }, label: "AuthorLeftScroll");
        authorLeftScroll = authorLeftScrollLocal;
        authorLeftContentHeight = authorLeftContentHeightLocal;
    }

    private void DrawExistingSelector(Rect inner, ref float y)
            {
                // Compact selector for built-in AnimGroupDefs (read-only).
                // This replaces the old Preview Existing tab; playback is driven by the Authoring transport controls.

                Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "AnimGroup Family");
                y += 24f;

                var famBtn = new Rect(inner.x, y, inner.width, 28f);
                var famLabel = selectedFamilyKey.NullOrEmpty() ? "(select)" : selectedFamilyKey;
                if (Widgets.ButtonText(famBtn, famLabel))
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
                y += 36f;

                if (selectedFamilyKey.NullOrEmpty() || families == null || !families.TryGetValue(selectedFamilyKey, out var fam) || fam.variationsSorted.NullOrEmpty())
                {
                    Widgets.Label(new Rect(inner.x, y, inner.width, 40f), "Pick a family to browse its variations.");
                    return;
                }

                Widgets.Label(new Rect(inner.x, y, inner.width, 22f), "Variation (AnimGroupDef)");
                y += 24f;

                var varBtn = new Rect(inner.x, y, inner.width, 28f);
                var currentVar = selectedGroup;
                var varLabel = currentVar == null ? "(select)" : currentVar.defName;
                if (Widgets.ButtonText(varBtn, varLabel))
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
                y += 36f;

                if (selectedGroup == null)
                {
                    Widgets.Label(new Rect(inner.x, y, inner.width, 40f), "Pick a variation to preview.");
                    return;
                }

                // Stage selector
                int stageCount = preview.StageCount;
                if (stageCount <= 0)
                {
                    Widgets.Label(new Rect(inner.x, y, inner.width, 40f), "This AnimGroupDef has no playable stages.");
                    return;
                }

                Widgets.Label(new Rect(inner.x, y, 60f, 22f), "Stage");
                var stageBtn = new Rect(inner.x + 64f, y, inner.width - 64f, 24f);
                string stageLabel = "Stage " + Mathf.Clamp(selectedStageIndex, 0, stageCount - 1);
                if (Widgets.ButtonText(stageBtn, stageLabel))
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
                y += 32f;

                // Quick repeat info (from loopIndex)
                int rep = 1;
                try
                {
                    if (selectedGroup?.loopIndex != null && selectedStageIndex >= 0 && selectedStageIndex < selectedGroup.loopIndex.Count)
                        rep = Mathf.Max(1, selectedGroup.loopIndex[selectedStageIndex]);
                }
                catch (Exception ex) { Despicable.Core.DebugLogger.WarnExceptionOnce("AgsDrawLeftPanel:2", "AgsDrawLeftPanel ignored a non-fatal editor exception.", ex); }
                Widgets.Label(new Rect(inner.x, y, inner.width, 22f), $"Repeat count (loopIndex): {rep}");
                y += 26f;
                // Import CTA (bottom)
                float btnH = 32f;
                var importBtn = new Rect(inner.x, inner.yMax - btnH, inner.width, btnH);
                if (Widgets.ButtonText(importBtn, "Import as new player project"))
                {
                    ImportSelectedExistingAsProject();
                }
                TooltipHandler.TipRegion(importBtn, "Creates a new mutable project based on this AnimGroupDef's roles/stages.\nBuilt-in defs stay read-only.");
            }


}
