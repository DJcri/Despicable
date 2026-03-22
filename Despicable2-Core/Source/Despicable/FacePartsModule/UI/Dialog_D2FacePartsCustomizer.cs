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
    private const float EyeDetailSideToggleRowHeight = 24f;
    private const float TileSize = 64f;
    private const float TileGap = 6f;
    private const float WidePreviewWidth = 360f;

    private enum FaceStyleLane
    {
        Eyes,
        Brows,
        Mouth,
        EyeDetails,
    }

    private readonly Pawn _pawn;
    private readonly CompFaceParts _comp;
    private readonly PreviewRenderMode _previewRenderMode;

    private Vector2 _selectorsScroll;
    private float _selectorsContentHeight;
    private Pawn _previewPawn;
    private CompFaceParts _previewComp;
    private WorkshopPreviewRenderer _previewRenderer;
    private bool _previewPortraitDirty = true;

    public Dialog_D2FacePartsCustomizer(Pawn pawn, PreviewRenderMode previewRenderMode = PreviewRenderMode.Live)
    {
        _pawn = pawn;
        _comp = pawn?.TryGetComp<CompFaceParts>();
        _previewRenderMode = previewRenderMode;

        SetEditorActiveForPawn(_pawn, true);

        draggable = true;
        doCloseX = true;
        closeOnClickedOutside = false;
        absorbInputAroundWindow = true;
        forcePause = true;
        resizeable = false;
    }

    public override Vector2 InitialSize => new(920f, 640f);

    private static void SetEditorActiveForPawn(Pawn pawn, bool isActive)
    {
        FaceRuntimeActivityManager.SetEditorActive(pawn, isActive);
        pawn?.TryGetComp<CompFaceParts>()?.NotifyEditorActiveStateChanged(isActive);
    }

    protected override bool UseBodyScroll => false;
    protected override bool EnableAutoMeasure => false;
    protected override D2UIStyle Style => FaceUiStyle;

    public override void PreClose()
    {
        SetEditorActiveForPawn(_pawn, false);
        SetEditorActiveForPawn(_previewPawn, false);
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
        using var previewPanel = Ctx.GroupPanel("PreviewPanel", rect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
        if (_pawn == null)
        {
            var v = Ctx.D2VStack(previewPanel.Inner, label: "PreviewPanel/Empty");
            v.NextTextBlock(Ctx, "No pawn selected.", GameFont.Small, 0f, "PreviewPanel/EmptyLabel");
            return;
        }

        if (_previewRenderMode == PreviewRenderMode.IsolatedComposite)
        {
            DrawIsolatedFaceCompositePreview(previewPanel.Inner);
            return;
        }

        Rect livePreviewRect = ResolveLivePreviewRect(previewPanel.Inner);
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
        Pawn previewPawn = GetPortraitPawn();
        if (previewPawn == null || !_previewPortraitDirty)
            return;

        try
        {
            previewPawn.Drawer?.renderer?.EnsureGraphicsInitialized();
            AutoEyePatchRuntime.PrewarmForPawn(previewPawn);
            _comp?.InvalidateFaceStructure();
            _comp?.RefreshFaceHard(false);
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
        return _pawn ?? _previewPawn;
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
        FacePartSideMode eyeDetailSideMode = _comp?.GetResolvedEyeDetailSideMode() ?? FacePartSideMode.Both;
        string eyeDetailDebugLabel = ResolvePreferredPreviewDebugLabel("FacePart_EyeDetail_L", "FacePart_EyeDetail_R", _comp?.eyeDetailStyleDef, eyeDetailSideMode);
        Texture2D eyeDetailTexture = ResolvePreviewPartTexture(eyeDetailDebugLabel, _comp?.eyeDetailStyleDef, CompFaceParts.EMPTY_DETAIL_TEX_PATH, "FacePreviewCache.ResolveEyeDetailTexture", eyeDetailSideMode);
        Texture2D eyeTexture = ResolvePreviewPartTexture("FacePart_Eye_L", _comp?.eyeStyleDef, CompFaceParts.DEFAULT_EYE_TEX_PATH, "FacePreviewCache.ResolveEyeTexture");
        Texture2D browTexture = ResolvePreviewPartTexture("FacePart_Brow_L", _comp?.browStyleDef, CompFaceParts.DEFAULT_BROW_TEX_PATH, "FacePreviewCache.ResolveBrowTexture");
        Texture2D mouthTexture = ResolvePreviewPartTexture("FacePart_Mouth", _comp?.mouthStyleDef, CompFaceParts.DEFAULT_MOUTH_TEX_PATH, "FacePreviewCache.ResolveMouthTexture");

        FacePreviewCache.DrawAlignedTextureStack(
            Ctx,
            rect,
            "PreviewPanel/CompositeImage",
            FacePreviewCache.PreviewAnchor.Center,
            padding: 8f,
            headTexture,
            eyeDetailTexture,
            eyeTexture,
            browTexture,
            mouthTexture);
    }

    private static string ResolvePreferredPreviewDebugLabel(string leftDebugLabel, string rightDebugLabel, FacePartStyleDef style, FacePartSideMode selectedSideMode = FacePartSideMode.Both)
    {
        FacePartSideMode effectiveSideMode = CompFaceParts.ResolveStyleSideMode(style, selectedSideMode);
        if (effectiveSideMode == FacePartSideMode.RightOnly && !rightDebugLabel.NullOrEmpty())
            return rightDebugLabel;

        return leftDebugLabel;
    }

    private Texture2D ResolvePreviewPartTexture(string debugLabel, FacePartStyleDef style, string fallbackTexPath, string warnOnceKey, FacePartSideMode selectedSideMode = FacePartSideMode.Both)
    {
        Texture2D texture = FacePreviewCache.ResolveFacePartTexture(_pawn, style, debugLabel, fallbackTexPath, selectedSideMode);
        if (texture != null)
            return texture;

        string resolvedPath = fallbackTexPath;
        if ((debugLabel == "FacePart_Eye_L" || debugLabel == "FacePart_Eye_R") && !resolvedPath.NullOrEmpty())
            resolvedPath = FacePartsUtil.GetEyePath(_pawn, resolvedPath);

        return FacePreviewCache.ResolveTexture(resolvedPath, warnOnceKey);
    }

    private void DrawSelectorsPane(Rect rect)
    {
        string unavailableReason = GetUnavailableReason();
        if (!unavailableReason.NullOrEmpty())
        {
            using var unavailablePanel = Ctx.GroupPanel("UnavailablePanel", rect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
            var v = Ctx.D2VStack(unavailablePanel.Inner, label: "UnavailablePanel/Stack");
            Rect messageRect = v.NextFill(UIRectTag.Label, "UnavailablePanel/MessageRect");
            using (new TextStateScope(GameFont.Small, TextAnchor.MiddleCenter, true))
                D2Widgets.LabelClippedAligned(Ctx, messageRect, "Face parts unavailable for this pawn.", TextAnchor.MiddleCenter, "UnavailablePanel/Message", unavailableReason);
            return;
        }

        using var selectorsPanel = Ctx.GroupPanel("SelectorsPanel", rect, soft: true, pad: true, padOverride: Ctx.Style.Pad);
        D2ScrollView.Draw(
            Ctx,
            selectorsPanel.Inner,
            ref _selectorsScroll,
            ref _selectorsContentHeight,
            delegate(UIContext localCtx, ref D2VStack localV)
            {
                DrawAllStylePanels(localCtx, ref localV);
            },
            "SelectorsPanel/Scroll");
    }

    private void DrawAllStylePanels(UIContext ctx, ref D2VStack v)
    {
        DrawStylePanel(ctx, ref v, FaceStyleLane.Eyes);
        DrawStylePanel(ctx, ref v, FaceStyleLane.Brows);
        DrawStylePanel(ctx, ref v, FaceStyleLane.Mouth);
        DrawStylePanel(ctx, ref v, FaceStyleLane.EyeDetails);
    }

    private void DrawStylePanel(UIContext ctx, ref D2VStack v, FaceStyleLane lane)
    {
        string title = GetLaneTitle(lane);
        List<FacePartStyleDef> styles = GetStyles(lane);
        bool showEyeDetailSideToggle = lane == FaceStyleLane.EyeDetails && ShouldShowEyeDetailSideToggle();
        float panelHeight = MeasureStylePanelHeight(v.Bounds.width, styles?.Count ?? 0, showEyeDetailSideToggle);
        Rect rect = v.Next(panelHeight, UIRectTag.PanelSoft, title + "Panel/Outer");

        using var stylePanel = ctx.GroupPanel(title + "Panel", rect, soft: true, pad: true, padOverride: ctx.Style.Pad);
        D2Section.Parts section = D2Section.Layout(ctx, stylePanel.Inner, new D2Section.Spec(title + "Section", headerHeight: SectionHeaderHeight, soft: false, pad: false, drawBackground: false));
        DrawStylePanelHeader(ctx, section.Header, title, lane);

        Texture2D headTexture = FacePreviewCache.ResolveHeadTexture(_pawn?.story?.headType);
        var bodyStack = ctx.D2VStack(section.Body, label: title + "Section/BodyStack");
        if (showEyeDetailSideToggle)
        {
            Rect toggleRowRect = bodyStack.Next(EyeDetailSideToggleRowHeight, UIRectTag.Group, title + "Section/SideToggleRow");
            DrawEyeDetailSideToggleRow(ctx, toggleRowRect, "EyeDetails/SideToggleRow");
        }

        DrawTileGrid(ctx, ref bodyStack, styles, lane, headTexture);
    }

    private float MeasureStylePanelHeight(float availableWidth, int itemCount, bool includeEyeDetailSideToggle = false)
    {
        float innerWidth = Mathf.Max(0f, availableWidth - (Ctx.Style.Pad * 2f));
        float bodyHeight = itemCount > 0
            ? FacePreviewCache.MeasureGridHeight(innerWidth, itemCount, TileSize, TileGap)
            : Ctx.Style.Line;

        if (includeEyeDetailSideToggle)
            bodyHeight += EyeDetailSideToggleRowHeight + Ctx.Style.Gap;

        return (Ctx.Style.Pad * 2f) + SectionHeaderHeight + bodyHeight;
    }

    private void DrawTileGrid(UIContext ctx, ref D2VStack v, List<FacePartStyleDef> styles, FaceStyleLane lane, Texture2D headTexture)
    {
        string laneId = GetLaneIdPrefix(lane);
        if (styles == null || styles.Count == 0)
        {
            v.NextTextBlock(ctx, "No styles available.", GameFont.Small, 0f, laneId + "/Empty");
            return;
        }

        int columns = FacePreviewCache.ComputeGridColumns(v.Bounds.width, TileSize, TileGap);
        int index = 0;
        int rowIndex = 0;
        while (index < styles.Count)
        {
            Rect rowRect = v.Next(TileSize, UIRectTag.Group, laneId + "/GridRow[" + rowIndex + "]");
            float x = rowRect.x;
            for (int col = 0; col < columns && index < styles.Count; col++, index++)
            {
                Rect tileRect = new(x, rowRect.y, TileSize, TileSize);
                DrawStyleTile(tileRect, styles[index], lane, index, headTexture);
                x += TileSize + TileGap;
            }

            rowIndex++;
        }
    }

    private void DrawStyleTile(Rect rect, FacePartStyleDef style, FaceStyleLane lane, int index, Texture2D headTexture)
    {
        bool selected = style == GetSelectedStyle(lane);
        string laneId = GetLaneIdPrefix(lane);
        string id = laneId + "/Tile[" + index + "]";
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

            FacePartSideMode previewSideMode = GetPreviewSideModeForStyle(lane, style, selected);
            string previewDebugLabel = lane == FaceStyleLane.EyeDetails ? ResolvePreferredPreviewDebugLabel("FacePart_EyeDetail_L", "FacePart_EyeDetail_R", style, previewSideMode) : null;
            Texture2D styleTexture = FacePreviewCache.ResolveFacePartTexture(_pawn, style, previewDebugLabel, null, previewSideMode);
            FacePreviewCache.DrawCroppedTexture(
                Ctx,
                rect.ContractedBy(4f),
                styleTexture,
                id + "/Preview",
                0f,
                FacePreviewCache.PreviewAnchor.Center);

            if (Widgets.ButtonInvisible(rect, false))
                ApplyStyle(style, lane);
        }
    }


    private void DrawStylePanelHeader(UIContext ctx, Rect rect, string title, FaceStyleLane lane)
    {
        D2Section.DrawCaptionStrip(ctx, rect, title, title + "Section/Title", GameFont.Medium);
    }

    private bool ShouldShowEyeDetailSideToggle()
    {
        return _comp?.eyeDetailStyleDef?.allowSideSelection == true;
    }

    private FacePartSideMode GetPreviewSideModeForStyle(FaceStyleLane lane, FacePartStyleDef style, bool selected)
    {
        if (lane != FaceStyleLane.EyeDetails)
            return FacePartSideMode.Both;

        if (style?.allowSideSelection == true && _comp != null)
            return NormalizeEyeDetailSideMode(_comp.eyeDetailSideMode);

        if (selected && _comp != null)
            return _comp.GetResolvedEyeDetailSideMode();

        return CompFaceParts.ResolveStyleSideMode(style, style?.sideMode ?? FacePartSideMode.Both);
    }

    private static FacePartSideMode NormalizeEyeDetailSideMode(FacePartSideMode sideMode)
    {
        return sideMode == FacePartSideMode.RightOnly ? FacePartSideMode.RightOnly : FacePartSideMode.LeftOnly;
    }

    private void DrawEyeDetailSideToggle(UIContext ctx, Rect rect, string id)
    {
        FacePartSideMode currentSideMode = NormalizeEyeDetailSideMode(_comp?.eyeDetailSideMode ?? FacePartSideMode.LeftOnly);
        float buttonWidth = Mathf.Max(0f, (rect.width - ctx.Style.GapXS) * 0.5f);
        D2RectSplit.SplitVertical(rect, buttonWidth, ctx.Style.GapXS, out Rect leftRect, out Rect rightRect);

        DrawEyeDetailSideButton(ctx, leftRect, "L", currentSideMode != FacePartSideMode.RightOnly, id + "/Left", FacePartSideMode.LeftOnly, "Render this detail on the left side.");
        DrawEyeDetailSideButton(ctx, rightRect, "R", currentSideMode == FacePartSideMode.RightOnly, id + "/Right", FacePartSideMode.RightOnly, "Render this detail on the right side.");
    }


    private void DrawEyeDetailSideToggleRow(UIContext ctx, Rect rect, string id)
    {
        float maxToggleWidth = (Ctx.Style.ButtonHeight * 2f) + ctx.Style.GapXS;
        float toggleWidth = Mathf.Min(rect.width, Mathf.Max(46f, maxToggleWidth));
        float labelWidth = Mathf.Max(0f, rect.width - toggleWidth - ctx.Style.GapS);
        D2RectSplit.SplitVertical(rect, labelWidth, ctx.Style.GapS, out Rect labelRect, out Rect toggleRect);
        D2Widgets.LabelClippedAligned(ctx, labelRect, "Side", TextAnchor.MiddleLeft, id + "/Label", "Choose which side of the face this asymmetrical detail should use.");
        DrawEyeDetailSideToggle(ctx, toggleRect, id + "/Buttons");
    }

    private void DrawEyeDetailSideButton(UIContext ctx, Rect rect, string text, bool selected, string id, FacePartSideMode sideMode, string tooltip)
    {
        ctx.RecordRect(rect, UIRectTag.Button, id, selected ? "selected=1" : "selected=0");
        if (ctx.Pass == UIPass.Draw)
        {
            if (selected)
                D2Widgets.HighlightSelected(ctx, rect, id + "/Selected");

            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(rect, tooltip);

            if (D2Widgets.ButtonText(ctx, rect, text, id))
                ApplyEyeDetailSideMode(sideMode);
        }
    }

    private void ApplyEyeDetailSideMode(FacePartSideMode sideMode)
    {
        if (_comp?.eyeDetailStyleDef?.allowSideSelection != true)
            return;

        FacePartSideMode normalizedSideMode = NormalizeEyeDetailSideMode(sideMode);
        if (_comp.eyeDetailSideMode == normalizedSideMode)
            return;

        _comp.eyeDetailSideMode = normalizedSideMode;
        _comp.InvalidateFaceStructure();
        _comp.RefreshFaceHard(true);
        ForceImmediatePawnVisualRefresh(_pawn, markPortraitDirty: true);
        _previewPortraitDirty = true;

        if (_previewComp != null)
        {
            _previewComp.eyeDetailSideMode = normalizedSideMode;
            _previewComp.InvalidateFaceStructure();
            _previewComp.RefreshFaceHard(false);
            ForceImmediatePawnVisualRefresh(_previewPawn, markPortraitDirty: true);
            _previewPortraitDirty = true;
        }
    }

    private List<FacePartStyleDef> GetStyles(FaceStyleLane lane)
    {
        string tag = GetLaneRenderNodeTagDefName(lane);

        return DefDatabase<FacePartStyleDef>.AllDefsListForReading
            .Where(style => style != null && style.renderNodeTag != null && string.Equals(style.renderNodeTag.defName, tag, StringComparison.Ordinal))
            .Where(style => lane != FaceStyleLane.EyeDetails || !CompFaceParts.IsRetiredEyeDetailStyle(style))
            .Where(style => CompFaceParts.IsStyleEligibleForPawn(_pawn, style))
            .OrderBy(style => GetStyleSortGroup(style, lane))
            .ThenBy(style => style.LabelCap.ToString())
            .ThenBy(style => style.defName ?? string.Empty)
            .ToList();
    }

    private int GetStyleSortGroup(FacePartStyleDef style, FaceStyleLane lane)
    {
        if (style == null)
            return 2;

        if (lane == FaceStyleLane.EyeDetails
            && string.Equals(style.texPath, CompFaceParts.EMPTY_DETAIL_TEX_PATH, StringComparison.OrdinalIgnoreCase))
            return 0;

        return 1;
    }

    private FacePartStyleDef GetSelectedStyle(FaceStyleLane lane)
    {
        return lane switch
        {
            FaceStyleLane.Eyes => _comp?.eyeStyleDef,
            FaceStyleLane.Brows => _comp?.browStyleDef,
            FaceStyleLane.Mouth => _comp?.mouthStyleDef,
            FaceStyleLane.EyeDetails => _comp?.eyeDetailStyleDef,
            _ => null
        };
    }

    private string GetLaneTitle(FaceStyleLane lane)
    {
        return lane switch
        {
            FaceStyleLane.Eyes => "Eyes",
            FaceStyleLane.Brows => "Brows",
            FaceStyleLane.Mouth => "Mouth",
            FaceStyleLane.EyeDetails => "Eye Details",
            _ => "Styles"
        };
    }

    private string GetLaneIdPrefix(FaceStyleLane lane)
    {
        return lane switch
        {
            FaceStyleLane.Eyes => "Eyes",
            FaceStyleLane.Brows => "Brows",
            FaceStyleLane.Mouth => "Mouth",
            FaceStyleLane.EyeDetails => "EyeDetails",
            _ => "Styles"
        };
    }

    private string GetLaneRenderNodeTagDefName(FaceStyleLane lane)
    {
        return lane switch
        {
            FaceStyleLane.Eyes => "FacePart_Eye",
            FaceStyleLane.Brows => "FacePart_Brow",
            FaceStyleLane.Mouth => "FacePart_Mouth",
            FaceStyleLane.EyeDetails => "FacePart_EyeDetail",
            _ => string.Empty
        };
    }

    private void ApplyStyle(FacePartStyleDef style, FaceStyleLane lane)
    {
        if (_comp == null || style == null || !CompFaceParts.IsStyleEligibleForPawn(_pawn, style))
            return;

        if (GetSelectedStyle(lane) == style)
            return;

        SetStyleOnComp(_comp, lane, style);
        if (lane == FaceStyleLane.EyeDetails && style.allowSideSelection)
            _comp.eyeDetailSideMode = NormalizeEyeDetailSideMode(_comp.eyeDetailSideMode);
        
        _comp.InvalidateFaceStructure();
        _comp.RefreshFaceHard(true);
        ForceImmediatePawnVisualRefresh(_pawn, markPortraitDirty: true);
        _previewPortraitDirty = true;

        if (_previewComp != null)
        {
            SetStyleOnComp(_previewComp, lane, style);
            if (lane == FaceStyleLane.EyeDetails && style.allowSideSelection)
                _previewComp.eyeDetailSideMode = NormalizeEyeDetailSideMode(_previewComp.eyeDetailSideMode);
            _previewComp.InvalidateFaceStructure();
            _previewComp.RefreshFaceHard(false);
            ForceImmediatePawnVisualRefresh(_previewPawn, markPortraitDirty: true);
            _previewPortraitDirty = true;
        }
    }

    private static void SetStyleOnComp(CompFaceParts comp, FaceStyleLane lane, FacePartStyleDef style)
    {
        if (comp == null)
            return;

        switch (lane)
        {
            case FaceStyleLane.Eyes:
                comp.eyeStyleDef = style;
                break;
            case FaceStyleLane.Brows:
                comp.browStyleDef = style;
                break;
            case FaceStyleLane.Mouth:
                comp.mouthStyleDef = style;
                break;
            case FaceStyleLane.EyeDetails:
                comp.eyeDetailStyleDef = style;
                break;
        }
    }

    private static void ForceImmediatePawnVisualRefresh(Pawn pawn, bool markPortraitDirty)
    {
        PawnRenderer renderer = pawn?.Drawer?.renderer;
        if (renderer == null)
            return;

        renderer.SetAllGraphicsDirty();
        GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
        if (markPortraitDirty)
            PortraitsCache.SetDirty(pawn);
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
