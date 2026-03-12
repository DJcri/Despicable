using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Simple icon tile with hover-aware fill, border, fitted icon, and tooltip hotspot.
/// Keeps tile rendering measure-safe and reusable across feature modules.
/// </summary>
public static class D2IconTile
{
    public static void Draw(UIContext ctx, Rect rect, Texture2D icon, string id, string tooltip = null,
        float iconInset = 6f, Color? idleFill = null, Color? hoverFill = null)
    {
        ctx?.Record(rect, UIRectTag.PanelSoft, id ?? "IconTile"); // loc-allow-internal: fallback tile id
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return;

        bool hovered = Mouse.IsOver(rect);
        Color fill = hovered
            ? (hoverFill ?? new Color(1f, 1f, 1f, 0.10f))
            : (idleFill ?? new Color(0f, 0f, 0f, 0.16f));

        D2Widgets.DrawBoxSolid(ctx, rect, fill, (id ?? "IconTile") + "/BG"); // loc-allow-internal: fallback tile id
        D2Widgets.DrawBox(ctx, rect, hovered ? 2 : 1, (id ?? "IconTile") + "/Border"); // loc-allow-internal: fallback tile id

        float inset = Mathf.Clamp(iconInset, 0f, Mathf.Min(rect.width, rect.height) * 0.45f);
        Rect iconRect = rect.ContractedBy(inset);
        D2Widgets.DrawTextureFitted(ctx, iconRect, icon, (id ?? "IconTile") + "/Icon"); // loc-allow-internal: fallback tile id

        if (!tooltip.NullOrEmpty())
        {
            D2Widgets.TooltipHotspot(ctx, rect, (id ?? "IconTile") + "/Tooltip"); // loc-allow-internal: fallback tile id
            TooltipHandler.TipRegion(rect, tooltip);
        }
    }
}
