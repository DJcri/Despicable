using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Despicable.UIFramework;
using Despicable.UIFramework.Blueprints;
using Despicable.UIFramework.Layout;

namespace Despicable.FacePartsModule.UI;

// Guardrail-Reason: Face-parts customizer keeps preview orchestration, selector UI, and fallback portrait resolution together because this editor window depends on one tightly coupled face-preview seam.
public sealed class Dialog_D2FacePartsCustomizer : D2WindowBlueprint
{
    public enum PreviewRenderMode
    {
        Live,
        LiveSquareSandbox,
        IsolatedComposite,
    }

    private static readonly D2UIStyle FaceUiStyle = D2UIStyle.Default.With(s =>
    {
        s.HeaderHeight = 58f;
        s.FooterHeight = 0f;
        s.BodyTopPadY = 6f;
        s.BodyBottomPadY = 6f;
        s.RowHeight = 28f;
        s.ButtonHeight = 28f;
    });

    private const float SectionHeaderHeight = 28f;
    private const float TileSize = 64f;
    private const float TileGap = 6f;
    private const float WidePreviewWidth = 360f;

    private readonly Pawn _pawn;
    private readonly CompFaceParts _comp;
    private readonly PreviewRenderMode _previewRenderMode;

    private Vector2 _eyesScroll;
    private float _eyesContentHeight;
    private Vector2 _mouthScroll;
    private float _mouthContentHeight;
    private Pawn _previewPawn;
    private CompFaceParts _previewComp;
    private WorkshopPreviewRenderer _previewRenderer;
    private bool _previewPortraitDirty = true;

    public Dialog_D2FacePartsCustomizer(Pawn pawn, PreviewRenderMode previewRenderMode = PreviewRenderMode.Live)
    {
        _pawn = pawn;
        _comp = pawn?.TryGetComp<CompFaceParts>();
        _previewRenderMode = previewRenderMode;

        draggable = true;
        doCloseX = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
        forcePause = true;
        resizeable = false;
    }

    public override Vector2 InitialSize => new(920f, 640f);

    protected override bool UseBodyScroll => false;
    protected override bool EnableAutoMeasure => false;
    protected override D2UIStyle Style => FaceUiStyle;

    public override void PreClose()
    {
        ReleasePreviewResources();
        base.PreClose();
    }

    protected override void DrawHeader(Rect rect)
    {
        string subtitle = _pawn != null ? _pawn.LabelShortCap.ToString() : "No pawn";
        var v = Ctx.D2VStack(rect, label: "Header/Stack");
        Rect titleRect = v.Next(28f, UIRectTag.Label, "Header/Title");
        Rect subtitleRect = v.Next(24f, UIRectTag.Label, "Header/Subtitle");

        D2Text.DrawWrappedLabel(Ctx, titleRect, "Face Parts", GameFont.Medium, UIRectTag.Label, "Header/TitleText");
        D2Text.DrawWrappedLabel(Ctx, subtitleRect, subtitle, GameFont.Small, UIRectTag.Label, "Header/SubtitleText");
    }

    protected override void DrawBody(Rect rect)
    {
        bool stacked = rect.width < 760f;
        if (stacked)
        {
            float previewHeight = Mathf.Clamp(rect.height * 0.42f, 220f, 300f);
            Rect previewRect = new(rect.x, rect.y, rect.width, previewHeight);
            Rect selectorsRect = new(rect.x, previewRect.yMax + Ctx.Style.GapS, rect.width, Mathf.Max(0f, rect.yMax - previewRect.yMax - Ctx.Style.GapS));
            DrawPreviewPane(previewRect);
            DrawSelectorsPane(selectorsRect);
            return;
        }

        Rect leftRect = new(rect.x, rect.y, Mathf.Min(WidePreviewWidth, rect.width), rect.height);
        Rect rightRect = new(leftRect.xMax + Ctx.Style.GapS, rect.y, Mathf.Max(0f, rect.xMax - leftRect.xMax - Ctx.Style.GapS), rect.height);
        DrawPreviewPane(leftRect);
        DrawSelectorsPane(rightRect);
    }

