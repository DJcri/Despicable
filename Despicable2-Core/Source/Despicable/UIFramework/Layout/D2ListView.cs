using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Standardized list view helper:
/// - fixed row height (RimWorld-style)
/// - optional selection and zebra striping
/// - scroll view integration
/// - records row rects for validation/debug overlay
/// </summary>
public static class D2ListView
{
    public delegate void RowDrawer<T>(UIContext ctx, Rect rowRect, T item, int index, bool selected);

    // Local no-op disposable to keep `using (...)` clean when ctx == null.
    private readonly struct NullScope : IDisposable
    {
        public void Dispose() { }
    }

    public static float DefaultRowHeight(UIContext ctx)
    {
        if (ctx?.Style == null) return 28f;
        // Canonical list row height: use RowHeight when available, fall back to a safe minimum.
        return Math.Max(28f, ctx.Style.RowHeight);
    }

    public static float CalcContentHeight(int count, float rowHeight, float gap = 0f)
    {
        if (count <= 0) return 0f;
        return (count * rowHeight) + ((count - 1) * gap);
    }

    /// <summary>
    /// Draw a scrollable list.
    /// Caller supplies a row drawer that can use D2Widgets/Widgets as desired.
    /// </summary>
    public static void Draw<T>(
        UIContext ctx,
        Rect outRect,
        ref Vector2 scroll,
        IList<T> items,
        ref int selectedIndex,
        RowDrawer<T> drawRow,
        float? rowHeightOverride = null,
        float rowGap = 0f,
        bool zebra = false,
        string label = "ListView")
    {
        if (items == null) return;

        IDisposable scope = null;
        try
        {
            scope = (ctx != null) ? (IDisposable)ctx.PushScope(label) : new NullScope();

            float rowH = rowHeightOverride ?? DefaultRowHeight(ctx);
            float contentH = CalcContentHeight(items.Count, rowH, rowGap);

            // Only reserve scrollbar width when we actually need a scrollbar.
            // (If we always subtract 16px, short lists get an odd "dead" vertical strip.)
            bool needsScrollbar = contentH > outRect.height + 0.5f;
            float scrollbarW = needsScrollbar ? 16f : 0f;

            // Record the visible scroll viewport.
            ctx?.Record(outRect, UIRectTag.ScrollView, "ScrollView");

            // RimWorld expects viewRect to represent the content size. When content is shorter than
            // the viewport, giving viewRect a tiny height can create strange "broken" dead zones.
            // Clamp to at least viewport height for stable visuals and hit testing.
            float viewH = Mathf.Max(contentH, outRect.height);
            Rect viewRect = new(0f, 0f, outRect.width - scrollbarW, viewH);

            if (ctx != null && ctx.Pass == UIPass.Measure)
            {
                // Measure pass: record row rects so ContentMaxY is meaningful, but do not emit widgets.
                float yMeasure = 0f;
                for (int i = 0; i < items.Count; i++)
                {
                    Rect row = new(0f, yMeasure, viewRect.width, rowH);
                    var tag = (i == selectedIndex) ? UIRectTag.ListRowSelected : UIRectTag.ListRow;
                    ctx.Record(row, tag, $"Row[{i}]");
                    yMeasure += rowH + rowGap;
                }
                return;
            }

            // D2ListView is a vertical-only list. Keep horizontal scroll pinned shut so
            // stale Vector2 state never manifests as a phantom horizontal scrollbar.
            scroll.x = 0f;
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            scroll.x = 0f;
            try
            {
                if (ctx != null)
                {
                    using (ctx.PushOffset(outRect.position - scroll))
                    {
                        DrawRows(ctx, viewRect, items, ref selectedIndex, drawRow, rowH, rowGap, zebra);
                    }
                }
                else
                {
                    DrawRows(null, viewRect, items, ref selectedIndex, drawRow, rowH, rowGap, zebra);
                }
            }
            finally
            {
                Widgets.EndScrollView();
                scroll.x = 0f;
            }
        }
        finally
        {
            scope?.Dispose();
        }
    }

    private static void DrawRows<T>(
        UIContext ctx,
        Rect viewRect,
        IList<T> items,
        ref int selectedIndex,
        RowDrawer<T> drawRow,
        float rowH,
        float rowGap,
        bool zebra)
    {
        float y = 0f;

        for (int i = 0; i < items.Count; i++)
        {
            Rect row = new(0f, y, viewRect.width, rowH);

            bool selected = i == selectedIndex;

            // Record first, so validation knows about it even if drawer early-returns.
            ctx?.Record(row, selected ? UIRectTag.ListRowSelected : UIRectTag.ListRow, $"Row[{i}]");

            if (zebra && (i % 2 == 1))
                Widgets.DrawAltRect(row);

            if (selected)
                Widgets.DrawHighlightSelected(row);
            else
                Widgets.DrawHighlightIfMouseover(row);

            // Selection hitbox
            if (Widgets.ButtonInvisible(row))
                selectedIndex = i;

            drawRow?.Invoke(ctx, row, items[i], i, selected);

            y += rowH + rowGap;
        }
    }
}
