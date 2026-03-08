using UnityEngine;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Simple horizontal flow layout with wrapping.
/// Useful for rows containing multiple variable-width controls (e.g. checkboxes with long labels).
///
/// Typical usage:
/// var f = new HFlow(ctx, rect);
/// Rect a = f.Next(minWidthA);
/// Rect b = f.Next(minWidthB);
///
/// If a rect would exceed the available width, HFlow wraps to the next line.
/// </summary>
public sealed class HFlow
{
    private readonly UIContext _ctx;
    private readonly Rect _outer;

    private float _x;
    private float _y;
    private float _lineH;
    private float _gap;

    public HFlow(UIContext ctx, Rect outer, float? lineHeight = null, float? gap = null)
    {
        _ctx = ctx;
        _outer = outer;
        _x = outer.xMin;
        _y = outer.yMin;
        _lineH = lineHeight ?? ctx?.Style?.Line ?? 24f;
        _gap = gap ?? ctx?.Style?.Gap ?? 6f;
    }

    public float CursorY => _y;

    /// <summary>
    /// Returns the next rect in the flow.
    /// minWidth is required. The returned rect's height is the flow line height.
    /// </summary>
    public Rect Next(float minWidth)
    {
        if (minWidth < 0f) minWidth = 0f;

        float remaining = _outer.xMax - _x;

        // Wrap if we don't have enough room (and we're not at line start).
        if (_x > _outer.xMin && minWidth > remaining)
        {
            _x = _outer.xMin;
            _y += _lineH + _gap;
        }

        float w = Mathf.Min(minWidth, _outer.xMax - _x);
        Rect r = new(_x, _y, w, _lineH);
        _x += w + _gap;
        return r;
    }

    /// <summary>
    /// Starts a new line explicitly.
    /// </summary>
    public void NewLine()
    {
        _x = _outer.xMin;
        _y += _lineH + _gap;
    }
}
