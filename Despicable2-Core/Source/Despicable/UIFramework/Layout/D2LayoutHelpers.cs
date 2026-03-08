using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Small "cookbook" helpers that encode common layout policy decisions.
/// These helpers do NOT draw widgets; they just allocate rects consistently.
/// </summary>
public static class D2LayoutHelpers
{
    /// <summary>
    /// Split a rect into two columns with a fixed left width.
    /// </summary>
    public static void SplitColumns(Rect outer, float leftWidth, float gap, out Rect left, out Rect right)
    {
        float lw = Mathf.Clamp(leftWidth, 0f, outer.width);
        RectSplit.SplitVertical(outer, lw, gap, out left, out right);
    }

    /// <summary>
    /// Split a rect into a top and bottom slice, guaranteeing a minimum bottom height.
    /// If the outer rect is too small, the bottom gets as much as possible and the top may shrink.
    /// </summary>
    public static void SplitTopBottomMin(Rect outer, float minBottomHeight, float gap, out Rect top, out Rect bottom)
    {
        float mb = Mathf.Max(0f, minBottomHeight);

        // If outer is too short to satisfy min + gap, give bottom what we can.
        if (outer.height <= mb)
        {
            top = new Rect(outer.x, outer.y, outer.width, 0f);
            bottom = outer;
            return;
        }

        float topH = Mathf.Max(0f, outer.height - mb - gap);
        RectSplit.SplitHorizontal(outer, topH, gap, out top, out bottom);
    }

    /// <summary>
    /// Split a rect into a top and bottom slice, guaranteeing a minimum top height and attempting
    /// to guarantee a minimum bottom height.
    ///
    /// Policy:
    /// - Top gets at least <paramref name="minTopHeight"/> whenever possible.
    /// - Bottom gets <paramref name="minBottomHeight"/> when there is enough space; otherwise it shrinks.
    ///
    /// This is useful for "List + Details" layouts where the list must remain usable (e.g. show N rows),
    /// while the details area can become scrollable.
    /// </summary>
    public static void SplitTopBottomMinBoth(Rect outer, float minTopHeight, float minBottomHeight, float gap, out Rect top, out Rect bottom)
    {
        float mt = Mathf.Max(0f, minTopHeight);
        float mb = Mathf.Max(0f, minBottomHeight);

        // Not enough space to even satisfy the top slice.
        if (outer.height <= mt)
        {
            top = outer;
            bottom = new Rect(outer.x, outer.yMax, outer.width, 0f);
            return;
        }

        // Ensure we only keep a gap when there is room for both slices.
        float availableForBottom = outer.height - mt - gap;
        if (availableForBottom <= 0f)
        {
            top = outer;
            bottom = new Rect(outer.x, outer.yMax, outer.width, 0f);
            return;
        }

        // If we can satisfy both minimums, give the bottom its minimum and the top the remainder.
        if (outer.height >= mt + gap + mb)
        {
            float topH = Mathf.Max(mt, outer.height - mb - gap);
            RectSplit.SplitHorizontal(outer, topH, gap, out top, out bottom);
            return;
        }

        // Otherwise, pin the top to its minimum, and give the bottom whatever is left.
        RectSplit.SplitHorizontal(outer, mt, gap, out top, out bottom);
    }



    /// <summary>
    /// Split a rect into two even columns.
    /// </summary>
    public static void SplitEvenColumns(Rect outer, float gap, out Rect left, out Rect right)
    {
        float lw = Mathf.Max(0f, (outer.width - gap) * 0.5f);
        RectSplit.SplitVertical(outer, lw, gap, out left, out right);
    }

    /// <summary>
    /// Split a rect into three even columns.
    /// </summary>
    public static void SplitThreeColumns(Rect outer, float gap, out Rect first, out Rect second, out Rect third)
    {
        float cell = Mathf.Max(0f, (outer.width - (gap * 2f)) / 3f);
        Rect tail;
        RectSplit.SplitVertical(outer, cell, gap, out first, out tail);
        RectSplit.SplitVertical(tail, cell, gap, out second, out third);
    }

    /// <summary>
    /// Sum a vertical stack of rows separated by a consistent gap.
    /// </summary>
    public static float MeasureStack(float gap, params float[] heights)
    {
        if (heights == null || heights.Length == 0)
            return 0f;

        float total = 0f;
        int count = 0;
        for (int i = 0; i < heights.Length; i++)
        {
            float h = Mathf.Max(0f, heights[i]);
            if (h <= 0f)
                continue;

            total += h;
            count++;
        }

        if (count > 1)
            total += gap * (count - 1);

        return total;
    }