    private void DrawPreviewPane(Rect rect)
    {
        using var panel = Ctx.GroupPanel("PreviewPanel", rect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
        if (_pawn == null)
        {
            var v = Ctx.D2VStack(panel.Inner, label: "PreviewPanel/Empty");
            v.NextTextBlock(Ctx, "No pawn selected.", GameFont.Small, 0f, "PreviewPanel/EmptyLabel");
            return;
        }

        if (_previewRenderMode == PreviewRenderMode.IsolatedComposite)
        {
            DrawIsolatedFaceCompositePreview(panel.Inner);
            return;
        }

        Rect livePreviewRect = ResolveLivePreviewRect(panel.Inner);
        if (livePreviewRect.width <= 0f || livePreviewRect.height <= 0f)
            return;

        Texture previewTexture = null;
        if (Ctx.Pass == UIPass.Draw)
        {
            PrepareLivePreview();
            previewTexture = TryRenderLivePreviewTexture(livePreviewRect);
        }

        Ctx.RecordRect(livePreviewRect, UIRectTag.Icon, "PreviewPanel/Image", null);
        if (Ctx.Pass == UIPass.Draw)
        {
            if (previewTexture != null)
                GUI.DrawTexture(livePreviewRect, previewTexture, ScaleMode.ScaleToFit, true);
            else
                DrawIsolatedFaceCompositePreview(livePreviewRect);
        }
    }

    private Rect ResolveLivePreviewRect(Rect rect)
    {
        if (_previewRenderMode != PreviewRenderMode.LiveSquareSandbox)
            return rect;

        float size = Mathf.Min(rect.width, rect.height);
        float x = rect.x + ((rect.width - size) * 0.5f);
        float y = rect.y + ((rect.height - size) * 0.5f);
        return new Rect(x, y, size, size);
    }

    private void PrepareLivePreview()
    {
        if (_previewPawn == null && _pawn != null)
        {
            try
            {
                _previewPawn = PreviewPawnFactory.MakePreviewPawn(_pawn);
                _previewComp = _previewPawn?.TryGetComp<CompFaceParts>();
                _previewPortraitDirty = true;
            }
            catch (Exception ex)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce(
                    "Dialog_D2FacePartsCustomizer.CreatePreviewPawn",
                    "Face parts customizer failed to create a preview pawn cleanly.",
                    ex);
            }
        }

        Pawn previewPawn = GetPortraitPawn();
        if (previewPawn == null || !_previewPortraitDirty)
            return;

        try
        {
            previewPawn.Drawer?.renderer?.EnsureGraphicsInitialized();
            AutoEyePatchRuntime.PrewarmForPawn(previewPawn);
            _previewComp?.InvalidateFaceStructure();
            _previewComp?.RefreshFaceHard(false);
            previewPawn.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(previewPawn);
            _previewPortraitDirty = false;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "Dialog_D2FacePartsCustomizer.PrepareLivePreview",
                "Face parts customizer failed to prepare its live preview pawn cleanly.",
                ex);
        }
    }

    private Pawn GetPortraitPawn()
    {
        return _previewPawn ?? _pawn;
    }

    private Texture TryRenderLivePreviewTexture(Rect rect)
    {
        Pawn previewPawn = GetPortraitPawn();
        if (previewPawn == null)
            return null;

        int width = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(128f, rect.width)), 128, 2048);
        int height = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(128f, rect.height)), 128, 2048);

        try
        {
            _previewRenderer ??= new WorkshopPreviewRenderer(width, height);
            _previewRenderer.EnsureSize(width, height);
            _previewRenderer.RenderPawn(previewPawn, Rot4.South, 0f, default, renderHeadgear: false, portrait: true, scale: 1.2f);
            return _previewRenderer.GetTexture();
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "Dialog_D2FacePartsCustomizer.RenderLivePreviewTexture",
                "Face parts customizer failed to render its isolated live preview cleanly.",
                ex);
            return null;
        }
    }

    private void DrawIsolatedFaceCompositePreview(Rect rect)
    {
        Texture2D headTexture = FacePreviewCache.ResolveHeadTexture(_pawn?.story?.headType);
        Texture2D eyeTexture = FacePreviewCache.ResolveFacePartTexture(_pawn, _comp?.eyeStyleDef);
        Texture2D mouthTexture = FacePreviewCache.ResolveFacePartTexture(_pawn, _comp?.mouthStyleDef);
        Texture2D detailTexture = FacePreviewCache.ResolveTexture(_comp?.GetBaseDetailTexPath(), "FacePreviewCache.ResolveDetailTexture");

        FacePreviewCache.DrawAlignedTextureStack(
            Ctx,
            rect,
            "PreviewPanel/CompositeImage",
            FacePreviewCache.PreviewAnchor.Center,
            padding: 8f,
            headTexture,
            detailTexture,
            eyeTexture,
            mouthTexture);
    }

    private void DrawSelectorsPane(Rect rect)
    {
        string unavailableReason = GetUnavailableReason();
        if (!unavailableReason.NullOrEmpty())
        {
            using var panel = Ctx.GroupPanel("UnavailablePanel", rect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
            var v = Ctx.D2VStack(panel.Inner, label: "UnavailablePanel/Stack");
            Rect messageRect = v.NextFill(UIRectTag.Label, "UnavailablePanel/MessageRect");
            using (new TextStateScope(GameFont.Small, TextAnchor.MiddleCenter, true))
                D2Widgets.LabelClippedAligned(Ctx, messageRect, "Face parts unavailable for this pawn.", TextAnchor.MiddleCenter, "UnavailablePanel/Message", unavailableReason);
            return;
        }

        float panelGap = Ctx.Style.GapM;
        float panelHeight = Mathf.Max(0f, (rect.height - panelGap) * 0.5f);
        Rect eyesRect = new(rect.x, rect.y, rect.width, panelHeight);
        Rect mouthRect = new(rect.x, eyesRect.yMax + panelGap, rect.width, Mathf.Max(0f, rect.yMax - eyesRect.yMax - panelGap));

        DrawStylePanel(eyesRect, "Eyes", GetStyles(forEyes: true), ref _eyesScroll, ref _eyesContentHeight, forEyes: true);
        DrawStylePanel(mouthRect, "Mouth", GetStyles(forEyes: false), ref _mouthScroll, ref _mouthContentHeight, forEyes: false);
    }

    private void DrawStylePanel(Rect rect, string title, List<FacePartStyleDef> styles, ref Vector2 scroll, ref float contentHeight, bool forEyes)
    {
        using var panel = Ctx.GroupPanel(title + "Panel", rect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
        D2Section.Parts section = D2Section.Layout(Ctx, panel.Inner, new D2Section.Spec(title + "Section", headerHeight: SectionHeaderHeight, soft: false, pad: false, drawBackground: false));
        D2Section.DrawCaptionStrip(Ctx, section.Header, title, title + "Section/Title", GameFont.Medium);
        D2ScrollView.Draw(Ctx, section.Body, ref scroll, ref contentHeight,
            delegate(UIContext localCtx, ref D2VStack localV) { DrawTileGrid(localCtx, ref localV, styles, forEyes); },
            title + "Section/Scroll");
    }

    private void DrawTileGrid(UIContext ctx, ref D2VStack v, List<FacePartStyleDef> styles, bool forEyes)
    {
        if (styles == null || styles.Count == 0)
        {
            v.NextTextBlock(ctx, "No styles available.", GameFont.Small, 0f, (forEyes ? "Eyes" : "Mouth") + "/Empty");
            return;
        }

        int columns = FacePreviewCache.ComputeGridColumns(v.Bounds.width, TileSize, TileGap);
        int index = 0;
        int rowIndex = 0;
        while (index < styles.Count)
        {
            Rect rowRect = v.Next(TileSize, UIRectTag.Group, (forEyes ? "Eyes" : "Mouth") + "/GridRow[" + rowIndex + "]");
            float x = rowRect.x;
            for (int col = 0; col < columns && index < styles.Count; col++, index++)
            {
                Rect tileRect = new(x, rowRect.y, TileSize, TileSize);
                DrawStyleTile(tileRect, styles[index], forEyes, index);
                x += TileSize + TileGap;
            }

            rowIndex++;
        }
    }

    private void DrawStyleTile(Rect rect, FacePartStyleDef style, bool forEyes, int index)
    {
        bool selected = style == (forEyes ? _comp?.eyeStyleDef : _comp?.mouthStyleDef);
        string id = (forEyes ? "Eyes" : "Mouth") + "/Tile[" + index + "]";
        Ctx.RecordRect(rect, UIRectTag.Button, id, selected ? "selected=1" : "selected=0");

        if (Ctx.Pass == UIPass.Draw)
        {
            if (Command.BGTex != null)
                GUI.DrawTexture(rect, Command.BGTex, ScaleMode.StretchToFill, true);
            else
                Widgets.DrawMenuSection(rect);

            D2Widgets.HighlightOnHover(Ctx, rect, id + "/Hover");
            if (selected)
                D2Widgets.HighlightSelected(Ctx, rect, id + "/Selected");

            Texture2D texture = FacePreviewCache.ResolveFacePartTexture(_pawn, style);
            FacePreviewCache.DrawCroppedTexture(Ctx, rect.ContractedBy(6f), texture, id + "/Preview", 0f, FacePreviewCache.PreviewAnchor.Center);

            if (Widgets.ButtonInvisible(rect, false))
                ApplyStyle(style, forEyes);
        }
    }

    private List<FacePartStyleDef> GetStyles(bool forEyes)
    {
        string tag = forEyes ? "FacePart_Eye" : "FacePart_Mouth";
        Gender? pawnGender = _pawn?.gender;

        return DefDatabase<FacePartStyleDef>.AllDefsListForReading
            .Where(style => style != null && style.renderNodeTag != null && string.Equals(style.renderNodeTag.defName, tag, StringComparison.Ordinal))
            .Where(style => style.requiredGender == null || (pawnGender != null && style.requiredGender.Value == (byte)pawnGender.Value))
            .OrderBy(style => style.LabelCap.ToString())
            .ThenBy(style => style.defName ?? string.Empty)
            .ToList();
    }

    private void ApplyStyle(FacePartStyleDef style, bool forEyes)
    {
        if (_comp == null || style == null)
            return;

        if (forEyes)
        {
            if (_comp.eyeStyleDef == style)
                return;
            _comp.eyeStyleDef = style;
        }
        else
        {
            if (_comp.mouthStyleDef == style)
                return;
            _comp.mouthStyleDef = style;
        }

        _comp.InvalidateFaceStructure();
        _comp.RefreshFaceHard(true);

        if (_previewComp != null)
        {
            if (forEyes)
                _previewComp.eyeStyleDef = _comp.eyeStyleDef;
            else
                _previewComp.mouthStyleDef = _comp.mouthStyleDef;

            _previewComp.InvalidateFaceStructure();
            _previewComp.RefreshFaceHard(false);
            _previewPawn?.Drawer?.renderer?.SetAllGraphicsDirty();
            if (_previewPawn != null)
                PortraitsCache.SetDirty(_previewPawn);
            _previewPortraitDirty = false;
        }
    }

    private string GetUnavailableReason()
    {
        if (_pawn == null)
            return "No pawn selected.";
        if (ModMain.IsNlFacialInstalled)
            return "Face parts are disabled while NL Facial Animation is active.";
        if (_pawn.RaceProps?.Humanlike != true)
            return "Only humanlike pawns support face parts.";
        if ((CommonUtil.GetSettings()?.facialPartsExtensionEnabled ?? false) == false)
            return "Face parts are disabled in settings.";
        if (_comp == null)
            return "This pawn does not have a face-parts component.";
        if (FacePartsUtil.IsHeadBlacklisted(_pawn.story?.headType))
            return "This pawn's headtype is ignored by face parts.";
        return null;
    }

    private void ReleasePreviewResources()
    {
        if (_previewPawn != null)
        {
            try
            {
                if (_previewPawn.Spawned)
                    _previewPawn.DeSpawn();
            }
            catch (Exception ex)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce(
                    "Dialog_D2FacePartsCustomizer.ReleasePreviewPawnDeSpawn",
                    "Face parts customizer failed to despawn its preview pawn cleanly.",
                    ex);
            }

            try
            {
                _previewPawn.Destroy(DestroyMode.Vanish);
            }
            catch (Exception ex)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce(
                    "Dialog_D2FacePartsCustomizer.ReleasePreviewPawnDestroy",
                    "Face parts customizer failed to destroy its preview pawn cleanly.",
                    ex);
            }
        }

        try
        {
            _previewRenderer?.Dispose();
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "Dialog_D2FacePartsCustomizer.ReleasePreviewRenderer",
                "Face parts customizer failed to release its live preview renderer cleanly.",
                ex);
        }

        _previewRenderer = null;
        _previewPawn = null;
        _previewComp = null;
        _previewPortraitDirty = true;
    }
}
