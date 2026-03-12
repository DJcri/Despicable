using System.Collections.Generic;
using UnityEngine;
using Verse;
using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Simple tab strip helpers for "paged" layouts.
///
/// Not a full tab system: it draws selector-style or vanilla TabDrawer tabs and returns the selected index.
/// </summary>
public static class D2Tabs
{
    /// <summary>
    /// Vanilla attached tab shell layout result.
    /// The shell rect is the caller-owned region that contains both the tab extension and the panel below it.
    /// </summary>
    public readonly struct VanillaAttachedLayout
    {
        public readonly Rect ShellRect;
        public readonly Rect PanelRect;
        public readonly Rect InnerRect;
        public readonly Rect TabsRect;
        public readonly int RowCount;
        public readonly float TabExtension;

        public VanillaAttachedLayout(Rect shellRect, Rect panelRect, Rect innerRect, Rect tabsRect, int rowCount, float tabExtension)
        {
            ShellRect = shellRect;
            PanelRect = panelRect;
            InnerRect = innerRect;
            TabsRect = tabsRect;
            RowCount = rowCount;
            TabExtension = tabExtension;
        }
    }

    public const float VanillaTabHeight = 32f;
    public const float VanillaTabRowStride = 31f;
    public const float VanillaTabMaxWidth = 200f;
    public const float VanillaAttachedInnerPad = 18f;
    public const float VanillaTabMinOverflowWidth = 120f;

