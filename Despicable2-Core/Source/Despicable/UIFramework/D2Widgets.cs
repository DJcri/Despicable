using UnityEngine;
using Verse;

namespace Despicable.UIFramework;
/// <summary>
/// Thin wrappers that:
/// - record tags/labels for validation
/// - no-op during Measure pass (so measure doesn't mutate state or draw)
///
/// Vibe-coding rules:
/// - Allocate rects with VStack/HRow first.
/// - Then call D2Widgets to draw into those rects.
/// - If you draw something manually, call ctx.RecordRect(...) so overlay/validation can still see it.
/// </summary>
public static partial class D2Widgets
{
    private static string ResolveKeyText(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;
        return (args != null && args.Length > 0) ? key.Translate(args).ToString() : key.Translate().ToString();
    }

    private static Rect CenterY(Rect outer, float innerHeight)
    {
        if (innerHeight <= 0f || innerHeight >= outer.height) return outer;
        float y = outer.y + (outer.height - innerHeight) * 0.5f;
        return new Rect(outer.x, y, outer.width, innerHeight);
    }

    private static Rect ControlRect(UIContext ctx, Rect outer)
    {
        float h = ctx?.Style?.ControlHeight ?? 24f;
        return CenterY(outer, h);
    }

    /// <summary>
    /// Draws a single-line label clipped with an ellipsis if it does not fit.
    /// If clipped, adds a tooltip hotspot showing the full text.
    /// </summary>
    public static void LabelClipped(UIContext ctx, Rect rect, string text, string label = null, string tooltipOverride = null, Color? color = null)
    {
        LabelClippedAligned(ctx, rect, text, TextAnchor.UpperLeft, label, tooltipOverride, color);
    }

    public static void LabelClippedKey(UIContext ctx, Rect rect, string key, string label = null, string tooltipKey = null, Color? color = null, params object[] args)
    {
        string text = ResolveKeyText(key, args);
        string tooltip = string.IsNullOrEmpty(tooltipKey) ? null : ResolveKeyText(tooltipKey);
        LabelClipped(ctx, rect, text, label, tooltip, color);
    }

