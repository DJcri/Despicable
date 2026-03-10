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
    private void DrawAuthorCenter(Rect rect)
    {
        var ctx = frameworkCtx;
        var parts = D2Section.Layout(
            ctx,
            rect,
            new D2Section.Spec(
                "AuthorCenter",
                headerHeight: SectionHeaderHeight,
                soft: true,
                pad: true,
                drawBackground: true,
                padOverride: ctx.Style.Pad));

        D2Section.DrawCaptionStrip(ctx, parts.Header, "Preview & Playback", "AuthorCenter/Header", GameFont.Medium);

        var v = ctx.VStack(parts.Body);

        if (sourceMode == SourceMode.ExistingDef)
        {
            if (selectedGroup == null)
            {
                v.NextTextBlock(
                    ctx,
                    "Select an existing AnimGroupDef on the left to preview it. (Import it if you want to edit/create variations.)",
                    GameFont.Small,
                    padding: 2f,
                    label: "Center/Existing/Empty");
                return;
            }

            float transportH = authorPlayMode == AuthorPlayMode.Group
                ? (ctx.Style.RowHeight * 2f + ctx.Style.Gap)
                : ctx.Style.RowHeight;
            DrawExistingPreviewTransport(v.Next(transportH, UIRectTag.Input, "Center/Existing/Transport"));
            v.NextSpace(2f);

            if (authorPlayMode == AuthorPlayMode.Stage)
            {
                bool loopVal = loopCurrentStage;
                D2Widgets.CheckboxLabeled(
                    ctx,
                    v.NextRow(UIRectTag.Checkbox, "Center/Existing/LoopStage"),
                    "Loop selected stage",
                    ref loopVal,
                    "Center/Existing/LoopStage");
                loopCurrentStage = loopVal;
                preview.LoopCurrentStage = loopCurrentStage;
            }

            int stageCount = preview.StageCount;
            string stageInfo = stageCount > 0
                ? $"Stage {Mathf.Clamp(selectedStageIndex, 0, stageCount - 1):00} / {Mathf.Max(0, stageCount - 1):00}"
                : "No stages";
            string playInfo = preview.IsPlaying ? "Playback is driving stage selection." : "Click a stage on the left to inspect it.";
            v.NextTextBlock(ctx, selectedGroup.defName ?? "(unnamed)", GameFont.Small, padding: 2f, label: "Center/Existing/DefName");
            v.NextTextBlock(ctx, stageInfo + "   •   " + playInfo, GameFont.Small, padding: 2f, label: "Center/Existing/Info");

            Rect view = v.NextFill(UIRectTag.Body, "Center/Existing/Viewport");
            using (var previewPanel = ctx.GroupPanel("Center/Existing/PreviewSurface", view, soft: true, pad: true, padOverride: ctx.Style.Pad, drawBackground: true, label: "Center/Existing/PreviewSurface"))
                preview.DrawViewport(previewPanel.Inner, drawSection: false);
            return;
        }

        if (project == null)
        {
            v.NextTextBlock(ctx, "No project loaded.", GameFont.Small, padding: 2f, label: "Center/Author/NoProject");
            return;
        }

        var stage = GetStage(project, authorStageIndex);
        if (stage == null)
        {
            v.NextTextBlock(ctx, "No stage selected.", GameFont.Small, padding: 2f, label: "Center/Author/NoStage");
            return;
        }

        EnsureAuthorPreviewSource();

        DrawAuthorPreviewTimeline(v.Next(46f, UIRectTag.Input, "Center/Author/Timeline"), stage);
        v.NextSpace(2f);

        float authorTransportH = authorPlayMode == AuthorPlayMode.Group
            ? (ctx.Style.RowHeight * 2f + ctx.Style.Gap)
            : ctx.Style.RowHeight;
        DrawAuthorPreviewTransport(v.Next(authorTransportH, UIRectTag.Input, "Center/Author/Transport"));
        v.NextSpace(2f);

        Rect authorView = v.NextFill(UIRectTag.Body, "Center/Author/Viewport");
        using (var previewPanel = ctx.GroupPanel("Center/Author/PreviewSurface", authorView, soft: true, pad: true, padOverride: ctx.Style.Pad, drawBackground: true, label: "Center/Author/PreviewSurface"))
            DrawAuthorPreviewViewport(previewPanel.Inner);
    }

    private void DrawAuthorPreviewTimeline(Rect rect, AgsModel.StageSpec stage)
    {
        int dur = Mathf.Max(1, stage.durationTicks);
        authorPreviewTick = Mathf.Clamp(preview.CurrentTick, 0, dur);
        authorPreviewPlaying = preview.IsPlaying;

        Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.06f));
        Widgets.DrawHighlightIfMouseover(rect);

        if (Mouse.IsOver(rect)
            && (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseDrag)
            && Event.current.button == 0)
        {
            if (authorKeyIndex >= 0)
                authorKeyIndex = -1;

            float pct = Mathf.InverseLerp(rect.x + 6f, rect.xMax - 6f, Event.current.mousePosition.x);
            int t = Mathf.RoundToInt(pct * dur);
            t = Mathf.Clamp(t, 0, dur);
            if (t != authorPreviewTick)
            {
                authorPreviewTick = t;
                preview.SelectedStageIndex = authorStageIndex;
                if (!preview.IsPlaying)
                    preview.ShowSelectedStageAtTick(authorPreviewTick);
                else
                    preview.Seek(authorPreviewTick);
            }
            Event.current.Use();
        }

        float labelH = 22f;
        float gap = 4f;
        float barPadX = 10f;
        float barPadBottom = 6f;

        float barY = rect.y + labelH + gap;
        float barH = rect.height - (labelH + gap) - barPadBottom;
        if (barH < 10f)
            barH = 10f;

        var bar = new Rect(rect.x + barPadX, barY, rect.width - (barPadX * 2f), barH);
        float pctNow = dur <= 0 ? 0f : (authorPreviewTick / (float)dur);
        float x = Mathf.Lerp(bar.x, bar.xMax, pctNow);

        Widgets.DrawLineHorizontal(bar.x, bar.center.y, bar.width);
        Widgets.DrawBoxSolid(
            new Rect(bar.x, bar.y + (bar.height / 2f) - 1f, Mathf.Max(1f, x - bar.x), 3f),
            new Color(SelectedGreen.r, SelectedGreen.g, SelectedGreen.b, 0.25f));
        Widgets.DrawLineVertical(x, bar.y, bar.height);

        var leftLbl = new Rect(rect.x + 8f, rect.y + 2f, rect.width / 2f, labelH);
        var rightLbl = new Rect(rect.x, rect.y + 2f, rect.width - 8f, labelH);

        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.Label(leftLbl, "Timeline");
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(rightLbl, authorPreviewTick + "/" + dur);
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private void DrawAuthorPreviewViewport(Rect rect)
    {
        preview.DrawViewport(rect, drawSection: false);
    }

    private void DrawAuthorPreviewSlot(Rect rect, AuthorPreviewSlot slot)
    {
        if (slot == null) return;

        var header = rect.TopPartPixels(24f);
        Widgets.Label(header, slot.Label);

        var body = rect;
        body.yMin += 26f;

        try
        {
            int w = Mathf.Clamp((int)body.width, 128, 2048);
            int h = Mathf.Clamp((int)body.height, 128, 2048);
            slot.Renderer.EnsureSize(w, h);
            using (new WorkshopRenderContext.Scope(active: true, tick: authorPreviewTick))
            {
                slot.Renderer.RenderPawn(slot.Pawn);
            }

            var tex = slot.Renderer.Texture;
            if (tex != null)
                GUI.DrawTexture(body, tex, ScaleMode.ScaleToFit);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "AgsDrawPreviewPanel:1",
                "AgsDrawPreviewPanel ignored a non-fatal editor exception.",
                ex);
            Widgets.Label(body, "(render failed)");
        }
    }
}