    private static string[] TranslateKeys(string[] keys)
    {
        if (keys == null) return null;
        var labels = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++)
            labels[i] = string.IsNullOrEmpty(keys[i]) ? string.Empty : keys[i].Translate().ToString();
        return labels;
    }

    /// <summary>
    /// Opt-in vanilla tab strip wrapper.
    /// Existing selector-style tabs stay unchanged unless callers explicitly use this helper.
    /// </summary>
    public static int VanillaTabStrip(UIContext ctx, Rect rect, int selected, string[] labels, string label)
    {
        if (labels == null || labels.Length == 0)
            return selected;

        ctx?.Record(rect, UIRectTag.Tab, (label ?? "Tabs") + "/VanillaTabStrip");

        int newSel = Mathf.Clamp(selected, 0, labels.Length - 1);
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return newSel;

        var tabs = BuildVanillaTabs(labels, newSel, out var selectionRef);
        TabDrawer.DrawTabs(rect, tabs, VanillaTabMaxWidth);
        return selectionRef.Value;
    }

    public static int VanillaTabStripKeys(UIContext ctx, Rect rect, int selected, string[] keys, string label)
    {
        return VanillaTabStrip(ctx, rect, selected, TranslateKeys(keys), label);
    }

    /// <summary>
    /// Draws a vanilla tab strip that is visually attached to the panel it controls,
    /// following the same panel-owned geometry used by vanilla folder tabs.
    ///
    /// The provided <paramref name="rect"/> is the full shell region for both tabs and panel body.
    /// The returned layout exposes the panel rect and the padded inner content rect.
    /// </summary>
    public static VanillaAttachedLayout VanillaAttachedTabBody(
        UIContext ctx,
        Rect rect,
        ref int selected,
        string[] labels,
        string label,
        float innerPad = VanillaAttachedInnerPad,
        float minOverflowTabWidth = VanillaTabMinOverflowWidth,
        float maxTabWidth = VanillaTabMaxWidth,
        int forcedRows = 0)
    {
        int labelCount = labels?.Length ?? 0;
        int clamped = labelCount > 0 ? Mathf.Clamp(selected, 0, labelCount - 1) : 0;
        int rowCount = ResolveVanillaRowCount(rect.width, labelCount, minOverflowTabWidth, forcedRows);
        float tabExtension = rowCount * VanillaTabRowStride;

        Rect panelRect = rect;
        panelRect.yMin = Mathf.Min(panelRect.yMax, panelRect.yMin + tabExtension);

        float safeInnerPad = Mathf.Max(0f, innerPad);
        Rect innerRect = safeInnerPad > 0f ? panelRect.ContractedBy(safeInnerPad) : panelRect;
        Rect tabsRect = rowCount > 0
            ? new Rect(panelRect.x, panelRect.y - tabExtension, panelRect.width, tabExtension + 1f)
            : new Rect(panelRect.x, panelRect.y, panelRect.width, 0f);

        string prefix = string.IsNullOrEmpty(label) ? "Tabs" : label;
        if (rowCount > 0)
            ctx?.Record(tabsRect, UIRectTag.Tab, prefix + "/AttachedTabs");
        ctx?.Record(panelRect, UIRectTag.Panel, prefix + "/AttachedPanel");

        if (ctx == null || ctx.Pass != UIPass.Measure)
        {
            Widgets.DrawMenuSection(panelRect);

            if (labelCount > 0)
            {
                var tabs = BuildVanillaTabs(labels, clamped, out var selectionRef);
                if (rowCount > 1)
                    TabDrawer.DrawTabs(panelRect, tabs, rowCount, maxTabWidth);
                else
                    TabDrawer.DrawTabs(panelRect, tabs, maxTabWidth);

                clamped = selectionRef.Value;
            }
        }

        selected = clamped;
        return new VanillaAttachedLayout(rect, panelRect, innerRect, tabsRect, rowCount, tabExtension);
    }

    public static VanillaAttachedLayout VanillaAttachedTabBodyKeys(UIContext ctx, Rect rect, ref int selected, string[] keys, string label, float innerPad = VanillaAttachedInnerPad, float minOverflowTabWidth = VanillaTabMinOverflowWidth, float maxTabWidth = VanillaTabMaxWidth, int forcedRows = 0)
    {
        return VanillaAttachedTabBody(ctx, rect, ref selected, TranslateKeys(keys), label, innerPad, minOverflowTabWidth, maxTabWidth, forcedRows);
    }

    /// <summary>
    /// Draw a tab strip across <paramref name="rect"/>.
    /// </summary>
    public static int TabStrip(UIContext ctx, Rect rect, int selected, string[] labels, string label)
    {
        if (labels == null || labels.Length == 0)
            return selected;

        // Record the whole strip so overlay can show where the paging decision lives.
        ctx.Record(rect, UIRectTag.Input, label + "/TabStrip");

        var h = new D2HRow(ctx, rect);
        float w = rect.width / labels.Length;

        int newSel = Mathf.Clamp(selected, 0, labels.Length - 1);

        for (int i = 0; i < labels.Length; i++)
        {
            bool isLast = (i == labels.Length - 1);
            Rect r = isLast ? h.Remaining() : h.NextFixed(w);

            bool isSelected = (newSel == i);
            var spec = new D2Selectors.SelectorSpec(
                id: label + "/Tab[" + i + "]",
                label: labels[i],
                tooltip: null,
                selected: isSelected,
                disabled: false,
                disabledReason: null);

            if (D2Selectors.SelectorButton(ctx, r, spec))
                newSel = i;
        }

        return newSel;
    }

    public static int TabStripKeys(UIContext ctx, Rect rect, int selected, string[] keys, string label)
    {
        return TabStrip(ctx, rect, selected, TranslateKeys(keys), label);
    }

    public static int ResolveVanillaRowCount(float width, int tabCount, float minOverflowTabWidth = VanillaTabMinOverflowWidth, int forcedRows = 0)
    {
        if (forcedRows > 0)
            return Mathf.Max(1, forcedRows);

        if (tabCount <= 0)
            return 0;

        float safeWidth = Mathf.Max(1f, width);
        float safeMinWidth = Mathf.Max(1f, minOverflowTabWidth);
        return Mathf.Max(1, Mathf.CeilToInt((tabCount * safeMinWidth) / safeWidth));
    }

    public static float GetVanillaAttachedTabExtension(int rowCount)
    {
        return Mathf.Max(0, rowCount) * VanillaTabRowStride;
    }

    private static List<TabRecord> BuildVanillaTabs(string[] labels, int selected, out SelectionState selectionRef)
    {
        var state = new SelectionState(selected);
        selectionRef = state;
        int count = labels?.Length ?? 0;
        var tabs = new List<TabRecord>(count);
        for (int i = 0; i < count; i++)
        {
            int local = i;
            string tabLabel = labels[i] ?? string.Empty;
            tabs.Add(new TabRecord(tabLabel, () => state.Value = local, state.Value == local));
        }

        return tabs;
    }

    private sealed class SelectionState
    {
        public int Value;

        public SelectionState(int value)
        {
            Value = value;
        }
    }
}
