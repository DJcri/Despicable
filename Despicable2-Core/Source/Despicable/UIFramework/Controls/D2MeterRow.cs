using UnityEngine;
using Verse;

using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Compact icon/label/value/meter row for status summaries.
/// Keeps layout measure-safe, records rects for overlays, and centralizes
/// the very common "icon + number + fill bar + tooltip" pattern.
/// </summary>
public static class D2MeterRow
{
    public static void Draw(UIContext ctx, Rect rect, Texture2D icon, int value, int min, int max, string id, string tooltip = null, string labelText = null, float labelWidth = 0f, float valueWidth = 48f, float? iconSizeOverride = null)
    {
        ctx?.Record(rect, UIRectTag.Input, id ?? "MeterRow");

        var row = new HRow(ctx, rect);
        float iconSize = Mathf.Min(rect.height, iconSizeOverride ?? 20f);
        Rect iconRect = row.Next(iconSize, iconSize, UIRectTag.Icon, (id ?? "MeterRow") + "/Icon");

        Rect labelRect = default;
        bool drawLabel = !string.IsNullOrEmpty(labelText) && labelWidth > 0f;
        if (drawLabel)
            labelRect = row.NextFixed(labelWidth, UIRectTag.Label, (id ?? "MeterRow") + "/Label");

        Rect valueRect = row.NextFixed(valueWidth, UIRectTag.Label, (id ?? "MeterRow") + "/Value");
        Rect barRect = row.Remaining(UIRectTag.Input, (id ?? "MeterRow") + "/Bar");

        if (ctx != null && ctx.Pass == UIPass.Measure)
            return;

        if (icon != null)
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);

        if (drawLabel)
            D2Widgets.LabelClippedAligned(ctx, labelRect, labelText, TextAnchor.MiddleLeft, (id ?? "MeterRow") + "/LabelText", labelText);
        D2Widgets.LabelClippedAligned(ctx, valueRect, value.ToString(), TextAnchor.MiddleRight, (id ?? "MeterRow") + "/ValueText", value.ToString());

        Widgets.FillableBar(barRect, Mathf.InverseLerp(min, max, value));

        if (!string.IsNullOrEmpty(tooltip))
        {
            D2Widgets.TooltipHotspot(ctx, rect, (id ?? "MeterRow") + "/Tooltip");
            TooltipHandler.TipRegion(rect, tooltip);
        }
    }
}
