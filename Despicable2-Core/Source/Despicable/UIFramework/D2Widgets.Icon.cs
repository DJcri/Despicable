using UnityEngine;
using Verse;

namespace Despicable.UIFramework;

public static partial class D2Widgets
{
    /// <summary>
    /// Icon-only button for micro-actions.
    /// Uses the vanilla fitted-image button path so callers do not accidentally put tiny icons on chunky text-button chrome.
    /// </summary>
    public static bool ButtonIcon(UIContext ctx, Rect rect, Texture2D tex, string tooltip = null, Color? color = null, string label = null)
    {
        string id = label ?? "ButtonIcon";
        Rect iconRect = rect.ContractedBy(ctx?.Style?.IconInset ?? 2f);

        ctx?.Record(rect, UIRectTag.Button, id);
        ctx?.Record(iconRect, UIRectTag.Icon, id + "/Icon");

        if (ctx != null && ctx.Pass == UIPass.Measure)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        if (tex == null)
            return false;

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            if (color.HasValue)
                return Widgets.ButtonImageFitted(rect, tex, color.Value);

            return Widgets.ButtonImageFitted(rect, tex);
        }
    }

    /// <summary>
    /// Icon-only hitbox with manual draw path.
    /// Useful when callers want the vanilla-ish hover highlight plus a large invisible hitbox around a small icon.
    /// </summary>
    public static bool ButtonIconInvisible(UIContext ctx, Rect rect, Texture tex, string tooltip = null, Color? color = null, bool drawHoverHighlight = true, string label = null)
    {
        string id = label ?? "ButtonIconInvisible";
        Rect iconRect = rect.ContractedBy(ctx?.Style?.IconInset ?? 2f);

        ctx?.Record(rect, UIRectTag.Button, id);
        ctx?.Record(iconRect, UIRectTag.Icon, id + "/Icon");

        if (ctx != null && ctx.Pass == UIPass.Measure)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        if (tex == null)
            return false;

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            if (drawHoverHighlight)
                Widgets.DrawHighlightIfMouseover(rect);

            if (color.HasValue)
            {
                using (new GUIColorScope(color.Value))
                    GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit, true);
            }
            else
            {
                GUI.DrawTexture(iconRect, tex, ScaleMode.ScaleToFit, true);
            }

            return Widgets.ButtonInvisible(rect);
        }
    }
}
