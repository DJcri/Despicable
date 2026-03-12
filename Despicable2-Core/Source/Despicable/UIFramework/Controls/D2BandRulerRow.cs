using System.Collections.Generic;
using UnityEngine;
using Verse;
using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;

/// <summary>
/// Two-line status row: icon + label / summary above a band ruler.
/// Can optionally render a small milestone strip below the ruler.
/// </summary>
public static class D2BandRulerRow
{
    public readonly struct Milestone
    {
        public readonly int BandIndex;
        public readonly float InBandOffset;
        public readonly Texture2D Icon;
        public readonly bool Active;
        public readonly string Tooltip;
        public readonly string Id;

        public Milestone(int bandIndex, float inBandOffset, Texture2D icon, bool active, string tooltip = null, string id = null)
        {
            BandIndex = bandIndex;
            InBandOffset = inBandOffset;
            Icon = icon;
            Active = active;
            Tooltip = tooltip;
            Id = id;
        }
    }

    public static float Height(UIContext ctx, bool hasMilestones = false)
    {
        if (ctx == null)
            return hasMilestones ? 60f : 40f;

        float height = ctx.Style.Line + ctx.Style.GapXS + ctx.Style.RulerHeight;
        if (hasMilestones)
            height += ctx.Style.GapXS + ctx.Style.RulerMilestoneStripHeight;
        return height;
    }

    public static void Draw(
        UIContext ctx,
        Rect rect,
        Texture2D icon,
        string labelText,
        string summaryText,
        int value,
        int min,
        int max,
        IReadOnlyList<D2BandRuler.Band> bands,
        string id,
        string tooltip = null,
        IReadOnlyList<Milestone> milestones = null)
    {
        if (ctx == null)
            return;

        string root = id ?? "BandRulerRow";
        bool hasMilestones = milestones != null && milestones.Count > 0;
        float topH = ctx.Style.Line;
        float gap = ctx.Style.GapXS;
        Rect topRect = new(rect.x, rect.y, rect.width, topH);
        Rect rulerRect = new(rect.x, topRect.yMax + gap, rect.width, ctx.Style.RulerHeight);
        Rect stripRect = hasMilestones
            ? new Rect(rect.x, rulerRect.yMax + gap, rect.width, ctx.Style.RulerMilestoneStripHeight)
            : Rect.zero;

        ctx.RecordRect(rect, UIRectTag.Input, root, null);
        DrawTopRow(ctx, topRect, icon, labelText, summaryText, root + "/Summary", tooltip);
        D2BandRuler.Draw(ctx, rulerRect, value, min, max, bands, root + "/Ruler", tooltip: null);

        if (hasMilestones)
            DrawMilestoneStrip(ctx, stripRect, milestones, bands?.Count ?? 0, root + "/Milestones");
    }

    public static void Draw(
        UIContext ctx,
        Rect rect,
        string labelText,
        string summaryText,
        int value,
        int min,
        int max,
        IReadOnlyList<D2BandRuler.Band> bands,
        string id,
        string tooltip = null)
    {
        Draw(ctx, rect, null, labelText, summaryText, value, min, max, bands, id, tooltip, null);
    }

    private static void DrawTopRow(UIContext ctx, Rect rect, Texture2D icon, string labelText, string summaryText, string id, string tooltip)
    {
        ctx.RecordRect(rect, UIRectTag.Label, id, null);
        if (!tooltip.NullOrEmpty())
        {
            D2Widgets.TooltipHotspot(ctx, rect, id + "/Tooltip");
            if (ctx.Pass == UIPass.Draw)
                TooltipHandler.TipRegion(rect, tooltip);
        }

        float iconSlot = icon != null ? Mathf.Max(ctx.Style.IconSize, ctx.Style.IconVisualSize + (ctx.Style.IconInset * 2f)) : 0f;
        float summaryMaxWidth = Mathf.Max(80f, rect.width * 0.48f);
        float summaryWidth = Mathf.Clamp(MeasureTextWidth(summaryText) + ctx.Style.TextInsetX, Mathf.Min(110f, summaryMaxWidth), summaryMaxWidth);
        var h = new D2HRow(ctx, rect);
        Rect leftRect = h.NextFixed(Mathf.Max(0f, rect.width - summaryWidth - ctx.Style.GapXS), UIRectTag.Label, id + "/Left");
        Rect rightRect = h.Remaining(UIRectTag.Label, id + "/Right");

        if (icon != null)
        {
            var leftRow = new D2HRow(ctx, leftRect);
            Rect iconRect = leftRow.Next(iconSlot, iconSlot, UIRectTag.Icon, id + "/Icon");
            Rect labelRect = leftRow.Remaining(UIRectTag.Label, id + "/Label");
            D2Widgets.DrawTextureFitted(ctx, iconRect.ContractedBy(ctx.Style.IconInset), icon, id + "/IconDraw");
            D2Widgets.LabelClippedAligned(ctx, labelRect, labelText ?? string.Empty, TextAnchor.MiddleLeft, id + "/LabelText");
        }
        else
        {
            D2Widgets.LabelClippedAligned(ctx, leftRect, labelText ?? string.Empty, TextAnchor.MiddleLeft, id + "/LabelText");
        }

        D2Widgets.LabelClippedAligned(ctx, rightRect, summaryText ?? string.Empty, TextAnchor.MiddleRight, id + "/SummaryText");
    }

    private static void DrawMilestoneStrip(UIContext ctx, Rect rect, IReadOnlyList<Milestone> milestones, int bandCount, string id)
    {
        if (milestones == null || milestones.Count == 0 || bandCount <= 0)
            return;

        float iconSize = Mathf.Min(ctx.Style.RulerMilestoneIconSize, rect.height);
        float bandWidth = rect.width / bandCount;
        Rect lineRect = new(rect.x, rect.center.y, rect.width, 1f);
        D2Widgets.DrawDivider(ctx, lineRect, id + "/Guide");

        for (int i = 0; i < milestones.Count; i++)
        {
            Milestone milestone = milestones[i];
            if (milestone.Icon == null || milestone.BandIndex < 0 || milestone.BandIndex >= bandCount)
                continue;

            float centerX = rect.x + ((milestone.BandIndex + 0.5f + (milestone.InBandOffset * 0.28f)) * bandWidth);
            Rect iconRect = new(centerX - (iconSize * 0.5f), rect.center.y - (iconSize * 0.5f), iconSize, iconSize);
            string root = id + "/" + (milestone.Id ?? ("Milestone[" + i + "]"));
            ctx.RecordRect(iconRect, UIRectTag.Icon, root, milestone.Tooltip);

            if (!milestone.Tooltip.NullOrEmpty())
            {
                D2Widgets.TooltipHotspot(ctx, iconRect, root + "/Tooltip");
                if (ctx.Pass == UIPass.Draw)
                    TooltipHandler.TipRegion(iconRect, milestone.Tooltip);
            }

            if (ctx.Pass == UIPass.Measure)
                continue;

            float alpha = milestone.Active ? 1f : 0.40f;
            using (new GUIColorScope(new Color(1f, 1f, 1f, alpha)))
                GUI.DrawTexture(iconRect, milestone.Icon, ScaleMode.ScaleToFit, true);
        }
    }

    private static float MeasureTextWidth(string text)
    {
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, false))
            return Text.CalcSize(text ?? string.Empty).x;
    }
}
