using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Controls;

/// <summary>
/// Draws a threshold ruler made of equal-width solid bands with an in-band marker.
/// Use this for ranked tracks such as Hero Karma and Ideology Standing.
/// </summary>
public static class D2BandRuler
{
    public readonly struct Band
    {
        public readonly string Id;
        public readonly string Label;
        public readonly int MinInclusive;
        public readonly int MaxInclusive;
        public readonly string Tooltip;

        public Band(string id, string label, int minInclusive, int maxInclusive, string tooltip = null)
        {
            Id = id;
            Label = label;
            MinInclusive = minInclusive;
            MaxInclusive = maxInclusive;
            Tooltip = tooltip;
        }

        public bool Contains(int value)
        {
            return value >= MinInclusive && value <= MaxInclusive;
        }
    }

    private static readonly Color BaseFill = new(1f, 1f, 1f, 0.14f);
    private static readonly Color ActiveFill = new(1f, 1f, 1f, 0.22f);
    private static readonly Color DividerColor = new(1f, 1f, 1f, 0.18f);
    private static readonly Color MarkerFill = new(1f, 1f, 1f, 0.95f);
    private static readonly Color MarkerOutline = new(0f, 0f, 0f, 0.72f);

    public static void Draw(
        UIContext ctx,
        Rect rect,
        int value,
        int min,
        int max,
        IReadOnlyList<Band> bands,
        string id,
        string tooltip = null,
        bool highlightActiveBand = true,
        Color? markerColor = null)
    {
        if (ctx == null)
            return;

        string root = id ?? "BandRuler";
        ctx.RecordRect(rect, UIRectTag.Input, root, null);
        if (!tooltip.NullOrEmpty())
        {
            D2Widgets.TooltipHotspot(ctx, rect, root + "/Tooltip");
            if (ctx.Pass == UIPass.Draw)
                TooltipHandler.TipRegion(rect, tooltip);
        }

        if (bands == null || bands.Count == 0)
        {
            D2Widgets.DrawBox(ctx, rect, 1, root + "/Border");
            return;
        }

        int activeIndex = FindActiveBandIndex(value, bands);
        float segmentWidth = Mathf.Max(0f, rect.width / bands.Count);

        for (int i = 0; i < bands.Count; i++)
        {
            Rect segRect = new(
                rect.x + (i * segmentWidth),
                rect.y,
                i == bands.Count - 1 ? rect.xMax - (rect.x + (i * segmentWidth)) : segmentWidth,
                rect.height);

            string segId = root + "/Band[" + i + "]";
            ctx.RecordRect(segRect, UIRectTag.Input, segId, bands[i].Label);

            Color fill = highlightActiveBand && i == activeIndex ? ActiveFill : BaseFill;
            D2Widgets.DrawBoxSolid(ctx, segRect, fill, segId + "/Fill");

            if (i > 0)
            {
                Rect divider = new(segRect.x, rect.y + 1f, 1f, Mathf.Max(0f, rect.height - 2f));
                D2Widgets.DrawBoxSolid(ctx, divider, DividerColor, segId + "/Divider");
            }

            if (!bands[i].Tooltip.NullOrEmpty())
            {
                D2Widgets.TooltipHotspot(ctx, segRect, segId + "/Tooltip");
                if (ctx.Pass == UIPass.Draw)
                    TooltipHandler.TipRegion(segRect, bands[i].Tooltip);
            }
        }

        D2Widgets.DrawBox(ctx, rect, 1, root + "/Border");

        if (activeIndex < 0)
            return;

        Rect activeRect = GetBandRect(rect, bands.Count, activeIndex);
        float markerCenterX = ComputeMarkerX(activeRect, bands[activeIndex], value);
        DrawMarker(ctx, rect, activeRect, markerCenterX, root + "/Marker", markerColor ?? MarkerFill);
    }