    /// <summary>
    /// Measure a simple row list inside a padded panel.
    /// </summary>
    public static float MeasureRows(float rowHeight, int rowCount, float gap, float padding = 0f, float minHeight = 0f)
    {
        int rows = Mathf.Max(0, rowCount);
        float inner = (rows * Mathf.Max(0f, rowHeight)) + (Mathf.Max(0, rows - 1) * Mathf.Max(0f, gap));
        float total = inner + Mathf.Max(0f, padding * 2f);
        return Mathf.Max(minHeight, total);
    }

    /// <summary>
    /// Measure wrapped text with optional vertical padding.
    /// </summary>
    public static float MeasureWrappedText(UIContext ctx, string text, float width, GameFont font, float padding = 0f, float minHeight = 0f)
    {
        float innerW = Mathf.Max(0f, width - (padding * 2f));
        float innerH = D2Text.ParagraphHeight(ctx, text ?? string.Empty, innerW, font);
        float total = innerH + (Mathf.Max(0f, padding) * 2f);
        return Mathf.Max(minHeight, total);
    }

    /// <summary>
    /// Measure a bullet list that uses the framework bullet layout.
    /// </summary>
    public static float MeasureBulletList(UIContext ctx, IEnumerable<string> items, float width, GameFont font, float padding = 0f, float bulletIndent = 18f, float bulletGap = 4f, float itemGap = 0f)
    {
        if (items == null)
            return Mathf.Max(0f, padding * 2f);

        float total = Mathf.Max(0f, padding * 2f);
        float innerW = Mathf.Max(0f, width - (padding * 2f) - bulletIndent - bulletGap);
        int count = 0;
        foreach (string item in items)
        {
            total += D2Text.ParagraphHeight(ctx, item ?? string.Empty, innerW, font);
            count++;
        }

        if (count > 1)
            total += Mathf.Max(0f, itemGap) * (count - 1);

        return total;
    }

    /// <summary>
    /// Compute how many grid columns fit in the given width.
    /// </summary>
    public static int ComputeGridColumns(float width, float itemSize, float gap)
    {
        float step = Mathf.Max(1f, itemSize + gap);
        return Mathf.Max(1, Mathf.FloorToInt((Mathf.Max(0f, width) + gap) / step));
    }

    /// <summary>
    /// Measure a simple icon grid.
    /// </summary>
    public static float MeasureGrid(float width, int itemCount, float itemSize, float gap, float padding = 0f, int minRows = 1)
    {
        float innerW = Mathf.Max(0f, width - (padding * 2f));
        int columns = ComputeGridColumns(innerW, itemSize, gap);
        int rows = itemCount <= 0 ? Mathf.Max(1, minRows) : Mathf.Max(minRows, Mathf.CeilToInt((float)itemCount / Mathf.Max(1, columns)));
        float inner = (rows * Mathf.Max(0f, itemSize)) + (Mathf.Max(0, rows - 1) * Mathf.Max(0f, gap));
        return inner + (Mathf.Max(0f, padding) * 2f);
    }



    /// <summary>
    /// Compute how many grid rows are needed for a flat item count and known column count.
    /// </summary>
    public static int ComputeGridRows(int itemCount, int columns, int minRows = 1)
    {
        int cols = Mathf.Max(1, columns);
        if (itemCount <= 0)
            return Mathf.Max(1, minRows);

        return Mathf.Max(minRows, Mathf.CeilToInt((float)itemCount / cols));
    }

    /// <summary>
    /// Measure a card/tile grid that fits columns by minimum cell width and uses a fixed cell height.
    /// This is the reusable case for gallery UIs that need rectangular tiles instead of square icon cells.
    /// </summary>
    public static float MeasureGridRows(float width, int itemCount, float minCellWidth, float cellHeight, float gap, float padding = 0f, int minRows = 1)
    {
        float innerW = Mathf.Max(0f, width - (padding * 2f));
        int columns = ComputeGridColumns(innerW, Mathf.Max(1f, minCellWidth), gap);
        int rows = ComputeGridRows(itemCount, columns, minRows);
        float inner = (rows * Mathf.Max(0f, cellHeight)) + (Mathf.Max(0, rows - 1) * Mathf.Max(0f, gap));
        return inner + (Mathf.Max(0f, padding) * 2f);
    }

    /// <summary>
    /// Allocate a consistent section header row.
    /// Medium font looks cramped in a 24px "line", so we give it a slightly taller row.
    /// </summary>
    public static Rect NextSectionHeader(UIContext ctx, ref VStack v, string label)
    {
        float h = Mathf.Max(ctx.Style.RowHeight, 28f);
        return v.Next(h, UIRectTag.Label, label);
    }

    /// <summary>
    /// Split a single row into (label | control) with a fixed label width.
    /// </summary>
    public static void SplitLabeledRow(Rect row, float labelWidth, float gap, out Rect labelRect, out Rect controlRect)
    {
        float lw = Mathf.Clamp(labelWidth, 0f, row.width);
        RectSplit.SplitVertical(row, lw, gap, out labelRect, out controlRect);
    }
}
