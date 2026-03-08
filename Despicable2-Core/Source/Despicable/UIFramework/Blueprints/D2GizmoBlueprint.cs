using System;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Blueprints;
/// <summary>
/// Helper for drawing gizmo/command-like tiles consistently.
///
/// This is NOT a full replacement for Verse.Command / RimWorld gizmo system.
/// It's a drawing helper for custom panels / bespoke command grids.
///
/// Pattern:
/// - Allocate a grid cell rect.
/// - Call DrawGizmo per cell.
/// - Use NextFill for the grid panel if it should absorb leftover space.
/// </summary>
public static class D2GizmoBlueprint
{
    public readonly struct GizmoSpec
    {
        public readonly Texture2D Icon;
        public readonly string Label;
        public readonly string Tooltip;
        public readonly bool Disabled;
        public readonly string DisabledReason;
        public readonly bool Selected;
        public readonly string HotkeyLabel;
        public readonly Action Action;

        public GizmoSpec(
            Texture2D icon,
            string label,
            Action action,
            string tooltip = null,
            bool disabled = false,
            string disabledReason = null,
            bool selected = false,
            string hotkeyLabel = null)
        {
            Icon = icon;
            Label = label ?? string.Empty;
            Action = action;
            Tooltip = tooltip;
            Disabled = disabled;
            DisabledReason = disabledReason;
            Selected = selected;
            HotkeyLabel = hotkeyLabel;
        }
    }

    /// <summary>
    /// Draw a single gizmo tile and invoke its action if clicked.
    /// Returns true if clicked.
    /// </summary>
    public static bool DrawGizmo(UIContext ctx, Rect rect, GizmoSpec spec)
    {
        // Record the surface even in Measure pass so overlay can reason about grids.
        ctx?.RecordRect(rect, UIRectTag.Blueprint_Gizmo, spec.Label, Meta(spec));

        if (ctx == null || ctx.Pass != UIPass.Draw)
            return false;

        // Tooltips: prefer disabled reason.
        if (spec.Disabled && !spec.DisabledReason.NullOrEmpty())
            TooltipHandler.TipRegion(rect, spec.DisabledReason);
        else if (!spec.Tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, spec.Tooltip);

        bool clicked = false;

        using (new GUIEnabledScope(!spec.Disabled))
        using (new GUIColorScope(Color.white))
        {
            // Background
            Widgets.DrawMenuSection(rect);

            // Hover highlight
            if (Mouse.IsOver(rect))
                Widgets.DrawHighlight(rect);

            // Selected overlay
            if (spec.Selected)
                Widgets.DrawHighlightSelected(rect);

            // Layout
            float pad = 6f;
            float iconSize = Mathf.Min(36f, rect.width - pad * 2f);
            var iconRect = new Rect(rect.x + pad, rect.y + pad, iconSize, iconSize);

            float hotkeyH = spec.HotkeyLabel.NullOrEmpty() ? 0f : 16f;
            float labelH = 18f;

            var labelRect = new Rect(rect.x + pad, rect.yMax - pad - hotkeyH - labelH, rect.width - pad * 2f, labelH);
            var hotkeyRect = new Rect(rect.x + pad, rect.yMax - pad - hotkeyH, rect.width - pad * 2f, hotkeyH);

            ctx?.RecordRect(iconRect, UIRectTag.Icon, spec.Label + "/Icon");
            ctx?.RecordRect(labelRect, UIRectTag.Label, spec.Label + "/Label");
            if (!spec.HotkeyLabel.NullOrEmpty())
                ctx?.RecordRect(hotkeyRect, UIRectTag.Label, spec.Label + "/Hotkey");

            // Draw icon
            if (spec.Icon != null)
                GUI.DrawTexture(iconRect, spec.Icon);

            // Draw label (clipped)
            using (new TextStateScope(GameFont.Small, TextAnchor.MiddleCenter, false))
            {
                Widgets.Label(labelRect, spec.Label);
            }

            // Hotkey hint (small, faint)
            if (!spec.HotkeyLabel.NullOrEmpty())
            {
                using (new TextStateScope(GameFont.Tiny, TextAnchor.MiddleCenter, false))
                using (new GUIColorScope(new Color(1f, 1f, 1f, 0.6f)))
                {
                    Widgets.Label(hotkeyRect, spec.HotkeyLabel);
                }
            }

            // Click
            if (Widgets.ButtonInvisible(rect))
            {
                clicked = true;
                if (!spec.Disabled)
                    spec.Action?.Invoke();
            }
        }

        return clicked;
    }

    private static string Meta(GizmoSpec spec)
    {
        // Compact meta string for overlay.
        // Example: Sel=1 Dis=0
        return "Sel=" + (spec.Selected ? "1" : "0") + " Dis=" + (spec.Disabled ? "1" : "0");
    }
}
