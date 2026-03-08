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

    private void DrawPreviewExisting(Rect rect)
    {
                var left = new Rect(rect.x, rect.y, 420f, rect.height);
                var right = new Rect(left.xMax + 10f, rect.y, rect.width - left.width - 10f, rect.height);

                DrawPreviewControls(left);
                preview.DrawViewport(right);
            }

    private void DrawPreviewControls(Rect rect)
    {
                Widgets.DrawMenuSection(rect);
                var inner = rect.ContractedBy(10f);

                float y = inner.y;

                // PSEUDOCODE (Preview Existing UI)
                // 1) Pick a "family" (base name) from all AnimGroupDefs using SplitFamilyAndCode.
                // 2) Pick a "variation" (whole AnimGroupDef) within that family (natural-sorted).
                // 3) Pick a stage index (chronological index into AnimRoleDef.anims[]).
                // 4) Play selected stage OR from stage to end, applying role.offsetDef by body type.

                // Family picker
                Widgets.Label(new Rect(inner.x, y, inner.width, 24f), "AnimGroup Family");
                y += 26f;

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
                y += 38f;

                if (selectedFamilyKey.NullOrEmpty() || families == null || !families.TryGetValue(selectedFamilyKey, out var fam) || fam.variationsSorted.NullOrEmpty())
                {
                    Widgets.Label(new Rect(inner.x, y, inner.width, 48f), "Select a family to preview its variations.");
                    return;
                }

                // Variation picker
                Widgets.Label(new Rect(inner.x, y, inner.width, 24f), "Variation (AnimGroupDef)");
                y += 26f;

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
                y += 38f;

                if (selectedGroup == null)
                {
                    Widgets.Label(new Rect(inner.x, y, inner.width, 48f), "Select a variation to preview.");
                    return;
                }

                // Stage selector (index-based)
                int stageCount = preview.StageCount;
                if (stageCount <= 0)
                {
                    Widgets.Label(new Rect(inner.x, y, inner.width, 48f), "This AnimGroupDef has no playable stages.");
                    return;
                }

                Widgets.Label(new Rect(inner.x, y, 90f, 24f), "Stage:");
                var stageBtn = new Rect(inner.x + 95f, y, inner.width - 95f, 24f);
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
                y += 28f;

                // Loop toggle
                // Loop toggle
                var loopRect = new Rect(inner.x, y, inner.width, 24f);
                var loopCurrentStageLocal = loopCurrentStage;
                Widgets.CheckboxLabeled(loopRect, "Loop selected stage", ref loopCurrentStageLocal);
                loopCurrentStage = loopCurrentStageLocal;
                preview.LoopCurrentStage = loopCurrentStage;
                y += 34f;

                // Playback buttons
                var btnRow = new Rect(inner.x, inner.yMax - 96f, inner.width, 90f);
                DrawPlaybackButtons(btnRow);
            }
}
