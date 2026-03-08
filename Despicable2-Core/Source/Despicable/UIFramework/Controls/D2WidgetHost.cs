using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Lightweight dashboard/widget host with simple flow packing.
///
/// Widgets declare preferred width and fixed height. The host packs them left-to-right and wraps as needed.
/// </summary>
public static class D2WidgetHost
{
    public sealed class WidgetDef
    {
        public string Id;
        public string Label;
        public float PreferredWidth = 180f;
        public float Height = 96f;
        public bool Visible = true;
        public bool Soft = true;
        public Action<UIContext, Rect> Draw;

        public WidgetDef(string id, string label, Action<UIContext, Rect> draw = null)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Draw = draw;
        }
    }

    public struct State
    {
        public Vector2 Scroll;
    }

    public static float MeasureContentHeight(UIContext ctx, Rect rect, IList<WidgetDef> widgets)
    {
        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
        if (widgets == null || widgets.Count == 0)
            return 0f;

        float x = 0f;
        float y = 0f;
        float rowH = 0f;
        float width = Mathf.Max(0f, rect.width - 16f);

        for (int i = 0; i < widgets.Count; i++)
        {
            WidgetDef widget = widgets[i];
            if (widget == null || !widget.Visible)
                continue;

            float w = Mathf.Clamp(widget.PreferredWidth, 120f, Mathf.Max(120f, width));
            float h = Mathf.Max(48f, widget.Height);

            if (x > 0f && x + w > width)
            {
                x = 0f;
                y += rowH + gap;
                rowH = 0f;
            }

            x += (x > 0f ? gap : 0f) + w;
            if (h > rowH)
                rowH = h;
        }

        return y + rowH;
    }

    public static void Draw(UIContext ctx, Rect rect, IList<WidgetDef> widgets, ref State state, string label = "WidgetHost")
    {
        float contentH = Mathf.Max(rect.height, MeasureContentHeight(ctx, rect, widgets));
        Rect view = new(0f, 0f, Mathf.Max(0f, rect.width - 16f), contentH);

        Widgets.BeginScrollView(rect, ref state.Scroll, view);
        try
        {
            using (ctx.PushOffset(rect.position - state.Scroll))
            {
                DrawContent(ctx, view, widgets, label);
            }
        }
        finally
        {
            Widgets.EndScrollView();
        }
    }

    private static void DrawContent(UIContext ctx, Rect view, IList<WidgetDef> widgets, string label)
    {
        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
        float x = 0f;
        float y = 0f;
        float rowH = 0f;
        float width = view.width;

        if (widgets == null)
            return;

        for (int i = 0; i < widgets.Count; i++)
        {
            WidgetDef widget = widgets[i];
            if (widget == null || !widget.Visible)
                continue;

            float w = Mathf.Clamp(widget.PreferredWidth, 120f, Mathf.Max(120f, width));
            float h = Mathf.Max(48f, widget.Height);

            if (x > 0f && x + w > width)
            {
                x = 0f;
                y += rowH + gap;
                rowH = 0f;
            }

            if (x > 0f)
                x += gap;

            Rect slot = new(x, y, Mathf.Min(w, width), h);
            using (var g = ctx.GroupPanel(label + "/Widget[" + i + "]", slot, soft: widget.Soft, pad: true, drawBackground: true, label: widget.Label))
            {
                widget.Draw?.Invoke(ctx, g.Inner);
            }

            x += slot.width;
            if (h > rowH)
                rowH = h;
        }
    }
}
