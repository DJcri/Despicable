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
    private void DrawAuthorData(Rect rect)
    {
        var ctx = frameworkCtx;
        if (project == null)
        {
            var parts = D2Section.Layout(ctx, rect, new D2Section.Spec("AuthorDataEmpty", headerHeight: SectionHeaderHeight, soft: true, pad: true, drawBackground: true, padOverride: ctx.Style.Pad));
            D2Section.DrawCaptionStrip(ctx, parts.Header, "Animation Data", "AuthorDataEmpty/Header", GameFont.Medium);
            D2Widgets.Label(ctx, parts.Body, "No project loaded.", "AuthorDataEmpty/NoProject");
            return;
        }

        var stage = GetStage(project, authorStageIndex);
        if (stage == null)
        {
            var parts = D2Section.Layout(ctx, rect, new D2Section.Spec("AuthorDataNoStage", headerHeight: SectionHeaderHeight, soft: true, pad: true, drawBackground: true, padOverride: ctx.Style.Pad));
            D2Section.DrawCaptionStrip(ctx, parts.Header, "Animation Data", "AuthorDataNoStage/Header", GameFont.Medium);
            D2Widgets.Label(ctx, parts.Body, "No stage selected.", "AuthorDataNoStage/NoStage");
            return;
        }

        float paneGap = ctx.Style.Gap * 1.15f;
        float stageSettingsHeight = MeasureStageSettingsPanelHeight(rect.width);
        var panes = D2PaneLayout.Rows(
            ctx,
            rect,
            new[]
            {
                new D2PaneLayout.PaneSpec("StageSettings", stageSettingsHeight, stageSettingsHeight, 0f, canCollapse: false, priority: 0),
                new D2PaneLayout.PaneSpec("Tracks", 172f, 208f, 1.0f, canCollapse: false, priority: 0),
                new D2PaneLayout.PaneSpec("Keys", 166f, 202f, 1.15f, canCollapse: false, priority: 0),
            },
            gap: paneGap,
            fallback: D2PaneLayout.FallbackMode.None,
            label: "Author/DataPanes");

        DrawStageSettings(panes.Rects[0], stage);
        DrawTrackList(panes.Rects[1], stage);
        DrawKeyframeList(panes.Rects[2], stage);
    }

    private void DrawAuthorInspectorColumn(Rect rect)
    {
        var stage = project != null ? GetStage(project, authorStageIndex) : null;
        if (stage == null)
        {
            var ctx = frameworkCtx;
            var parts = D2Section.Layout(ctx, rect, new D2Section.Spec("AuthorInspectorColumn", headerHeight: SectionHeaderHeight, soft: true, pad: true, drawBackground: true, padOverride: ctx.Style.Pad));
            D2Section.DrawCaptionStrip(ctx, parts.Header, "Inspector", "AuthorInspectorColumn/Header", GameFont.Medium);
            D2Widgets.Label(ctx, parts.Body, project == null ? "No project loaded." : "No stage selected.", "AuthorInspectorColumn/Empty");
            return;
        }

        DrawKeyframeInspector(rect, stage);
    }

    private float MeasureStageSettingsPanelHeight(float width)
    {
        var ctx = frameworkCtx ?? new UIContext(StudioUiStyle, null, nameof(Dialog_AnimGroupStudio), UIPass.Draw);
        float pad = ctx.Style.Pad;
        float innerW = Mathf.Max(0f, width - (pad * 2f));
        float bodyRows = D2LayoutHelpers.MeasureRows(ctx.Style.RowHeight, 2, ctx.Style.Gap);
        float hintH = D2LayoutHelpers.MeasureWrappedText(ctx, "1 = normal repeat. Enable ∞ to keep repeating this stage until the next one.", innerW, GameFont.Small, 0f, ctx.Style.Line);

        // Tags section: header row + chip flow area (estimate 2 chip rows max) + add-button row
        float tagSectionH = ctx.Style.Gap + ctx.Style.RowHeight // "Stage Tags" header row
                          + ctx.Style.Gap + (ctx.Style.Line + ctx.Style.Gap) * 2f // chip area (2 lines)
                          + ctx.Style.Gap + ctx.Style.RowHeight; // "+ Add Tag" button row

        return (pad * 2f) + SubsectionHeaderHeight + ctx.Style.Gap + bodyRows + ctx.Style.Gap + hintH + tagSectionH;
    }


    private void DrawStageSettings(Rect rect, AgsModel.StageSpec stage)
    {
        var ctx = frameworkCtx;
        var parts = D2Section.Layout(
            ctx,
            rect,
            new D2Section.Spec(
                "StageSettings",
                headerHeight: SubsectionHeaderHeight,
                soft: true,
                pad: true,
                drawBackground: true,
                padOverride: ctx.Style.Pad));
        D2Section.DrawCaptionStrip(ctx, parts.Header, "Stage Settings", "StageSettings/Header", GameFont.Small);

        var v = ctx.VStack(parts.Body, label: "StageSettings/Body");
        float labelWidth = Mathf.Clamp(parts.Body.width * 0.34f, 70f, 94f);
        float rowGap = ctx.Style.Gap;

        int oldDuration = Mathf.Max(1, stage.durationTicks);
        int oldRepeat = Mathf.Max(1, stage.repeatCount);

        Rect durRow = v.NextRow(UIRectTag.Input, "StageSettings/DurationRow");
        D2LayoutHelpers.SplitLabeledRow(durRow, labelWidth, rowGap, out var durLabelRect, out var durFieldRect);
        D2Widgets.Label(ctx, durLabelRect, "Duration", "StageSettings/DurationLabel");
        int durVal = oldDuration;
        string durStr = durVal.ToString();
        ctx.Record(durFieldRect, UIRectTag.TextField, "StageSettings/DurationField");
        if (ctx.Pass == UIPass.Draw)
            Widgets.TextFieldNumeric(durFieldRect, ref durVal, ref durStr, 1, 60000);
        stage.durationTicks = durVal;

        Rect repRow = v.NextRow(UIRectTag.Input, "StageSettings/RepeatRow");
        D2LayoutHelpers.SplitLabeledRow(repRow, labelWidth, rowGap, out var repLabelRect, out var repControlsRect);
        D2Widgets.Label(ctx, repLabelRect, "Repeat", "StageSettings/RepeatLabel");

        var repH = new HRow(ctx, repControlsRect);
        int rep = Mathf.Max(1, stage.repeatCount);
        string repStr = rep.ToString();
        float toggleWidth = Mathf.Clamp(ctx.Style.MinClickSize + 28f, 50f, 60f);
        float fieldWidth = Mathf.Clamp(repControlsRect.width - toggleWidth - rowGap, 68f, 104f);
        var repRect = repH.NextFixed(fieldWidth, UIRectTag.TextField, "StageSettings/RepeatField");
        ctx.Record(repRect, UIRectTag.TextField, "StageSettings/RepeatField");
        if (ctx.Pass == UIPass.Draw)
            Widgets.TextFieldNumeric(repRect, ref rep, ref repStr, 1, 999999);
        stage.repeatCount = rep;

        bool inf = stage.repeatCount >= 999999;
        Rect infRect = repH.Remaining(UIRectTag.Checkbox, "StageSettings/InfiniteToggle");
        D2Widgets.CheckboxLabeledClipped(ctx, infRect, "∞", ref inf, checkboxWidth: 24f, label: "StageSettings/InfiniteToggle");
        if (inf) stage.repeatCount = 999999;
        else if (stage.repeatCount >= 999999) stage.repeatCount = 1;

        v.NextTextBlock(ctx, "1 = normal repeat. Enable ∞ to keep repeating this stage until the next one.", GameFont.Small, padding: 0f, label: "StageSettings/RepeatHint");

        // ── Stage Tags ──────────────────────────────────────────────────────────
        v.NextSpace(ctx.Style.Gap);
        Rect tagsHeaderRow = v.NextRow(UIRectTag.Label, "StageSettings/TagsHeader");
        D2Widgets.Label(ctx, tagsHeaderRow, "Stage Tags", "StageSettings/TagsLabel");

        if (stage.stageTags == null)
            stage.stageTags = new List<string>();

        // Chip area: render existing tags as [tag ×] chips using HFlow.
        float chipLineH = ctx.Style.Line;
        float chipGap = Mathf.Max(3f, ctx.Style.Gap * 0.5f);
        int tagCount = stage.stageTags.Count;
        // Measure how many flow lines the chips will occupy.
        float chipAreaWidth = parts.Body.width;
        float chipAreaH;
        if (tagCount == 0)
        {
            chipAreaH = chipLineH;
        }
        else
        {
            // Estimate chip width: text width + padding for the × button.
            float curX = 0f;
            int lineCount = 1;
            float closeW = chipLineH; // × button is square
            float chipPadX = 6f;
            GameFont prev = Text.Font;
            Text.Font = GameFont.Small;
            for (int ti = 0; ti < tagCount; ti++)
            {
                string t = stage.stageTags[ti] ?? "";
                float chipW = Mathf.Min(Text.CalcSize(t).x + chipPadX * 2f + closeW + chipGap, chipAreaWidth);
                if (curX > 0f && curX + chipW > chipAreaWidth)
                {
                    lineCount++;
                    curX = 0f;
                }
                curX += chipW + chipGap;
            }
            Text.Font = prev;
            chipAreaH = lineCount * chipLineH + Mathf.Max(0, lineCount - 1) * chipGap;
        }

        Rect chipAreaRect = v.Next(chipAreaH, UIRectTag.None, "StageSettings/TagChips");

        bool tagsDirty = false;
        if (ctx.Pass == UIPass.Draw)
        {
            if (tagCount == 0)
            {
                var prevColor = GUI.color;
                GUI.color = Color.gray;
                Widgets.Label(chipAreaRect, "(none)");
                GUI.color = prevColor;
            }
            else
            {
                float closeW = chipLineH;
                float chipPadX = 6f;
                var flow = new HFlow(ctx, chipAreaRect, chipLineH, chipGap);
                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Small;
                int removeAt = -1;
                for (int ti = 0; ti < tagCount; ti++)
                {
                    string t = stage.stageTags[ti] ?? "";
                    float chipW = Mathf.Min(Text.CalcSize(t).x + chipPadX * 2f + closeW + chipGap, chipAreaRect.width);
                    Rect chipRect = flow.Next(chipW);

                    // Chip background
                    Widgets.DrawBoxSolid(chipRect, new Color(0.25f, 0.25f, 0.25f, 0.85f));

                    // Tag label
                    Rect labelChipRect = new Rect(chipRect.xMin + chipPadX, chipRect.yMin, chipRect.width - chipPadX - closeW, chipRect.height);
                    Widgets.Label(labelChipRect, t);

                    // × button
                    Rect closeRect = new Rect(chipRect.xMax - closeW, chipRect.yMin, closeW, chipRect.height);
                    TooltipHandler.TipRegion(closeRect, "Remove tag");
                    if (Widgets.ButtonImage(closeRect, D2VanillaTex.CloseX))
                        removeAt = ti;
                }
                Text.Font = prevFont;
                if (removeAt >= 0)
                {
                    stage.stageTags.RemoveAt(removeAt);
                    tagsDirty = true;
                }
            }
        }

        // "+ Add Tag" button row
        Rect addTagRow = v.NextRow(UIRectTag.Button, "StageSettings/AddTagRow");
        var addTagH = new HRow(ctx, addTagRow);
        Rect addTagBtnRect = addTagH.NextFixed(ctx.Style.RowHeight, UIRectTag.Button, "StageSettings/AddTagBtn");
        if (DrawIconButton(ctx, addTagBtnRect, D2VanillaTex.Plus, "Add stage tag", "StageSettings/AddTagBtn") && ctx.Pass == UIPass.Draw)
        {
            // Offer a text-input float menu for a new freeform tag.
            // We open a small dialog to enter the tag name.
            var currentStage = stage;
            Find.WindowStack.Add(new Dialog_AgsAddTag(newTag =>
            {
                if (newTag.NullOrEmpty()) return;
                if (currentStage.stageTags == null) currentStage.stageTags = new List<string>();
                string trimmed = newTag.Trim();
                if (!trimmed.NullOrEmpty() && !currentStage.stageTags.Contains(trimmed))
                {
                    currentStage.stageTags.Add(trimmed);
                    TrySaveProjects();
                }
            }));
        }

        if (tagsDirty)
            TrySaveProjects();

        if (stage.durationTicks != oldDuration || stage.repeatCount != oldRepeat)
            TrySaveProjects();
    }

    private void DrawExistingInfo(Rect rect)
    {
        var ctx = frameworkCtx;
        var parts = D2Section.Layout(ctx, rect, new D2Section.Spec("ExistingInfo", headerHeight: SectionHeaderHeight, soft: true, pad: true, drawBackground: true, padOverride: ctx.Style.Pad));
        D2Section.DrawCaptionStrip(ctx, parts.Header, "Selection Details", "ExistingInfo/Header", GameFont.Medium);

        var v = ctx.VStack(parts.Body, label: "ExistingInfo/Body");
        if (selectedGroup == null)
        {
            v.NextTextBlock(ctx, "Read-only preview of an existing AnimGroupDef. Select one on the left, then import it there if you want a mutable project.", GameFont.Small, padding: 2f, label: "ExistingInfo/Empty");
            return;
        }

        v.NextTextBlock(ctx, "AnimGroupDef", GameFont.Small, padding: 0f, label: "ExistingInfo/DefNameLabel");
        v.NextTextBlock(ctx, selectedGroup.defName ?? "(unnamed)", GameFont.Small, padding: 2f, label: "ExistingInfo/DefName");
        if (!selectedFamilyKey.NullOrEmpty())
        {
            v.NextTextBlock(ctx, "Family", GameFont.Small, padding: 0f, label: "ExistingInfo/FamilyLabel");
            v.NextTextBlock(ctx, selectedFamilyKey, GameFont.Small, padding: 2f, label: "ExistingInfo/Family");
        }

        int stageCount = Mathf.Max(0, preview.StageCount);
        if (stageCount > 0)
        {
            int stageNumber = Mathf.Clamp(selectedStageIndex, 0, stageCount - 1);
            v.NextTextBlock(ctx, "Stage", GameFont.Small, padding: 0f, label: "ExistingInfo/StageLabel");
            v.NextTextBlock(ctx, $"{stageNumber:00} / {Mathf.Max(0, stageCount - 1):00}", GameFont.Small, padding: 2f, label: "ExistingInfo/Stage");
            v.NextTextBlock(ctx, "Repeat count", GameFont.Small, padding: 0f, label: "ExistingInfo/RepeatLabel");
            v.NextTextBlock(ctx, GetExistingStageRepeatCount(stageNumber).ToString(), GameFont.Small, padding: 2f, label: "ExistingInfo/Repeat");
        }

        v.NextSpace(Mathf.Max(4f, ctx.Style.Gap * 0.75f));
        v.NextTextBlock(ctx, preview.IsPlaying ? "Playback is currently driving the selected stage." : "Stage selection is idle and can be browsed from the left list.", GameFont.Small, padding: 2f, label: "ExistingInfo/PlaybackHint");
        v.NextTextBlock(ctx, "Import on the left to turn this into a mutable player project.", GameFont.Small, padding: 2f, label: "ExistingInfo/Hint");
    }
}
