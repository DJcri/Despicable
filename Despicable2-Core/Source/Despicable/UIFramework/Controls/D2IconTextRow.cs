using UnityEngine;
using Verse;

using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Compact icon + clipped text row with optional hover chrome, click hitbox, and tooltip.
/// Good for legends, help rows, and list entries that begin with a small icon.
/// </summary>
public static class D2IconTextRow
{
    public static bool Draw(UIContext ctx, Rect rect, Texture2D icon, string text, string id,
        string tooltip = null, float iconSize = 22f, bool clickable = false, bool hover = false)
    {
        ctx?.Record(rect, UIRectTag.Input, id ?? "IconTextRow"); // loc-allow-internal: fallback row id
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return false;

        if (hover)
            D2Widgets.HighlightOnHover(ctx, rect, (id ?? "IconTextRow") + "/Hover"); // loc-allow-internal: fallback row id

        bool clicked = clickable && D2Widgets.ButtonInvisible(ctx, rect, (id ?? "IconTextRow") + "/Hitbox"); // loc-allow-internal: fallback row id

        var row = new D2HRow(ctx, rect);
        Rect iconRect = row.Next(iconSize, iconSize, UIRectTag.Icon, (id ?? "IconTextRow") + "/Icon"); // loc-allow-internal: fallback row id
        Rect textRect = row.Remaining(UIRectTag.Label, (id ?? "IconTextRow") + "/Text"); // loc-allow-internal: fallback row id

        D2Widgets.DrawTextureFitted(ctx, iconRect, icon, (id ?? "IconTextRow") + "/IconDraw"); // loc-allow-internal: fallback row id
        D2Widgets.LabelClippedAligned(ctx, textRect, text ?? string.Empty, TextAnchor.MiddleLeft, (id ?? "IconTextRow") + "/TextDraw", tooltip == null ? text : null); // loc-allow-internal: fallback row id

        if (!tooltip.NullOrEmpty())
        {
            D2Widgets.TooltipHotspot(ctx, rect, (id ?? "IconTextRow") + "/Tooltip"); // loc-allow-internal: fallback row id
            TooltipHandler.TipRegion(rect, tooltip);
        }

        return clicked;
    }
}
