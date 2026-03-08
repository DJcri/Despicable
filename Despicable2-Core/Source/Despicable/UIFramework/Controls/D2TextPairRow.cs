using UnityEngine;
using Verse;

using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Standard two-column text row with optional row chrome.
/// Useful for list entries, key/value tables, and compact summary rows.
/// </summary>
public static class D2TextPairRow
{
    public static bool Draw(UIContext ctx, Rect rect, string leftText, string rightText, string id,
        string tooltip = null, float leftWidthFraction = 0.65f, float? leftWidthOverride = null,
        bool selected = false, bool zebra = false, bool clickable = false,
        Color? selectedFill = null, Color? customFill = null,
        Color? leftColor = null, Color? rightColor = null)
    {
        ctx?.Record(rect, selected ? UIRectTag.ListRowSelected : UIRectTag.ListRow, id ?? "TextPairRow");

        if (ctx != null && ctx.Pass == UIPass.Measure)
            return false;

        if (zebra)
            D2Widgets.DrawAltRect(ctx, rect, (id ?? "TextPairRow") + "/Alt");

        if (selected)
            D2Widgets.DrawBoxSolid(ctx, rect, selectedFill ?? new Color(1f, 1f, 1f, 0.08f), (id ?? "TextPairRow") + "/Selected");
        else if (customFill.HasValue)
            D2Widgets.DrawBoxSolid(ctx, rect, customFill.Value, (id ?? "TextPairRow") + "/Fill");

        D2Widgets.HighlightOnHover(ctx, rect, (id ?? "TextPairRow") + "/Hover");
        bool clicked = clickable && D2Widgets.ButtonInvisible(ctx, rect, (id ?? "TextPairRow") + "/Hitbox");

        var h = new HRow(ctx, rect);
        float leftWidth = leftWidthOverride ?? (rect.width * Mathf.Clamp(leftWidthFraction, 0.1f, 0.9f));
        Rect leftRect = h.NextFixed(leftWidth, UIRectTag.Label, (id ?? "TextPairRow") + "/Left");
        Rect rightRect = h.Remaining(UIRectTag.Label, (id ?? "TextPairRow") + "/Right");

        D2Widgets.LabelClippedAligned(ctx, leftRect, leftText ?? string.Empty, TextAnchor.MiddleLeft, (id ?? "TextPairRow") + "/LeftText", null, leftColor);
        D2Widgets.LabelClippedAligned(ctx, rightRect, rightText ?? string.Empty, TextAnchor.MiddleRight, (id ?? "TextPairRow") + "/RightText", null, rightColor);

        if (!tooltip.NullOrEmpty())
        {
            D2Widgets.TooltipHotspot(ctx, rect, (id ?? "TextPairRow") + "/Tooltip");
            TooltipHandler.TipRegion(rect, tooltip);
        }

        return clicked;
    }
}
