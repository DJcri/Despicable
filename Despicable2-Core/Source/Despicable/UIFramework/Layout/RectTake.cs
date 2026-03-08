using UnityEngine;

namespace Despicable.UIFramework.Layout;

/// <summary>
/// Remainder-rect carving helpers.
/// Use these when a layout has a clear top/bottom/left/right space budget instead of rebuilding positions with hardcoded offsets.
/// </summary>
public static class RectTake
{
    public static Rect TakeTop(ref Rect remaining, float size)
    {
        float takenHeight = Mathf.Clamp(size, 0f, remaining.height);
        Rect taken = new(remaining.x, remaining.y, remaining.width, takenHeight);
        remaining = new Rect(remaining.x, remaining.y + takenHeight, remaining.width, Mathf.Max(0f, remaining.height - takenHeight));
        return taken;
    }

    public static Rect TakeBottom(ref Rect remaining, float size)
    {
        float takenHeight = Mathf.Clamp(size, 0f, remaining.height);
        Rect taken = new(remaining.x, remaining.yMax - takenHeight, remaining.width, takenHeight);
        remaining = new Rect(remaining.x, remaining.y, remaining.width, Mathf.Max(0f, remaining.height - takenHeight));
        return taken;
    }

    public static Rect TakeLeft(ref Rect remaining, float size)
    {
        float takenWidth = Mathf.Clamp(size, 0f, remaining.width);
        Rect taken = new(remaining.x, remaining.y, takenWidth, remaining.height);
        remaining = new Rect(remaining.x + takenWidth, remaining.y, Mathf.Max(0f, remaining.width - takenWidth), remaining.height);
        return taken;
    }

    public static Rect TakeRight(ref Rect remaining, float size)
    {
        float takenWidth = Mathf.Clamp(size, 0f, remaining.width);
        Rect taken = new(remaining.xMax - takenWidth, remaining.y, takenWidth, remaining.height);
        remaining = new Rect(remaining.x, remaining.y, Mathf.Max(0f, remaining.width - takenWidth), remaining.height);
        return taken;
    }
}
