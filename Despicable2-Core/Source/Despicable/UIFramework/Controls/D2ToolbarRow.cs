using UnityEngine;
using Verse;
using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Framework-native toolbar row with a flexible primary label/content slot and an auto-sized trailing meta label.
///
/// This removes the need for callers to hand-measure text widths in ad hoc rows.
/// Layout ownership stays in the framework: callers provide content strings, the toolbar budgets the row.
/// </summary>
public static class D2ToolbarRow
{
    public struct Spec
    {
        public string Id;
        public string PrimaryText;
        public string SecondaryText;
        public string PrimaryTooltip;
        public string SecondaryTooltip;
        public bool DrawBackground;
        public bool Soft;
        public bool Pad;
        public float? PadOverride;

        public Spec(string id, string primaryText, string secondaryText = null, string primaryTooltip = null, string secondaryTooltip = null, bool drawBackground = false, bool soft = true, bool pad = false, float? padOverride = null)
        {
            Id = id;
            PrimaryText = primaryText;
            SecondaryText = secondaryText;
            PrimaryTooltip = primaryTooltip;
            SecondaryTooltip = secondaryTooltip;
            DrawBackground = drawBackground;
            Soft = soft;
            Pad = pad;
            PadOverride = padOverride;
        }
    }

    public static void Draw(UIContext ctx, Rect rect, Spec spec)
    {
        using (var panel = ctx.GroupPanel(
            spec.Id,
            rect,
            soft: spec.Soft,
            pad: spec.Pad,
            padOverride: spec.PadOverride,
            drawBackground: spec.DrawBackground,
            label: spec.Id))
        {
            Rect inner = panel.Inner;
            ctx?.RecordRect(inner, UIRectTag.Input, (spec.Id ?? "ToolbarRow") + "/Inner", null);

            var row = new D2HRow(ctx, inner);
            float secondaryWidth = MeasureTextWidth(ctx, spec.SecondaryText, inner.width);
            Rect primaryRect;
            Rect secondaryRect = Rect.zero;

            if (secondaryWidth > 0f)
            {
                float primaryWidth = Mathf.Max(0f, inner.width - secondaryWidth - (ctx?.Style?.Gap ?? 6f));
                primaryRect = row.NextFixed(primaryWidth, UIRectTag.Label, (spec.Id ?? "ToolbarRow") + "/Primary");
                secondaryRect = row.Remaining(UIRectTag.Label, (spec.Id ?? "ToolbarRow") + "/Secondary");
            }
            else
            {
                primaryRect = row.Remaining(UIRectTag.Label, (spec.Id ?? "ToolbarRow") + "/Primary");
            }

            if (!string.IsNullOrEmpty(spec.PrimaryText))
                D2Widgets.LabelClipped(ctx, primaryRect, spec.PrimaryText, (spec.Id ?? "ToolbarRow") + "/PrimaryLabel", spec.PrimaryTooltip); // loc-allow-internal: fallback toolbar row id

            if (!string.IsNullOrEmpty(spec.SecondaryText))
                DrawRightAlignedLabel(ctx, secondaryRect, spec.SecondaryText, (spec.Id ?? "ToolbarRow") + "/SecondaryLabel", spec.SecondaryTooltip);
        }
    }

    private static float MeasureTextWidth(UIContext ctx, string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
            return 0f;

        float pad = (ctx?.Style?.Pad ?? 10f) * 2f;
        float width;
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, false))
            width = Text.CalcSize(text).x + pad;

        return Mathf.Min(Mathf.Max(0f, maxWidth), width);
    }

    private static void DrawRightAlignedLabel(UIContext ctx, Rect rect, string text, string label, string tooltip)
    {
        ctx?.Record(rect, UIRectTag.Label, label);
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return;

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.MiddleRight, true))
        {
            string truncated = text.Truncate(rect.width);
            Widgets.Label(rect, truncated);
            string tip = tooltip ?? (truncated != text ? text : null);
            if (!string.IsNullOrEmpty(tip))
                TooltipHandler.TipRegion(rect, tip);
        }
    }
}
