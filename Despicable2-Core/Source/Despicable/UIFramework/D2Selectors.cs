using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework
{
    /// <summary>
    /// Consistent selector/segmented button helpers (selected = green highlight rule).
    ///
    /// WHY THIS EXISTS
    /// RimWorld UIs often end up with 5 different "toggle buttons" that all look slightly different.
    /// This centralizes:
    /// - selected/unselected styling
    /// - disabled behavior + disabled tooltip
    /// - rect tagging for overlays/validators
    ///
    /// NOTE: We keep this separate from D2Widgets so D2Widgets can remain a thin wrapper layer.
    /// </summary>
    public static class D2Selectors
    {
        public readonly struct SelectorSpec
        {
            public readonly string Id;
            public readonly string Label;
            public readonly string Tooltip;
            public readonly bool Selected;
            public readonly bool Disabled;
            public readonly string DisabledReason;

            public SelectorSpec(
                string id,
                string label,
                bool selected,
                bool disabled = false,
                string disabledReason = null,
                string tooltip = null)
            {
                Id = id;
                Label = label;
                Selected = selected;
                Disabled = disabled;
                DisabledReason = disabledReason;
                Tooltip = tooltip;
            }
        }

        /// <summary>
        /// The canonical selector button.
        ///
        /// DRAW RULES (stable UX contract):
        /// - If Selected: draw selected highlight (Widgets.DrawHighlightSelected)
        /// - If Disabled: button is non-interactive and shows DisabledReason (if provided)
        /// - Tooltip: always preferred from Tooltip; fallback to DisabledReason if Tooltip absent
        /// - Height: caller should allocate at least ctx.Style.RowHeight; if smaller, we still draw but warn later
        ///
        /// PSEUDOCODE:
        /// ctx.RecordRect(rect, Control_Selector, spec.Label, meta: Selected/Disabled)
        /// if ctx.Measure: return false
        /// tip = spec.Tooltip ?? (spec.Disabled ? spec.DisabledReason : null)
        /// if tip: TooltipHandler.TipRegion(rect, tip)
        /// if spec.Selected: Widgets.DrawHighlightSelected(rect)
        /// using EnabledScope(!spec.Disabled): clicked = Widgets.ButtonText(rect, spec.Label, ...)
        /// return clicked && !spec.Disabled
        /// </summary>
        public static bool SelectorButton(UIContext ctx, Rect rect, SelectorSpec spec)
        {
            // Record for overlays (Draw pass only), but updating ContentMaxY is harmless in both passes.
            string meta = "Sel=" + (spec.Selected ? "1" : "0") + ",Dis=" + (spec.Disabled ? "1" : "0");
            ctx?.RecordRect(rect, UIRectTag.Control_Selector, spec.Id ?? spec.Label, meta);

            if (ctx == null || ctx.Pass != UIPass.Draw)
                return false;

            // Tooltip priority: explicit tooltip, then disabled reason (if disabled).
            string tip = !spec.Tooltip.NullOrEmpty()
                ? spec.Tooltip
                : (spec.Disabled ? spec.DisabledReason : null);

            if (!tip.NullOrEmpty())
                TooltipHandler.TipRegion(rect, tip);

            bool clicked;
            Color selectedTint = new(0.72f, 1f, 0.72f, 1f);

            if (spec.Disabled)
            {
                // RimWorld's Unity version does not provide UnityEngine.GUI.EnabledScope.
                // Use our framework scopes instead.
                if (spec.Selected)
                {
                    using (new GUIColorScope(selectedTint))
                    using (new GUIEnabledScope(false))
                    {
                        clicked = Widgets.ButtonText(rect, spec.Label ?? string.Empty, drawBackground: true, doMouseoverSound: true, active: true);
                    }
                }
                else
                {
                    using (new GUIEnabledScope(false))
                    {
                        clicked = Widgets.ButtonText(rect, spec.Label ?? string.Empty, drawBackground: true, doMouseoverSound: true, active: true);
                    }
                }
            }
            else if (spec.Selected)
            {
                using (new GUIColorScope(selectedTint))
                {
                    clicked = Widgets.ButtonText(rect, spec.Label ?? string.Empty, drawBackground: true, doMouseoverSound: true, active: true);
                }
            }
            else
            {
                clicked = Widgets.ButtonText(rect, spec.Label ?? string.Empty, drawBackground: true, doMouseoverSound: true, active: true);
            }

            if (spec.Selected)
                Widgets.DrawHighlightSelected(rect);

            return clicked && !spec.Disabled;
        }

        
/// <summary>
/// Small utility for single-select groups.
/// This is intentionally minimal: it does not own layout, only selection logic.
/// Pair it with D2VStack/D2HRow to allocate rects.
/// </summary>
public readonly struct SelectorGroup<T>
{
    public readonly string GroupId;
    public readonly IList<T> Options;
    public readonly System.Func<T, string> LabelOf;
    public readonly System.Func<T, string> TooltipOf;

    public SelectorGroup(string groupId, IList<T> options, System.Func<T, string> labelOf, System.Func<T, string> tooltipOf = null)
    {
        GroupId = groupId ?? "SelectorGroup";
        Options = options;
        LabelOf = labelOf ?? (x => x != null ? x.ToString() : string.Empty);
        TooltipOf = tooltipOf;
    }

    /// <summary>
    /// Draw options into a list of pre-allocated rects (one per option).
    /// Returns the new selected index.
    /// </summary>
    public int Draw(UIContext ctx, IList<Rect> optionRects, int selectedIndex, out bool changed, bool allowDeselect = false)
    {
        changed = false;
        if (ctx == null || optionRects == null || Options == null) return selectedIndex;

        int n = Mathf.Min(optionRects.Count, Options.Count);
        int newIndex = selectedIndex;

        for (int i = 0; i < n; i++)
        {
            bool isSel = (i == selectedIndex);
            T opt = Options[i];
            string label = LabelOf(opt);
            string tip = TooltipOf != null ? TooltipOf(opt) : null;

            bool clicked = SelectorButton(ctx, optionRects[i], label, isSel, disabled: false, disabledReason: null, tooltip: tip, id: GroupId + "/" + i);
            if (clicked)
            {
                if (allowDeselect && isSel) newIndex = -1;
                else newIndex = i;
            }
        }

        if (newIndex != selectedIndex)
        {
            changed = true;
            selectedIndex = newIndex;
        }

        return selectedIndex;
    }
}

/// <summary>
        /// Convenience overload when you don't want to build a SelectorSpec.
        /// </summary>
        public static bool SelectorButton(UIContext ctx, Rect rect, string label, bool selected, bool disabled = false, string disabledReason = null, string tooltip = null, string id = null)
        {
            return SelectorButton(ctx, rect, new SelectorSpec(id, label, selected, disabled, disabledReason, tooltip));
        }
    }
}