    public static void LabelClippedAligned(UIContext ctx, Rect rect, string text, TextAnchor anchor, string label = null, string tooltipOverride = null, Color? color = null)
    {
        ctx?.Record(rect, UIRectTag.Label, label ?? "LabelClipped");
        if (ctx != null && ctx.Pass == UIPass.Measure) return;

        if (text == null) text = string.Empty;

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(color ?? Color.white))
        using (new TextStateScope(GameFont.Small, anchor, true))
        {
            // GenText.Truncate is the RimWorld-idiomatic way to add ellipsis.
            string truncated = text.Truncate(rect.width);
            Widgets.Label(rect, truncated);

            // If we truncated (or caller explicitly wants a tooltip), show full text on hover.
            bool wasTruncated = truncated != text;
            string tip = tooltipOverride ?? (wasTruncated ? text : null);
            if (!string.IsNullOrEmpty(tip))
                TooltipHandler.TipRegion(rect, tip);
        }
    }

    public static void Label(UIContext ctx, Rect rect, string text, string label = null, Color? color = null)
    {
        ctx?.Record(rect, UIRectTag.Label, label ?? "Label");
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(color ?? Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            Widgets.Label(rect, text);
        }
    }

    public static void LabelKey(UIContext ctx, Rect rect, string key, string label = null, Color? color = null, params object[] args)
    {
        Label(ctx, rect, ResolveKeyText(key, args), label, color);
    }

    public static bool ButtonText(UIContext ctx, Rect rect, string text, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Button, label ?? text);
        if (ctx != null && ctx.Pass == UIPass.Measure) return false;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            return Widgets.ButtonText(rect, text);
        }
    }

    public static bool ButtonTextKey(UIContext ctx, Rect rect, string key, string label = null, params object[] args)
    {
        return ButtonText(ctx, rect, ResolveKeyText(key, args), label ?? key);
    }

    public static void DrawPanel(UIContext ctx, Rect rect, string label = null)
    {
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        ctx?.Record(rect, UIRectTag.Panel, label ?? "Panel");
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            Widgets.DrawMenuSection(rect);
        }
    }

    public static void DrawPanelSoft(UIContext ctx, Rect rect, string label = null)
    {
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        ctx?.Record(rect, UIRectTag.PanelSoft, label ?? "PanelSoft");
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            Widgets.DrawMenuSection(rect);
        }
    }

    public static void DrawBoxSolid(UIContext ctx, Rect rect, Color color, string label = null)
    {
        ctx?.Record(rect, UIRectTag.PanelSoft, label ?? "BoxSolid");
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            Widgets.DrawBoxSolid(rect, color);
        }
    }


    public static void DrawDivider(UIContext ctx, Rect rect, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Divider, label ?? "Divider");
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            Widgets.DrawLineHorizontal(rect.xMin, rect.center.y, rect.width);
        }
    }



    public static bool ButtonInvisible(UIContext ctx, Rect rect, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Button, label ?? "ButtonInvisible");
        if (ctx != null && ctx.Pass == UIPass.Measure) return false;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            return Widgets.ButtonInvisible(rect);
        }
    }

    public static void DrawAltRect(UIContext ctx, Rect rect, string label = null)
    {
        ctx?.Record(rect, UIRectTag.ListRow, label ?? "AltRect");
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            Widgets.DrawAltRect(rect);
        }
    }


    public static void DrawBox(UIContext ctx, Rect rect, int thickness = 1, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Panel, label ?? "Box");
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            Widgets.DrawBox(rect, thickness);
        }
    }

    public static void DrawTextureFitted(UIContext ctx, Rect rect, Texture2D tex, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Icon, label ?? "TextureFitted");
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        if (tex == null) return;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
        }
    }


    public static void DrawTextureFitted(UIContext ctx, Rect rect, Texture tex, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Icon, label ?? "TextureFitted");
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        if (tex == null) return;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit, true);
        }
    }

    public static void HighlightOnHover(UIContext ctx, Rect rect, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Button, label ?? "HoverHighlight");
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            Widgets.DrawHighlightIfMouseover(rect);
        }
    }

    public static void HighlightSelected(UIContext ctx, Rect rect, string label = null)
    {
        ctx?.Record(rect, UIRectTag.ListRowSelected, label ?? "SelectedHighlight");
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            Widgets.DrawHighlightSelected(rect);
        }
    }

    public static bool ButtonImage(UIContext ctx, Rect rect, Texture2D tex, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Button, label ?? "ButtonImage");
        ctx?.Record(rect, UIRectTag.Icon, (label ?? "ButtonImage") + "/Icon");
        if (ctx != null && ctx.Pass == UIPass.Measure) return false;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            return Widgets.ButtonImage(rect, tex);
        }
    }

    public static bool ButtonImageFitted(UIContext ctx, Rect rect, Texture2D tex, Color? color = null, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Button, label ?? "ButtonImageFitted");
        ctx?.Record(rect, UIRectTag.Icon, (label ?? "ButtonImageFitted") + "/Icon");
        if (ctx != null && ctx.Pass == UIPass.Measure) return false;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        {
            if (color.HasValue) return Widgets.ButtonImageFitted(rect, tex, color.Value);
            return Widgets.ButtonImageFitted(rect, tex);
        }
    }

    public static void TooltipHotspot(UIContext ctx, Rect rect, string label = null)
    {
        ctx?.Record(rect, UIRectTag.TooltipHotspot, label ?? "TooltipHotspot");
        if (ctx != null && ctx.Pass == UIPass.Measure) return;
        // Callers still set actual tooltip content via TooltipHandler.TipRegion.
    }

    // ---------------------------------------------------------------------
    // Skeleton forwarders (new helpers live in dedicated files)
    // ---------------------------------------------------------------------

    public static bool SelectorButton(UIContext ctx, Rect rect, string label, bool selected, bool disabled = false, string disabledReason = null, string tooltip = null, string id = null)
    {
        return D2Selectors.SelectorButton(ctx, rect, label, selected, disabled, disabledReason, tooltip, id);
    }

    public static void LabelWrapped(UIContext ctx, Rect rect, string text, GameFont font = GameFont.Small, string labelForOverlay = null)
    {
        // Delegate to D2Text so we have a single source of truth for text scoping and measurement.
        D2Text.DrawWrappedLabel(ctx, rect, text, font, UIRectTag.Text_Wrapped, labelForOverlay);
    }
}
