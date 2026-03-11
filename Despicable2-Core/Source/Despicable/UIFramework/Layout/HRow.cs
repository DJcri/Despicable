using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Layout;
/// <summary>
/// Horizontal allocator for a single row.
/// For stacks of rows, allocate row rects with VStack, then make an HRow per row rect.
///
/// Key behaviors:
/// - Never allocates negative widths.
/// - Clamps allocations to remaining width (prevents "push out of bounds" rows).
/// - Leaves validation to report TooSmall/Truncation when things get cramped.
/// </summary>
public struct HRow
{
    private readonly UIContext _ctx;
    private Rect _bounds;
    private float _x;

    public Rect Bounds => _bounds;
    public float RemainingWidth => _bounds.xMax - _x;

    public HRow(UIContext ctx, Rect bounds)
    {
        _ctx = ctx;
        _bounds = bounds;
        _x = bounds.xMin;
    }

    public Rect Next(float width, float height, UIRectTag tag = UIRectTag.None, string label = null)
    {
        if (width < 0f) width = 0f;
        if (height < 0f) height = 0f;

        float remaining = RemainingWidth;
        if (remaining <= 0f)
        {
            // We're already out of room: return an empty rect at the row end.
            var empty = new Rect(_bounds.xMax, _bounds.yMin, 0f, height);
            _ctx.Record(empty, tag, label);
            _x = _bounds.xMax;
            return empty;
        }

        // Clamp to avoid allocating past bounds.
        float clampedW = Mathf.Min(width, remaining);

        var r = new Rect(_x, _bounds.yMin, clampedW, height);
        _ctx.Record(r, tag, label);

        // Advance. Only add a gap if there's still space remaining.
        _x = r.xMax;
        if (_x < _bounds.xMax)
            _x = Mathf.Min(_bounds.xMax, _x + _ctx.Style.Gap);

        return r;
    }

    public Rect NextFixed(float width, UIRectTag tag = UIRectTag.None, string label = null)
        => Next(width, _bounds.height, tag, label);

    public Rect NextIcon(float? sizeOverride = null, UIRectTag tag = UIRectTag.Icon, string label = null)
    {
        float s = sizeOverride ?? _ctx.Style.IconSize;
        return Next(s, s, tag, label);
    }

    /// <summary>
    /// Allocate the remaining width.
    /// </summary>
    public Rect Remaining(UIRectTag tag = UIRectTag.None, string label = null)
    {
        float w = Mathf.Max(0f, _bounds.xMax - _x);
        var r = new Rect(_x, _bounds.yMin, w, _bounds.height);
        _x = _bounds.xMax;
        _ctx.Record(r, tag, label);
        return r;
    }

    /// <summary>
    /// Allocate the remainder of the available horizontal space.
    /// Semantic alias for Remaining(...).
    ///
    /// Common use: left fixed icon/label, right fill content.
    /// </summary>
    public Rect NextFillWidth(UIRectTag tag = UIRectTag.None, string label = null)
        => Remaining(tag, label);

    /// <summary>
    /// Allocate at least <paramref name="minWidth"/> pixels, or more if the remaining
    /// space is larger. The returned rect may extend past <see cref="Bounds"/> when
    /// remaining space is less than minWidth — this is intentional so the control
    /// renders at its declared minimum rather than silently collapsing.
    /// </summary>
    public Rect RemainingMin(float minWidth, UIRectTag tag = UIRectTag.None, string label = null)
    {
        float remaining = Mathf.Max(0f, _bounds.xMax - _x);
        float w = Mathf.Max(minWidth, remaining);
        var r = new Rect(_x, _bounds.yMin, w, _bounds.height);
        _x = _bounds.xMax;   // always advance to the logical end of the row
        _ctx.Record(r, tag, label);
        return r;
    }
}
