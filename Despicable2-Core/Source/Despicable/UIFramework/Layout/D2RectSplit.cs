using UnityEngine;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Small helpers for splitting rects into panels/columns without manual xMin/xMax soup.
/// Intentionally tiny: just enough to keep new dialogs readable.
/// </summary>
public static class D2RectSplit
{
    public static void SplitVertical(Rect outer, float leftWidth, float gap, out Rect left, out Rect right)
    {
        if (leftWidth < 0f) leftWidth = 0f;
        if (gap < 0f) gap = 0f;

        float lw = Mathf.Min(leftWidth, outer.width);
        float rx = outer.xMin + lw + gap;
        float rw = Mathf.Max(0f, outer.xMax - rx);

        left = new Rect(outer.xMin, outer.yMin, lw, outer.height);
        right = new Rect(rx, outer.yMin, rw, outer.height);
    }

    public static void SplitHorizontal(Rect outer, float topHeight, float gap, out Rect top, out Rect bottom)
    {
        if (topHeight < 0f) topHeight = 0f;
        if (gap < 0f) gap = 0f;

        float th = Mathf.Min(topHeight, outer.height);
        float by = outer.yMin + th + gap;
        float bh = Mathf.Max(0f, outer.yMax - by);

        top = new Rect(outer.xMin, outer.yMin, outer.width, th);
        bottom = new Rect(outer.xMin, by, outer.width, bh);
    }

    /// <summary>
    /// Splits into N columns by weights. Gap applies between columns.
    /// Example: Columns(rect, gap, new[]{1f,2f,1f}, out cols) => 25%,50%,25%.
    /// </summary>
    public static Rect[] Columns(Rect outer, float gap, float[] weights)
    {
        if (weights == null || weights.Length == 0)
            return new Rect[0];

        if (gap < 0f) gap = 0f;
        int n = weights.Length;

        float totalWeight = 0f;
        for (int i = 0; i < n; i++)
            totalWeight += Mathf.Max(0.0001f, weights[i]);

        float totalGap = gap * (n - 1);
        float usable = Mathf.Max(0f, outer.width - totalGap);

        var cols = new Rect[n];
        float x = outer.xMin;

        for (int i = 0; i < n; i++)
        {
            float w = usable * (Mathf.Max(0.0001f, weights[i]) / totalWeight);
            cols[i] = new Rect(x, outer.yMin, w, outer.height);
            x += w + gap;
        }

        return cols;
    }
}