    public static Rect GetBandRect(Rect rulerRect, int bandCount, int bandIndex)
    {
        if (bandCount <= 0)
            return Rect.zero;

        float width = Mathf.Max(0f, rulerRect.width / bandCount);
        float x = rulerRect.x + (width * bandIndex);
        float finalWidth = bandIndex == bandCount - 1 ? rulerRect.xMax - x : width;
        return new Rect(x, rulerRect.y, finalWidth, rulerRect.height);
    }

    private static int FindActiveBandIndex(int value, IReadOnlyList<Band> bands)
    {
        if (bands == null || bands.Count == 0)
            return -1;

        for (int i = 0; i < bands.Count; i++)
        {
            if (bands[i].Contains(value))
                return i;
        }

        if (value < bands[0].MinInclusive)
            return 0;

        return bands.Count - 1;
    }

    private static float ComputeMarkerX(Rect activeRect, Band band, int value)
    {
        if (band.MaxInclusive <= band.MinInclusive)
            return activeRect.center.x;

        float t = Mathf.InverseLerp(band.MinInclusive, band.MaxInclusive, value);
        return Mathf.Lerp(activeRect.xMin + 1f, activeRect.xMax - 1f, t);
    }

    private static void DrawMarker(UIContext ctx, Rect rulerRect, Rect activeRect, float centerX, string id, Color fill)
    {
        float stemWidth = Mathf.Clamp(ctx.Style.RulerMarkerWidth, 2f, Mathf.Max(2f, activeRect.width));
        float stemHeight = Mathf.Clamp(ctx.Style.RulerMarkerHeight, rulerRect.height, rulerRect.height + 6f);
        float stemY = rulerRect.y + ((rulerRect.height - stemHeight) * 0.5f);
        float stemMaxX = Mathf.Max(activeRect.xMin, activeRect.xMax - stemWidth - 2f);
        Rect stemOutline = new(
            Mathf.Clamp(centerX - (stemWidth * 0.5f) - 1f, activeRect.xMin, stemMaxX),
            stemY,
            stemWidth + 2f,
            stemHeight);
        Rect stemInner = stemOutline.ContractedBy(1f, 0f);

        float diamondSize = Mathf.Clamp(ctx.Style.RulerMarkerDiamondSize, stemWidth + 3f, rulerRect.height + 4f);
        float diamondMinX = activeRect.xMin - 1f;
        float diamondMaxX = Mathf.Max(diamondMinX, activeRect.xMax - diamondSize - 1f);
        Rect diamondOutline = new(
            Mathf.Clamp(centerX - (diamondSize * 0.5f) - 1f, diamondMinX, diamondMaxX),
            rulerRect.center.y - (diamondSize * 0.5f) - 1f,
            diamondSize + 2f,
            diamondSize + 2f);
        Rect diamondInner = diamondOutline.ContractedBy(1f);

        ctx.RecordRect(stemOutline, UIRectTag.Icon, id + "/Stem", null);
        D2Widgets.DrawBoxSolid(ctx, stemOutline, MarkerOutline, id + "/Stem/Outline");
        D2Widgets.DrawBoxSolid(ctx, stemInner, fill, id + "/Stem/Fill");

        DrawRotatedDiamond(ctx, diamondOutline, MarkerOutline, id + "/Diamond/Outline");
        DrawRotatedDiamond(ctx, diamondInner, fill, id + "/Diamond/Fill");
    }

    private static void DrawRotatedDiamond(UIContext ctx, Rect rect, Color color, string id)
    {
        ctx.RecordRect(rect, UIRectTag.Icon, id, null);
        if (ctx.Pass == UIPass.Measure)
            return;

        Matrix4x4 old = GUI.matrix;
        Vector2 pivot = rect.center;
        GUIUtility.RotateAroundPivot(45f, pivot);
        using (new GUIColorScope(color))
        {
            GUI.DrawTexture(rect, BaseContent.WhiteTex, ScaleMode.StretchToFill, true);
        }
        GUI.matrix = old;
    }
}
