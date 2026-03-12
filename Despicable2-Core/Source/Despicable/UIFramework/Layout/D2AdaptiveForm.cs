using System;
using System.Collections.Generic;
using UnityEngine;
using Despicable.UIFramework.Controls;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Compact adaptive form layout for inspector-style rows.
///
/// Keeps label/control alignment stable and removes a lot of hand-written D2HRow boilerplate.
/// </summary>
public static class D2AdaptiveForm
{
    public sealed class RowSpec
    {
        public string Id;
        public string Label;
        public float LabelWeight = 0.4f;
        public float Height = 0f;
        public Action<UIContext, Rect> DrawValue;
        public string Tooltip;

        public RowSpec(string id, string label, Action<UIContext, Rect> drawValue = null)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            DrawValue = drawValue;
        }
    }

    public static float MeasureHeight(UIContext ctx, IList<RowSpec> rows)
    {
        if (rows == null || rows.Count == 0)
            return 0f;

        float rowH = ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f;
        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
        float total = 0f;
        for (int i = 0; i < rows.Count; i++)
        {
            if (i > 0) total += gap;
            total += rows[i] != null && rows[i].Height > 0f ? rows[i].Height : rowH;
        }
        return total;
    }

    public static void Draw(UIContext ctx, Rect rect, IList<RowSpec> rows, string label = "AdaptiveForm")
    {
        if (rows == null || rows.Count == 0)
            return;

        float rowH = ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f;
        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
        float y = rect.y;

        for (int i = 0; i < rows.Count; i++)
        {
            RowSpec row = rows[i];
            if (row == null)
                continue;

            float h = row.Height > 0f ? row.Height : rowH;
            Rect rowRect = new(rect.x, y, rect.width, h);
            DrawRow(ctx, rowRect, row, label + "/Row[" + i + "]");
            y += h + gap;
        }
    }

    private static void DrawRow(UIContext ctx, Rect rect, RowSpec row, string label)
    {
        ctx?.RecordRect(rect, UIRectTag.Input, label, null);

        float labelWeight = Mathf.Clamp01(row.LabelWeight);
        if (labelWeight <= 0f) labelWeight = 0.4f;
        if (labelWeight >= 1f) labelWeight = 0.6f;

        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
        float labelW = Mathf.Max(80f, rect.width * labelWeight);
        if (labelW > rect.width - gap)
            labelW = Mathf.Max(0f, rect.width - gap);

        Rect labelRect = new(rect.x, rect.y, labelW, rect.height);
        Rect valueRect = new(labelRect.xMax + gap, rect.y, Mathf.Max(0f, rect.xMax - (labelRect.xMax + gap)), rect.height);

        D2Widgets.LabelClipped(ctx, labelRect, row.Label ?? string.Empty, label + "/Label", tooltipOverride: row.Tooltip);
        row.DrawValue?.Invoke(ctx, valueRect);
    }
}
