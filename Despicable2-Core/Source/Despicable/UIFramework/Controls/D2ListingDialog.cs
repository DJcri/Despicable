using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Searchable listing surface with optional filter sidebar.
///
/// This is a control primitive, not a Window. Host it inside any D2 window/dialog shell.
/// </summary>
public static class D2ListingDialog
{
    public sealed class Entry<T>
    {
        public T Value;
        public string Label;
        public string SearchText;
        public string Tooltip;
        public Texture2D Icon;
        public bool Disabled;
        public string DisabledReason;

        public Entry(T value, string label)
        {
            Value = value;
            Label = label ?? string.Empty;
        }
    }

    public struct State
    {
        public string Search;
        public Vector2 Scroll;
        public Vector2 FilterScroll;
        public int SelectedIndex;
        public bool ShowFilters;
    }

    public readonly struct Result<T>
    {
        public readonly bool Changed;
        public readonly int SelectedIndex;
        public readonly Entry<T> SelectedEntry;
        public readonly List<Entry<T>> VisibleEntries;

        public Result(bool changed, int selectedIndex, Entry<T> selectedEntry, List<Entry<T>> visibleEntries)
        {
            Changed = changed;
            SelectedIndex = selectedIndex;
            SelectedEntry = selectedEntry;
            VisibleEntries = visibleEntries ?? new List<Entry<T>>();
        }
    }

    public static Result<T> Draw<T>(
        UIContext ctx,
        Rect rect,
        IList<Entry<T>> entries,
        ref State state,
        IList<D2Filters.IFilter<Entry<T>>> filters = null,
        Action<UIContext, Rect, Entry<T>, int, bool> rowDrawer = null,
        string title = null,
        string label = "ListingDialog")
    {
        if (state.SelectedIndex < 0)
            state.SelectedIndex = 0;

        float row = ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f;
        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
        float filterToggleW = 96f;
        bool hasFilters = filters != null && filters.Count > 0;

        Rect header = new(rect.x, rect.y, rect.width, row);
        Rect body = new(rect.x, header.yMax + gap, rect.width, Mathf.Max(0f, rect.height - row - gap));

        Rect searchRect = header;
        if (hasFilters)
            searchRect.width = Mathf.Max(0f, header.width - filterToggleW - gap);
        D2Fields.SearchBox(ctx, searchRect, ref state.Search, placeholder: title ?? "D2C_UI_Search".Translate().ToString(), label: label + "/SearchBox");
        if (hasFilters)
        {
            Rect toggleRect = new(searchRect.xMax + gap, header.y, Mathf.Min(filterToggleW, Mathf.Max(0f, header.xMax - (searchRect.xMax + gap))), header.height);
            ctx?.RecordRect(toggleRect, UIRectTag.Button, label + "/FiltersToggle", null);
            string toggleLabel = state.ShowFilters ? "D2C_UI_HideFilters".Translate().ToString() : "D2C_UI_ShowFilters".Translate().ToString();
            if (D2Widgets.ButtonText(ctx, toggleRect, toggleLabel, label + "/ToggleFilters"))
                state.ShowFilters = !state.ShowFilters;
        }

        D2PaneLayout.LayoutResult layout;
        if (hasFilters && state.ShowFilters)
        {
            layout = D2PaneLayout.Columns(
                ctx,
                body,
                new[]
                {
                    new D2PaneLayout.PaneSpec("Filters", 160f, 180f, 0f, canCollapse: true, priority: 1),
                    new D2PaneLayout.PaneSpec("List", 200f, 480f, 1f, canCollapse: false, priority: 0)
                },
                gap: gap,
                fallback: D2PaneLayout.FallbackMode.Stack,
                label: label + "/Layout");
        }
        else
        {
            layout = new D2PaneLayout.LayoutResult(new[] { Rect.zero, body }, false, D2PaneLayout.FallbackMode.None, new[] { 1 });
            ctx?.RecordRect(body, UIRectTag.Body, label + "/ListOnly", null);
        }

        Rect filterRect = layout.Rects != null && layout.Rects.Length > 0 ? layout.Rects[0] : Rect.zero;
        Rect listRect = layout.Rects != null && layout.Rects.Length > 1 ? layout.Rects[1] : body;

        if (hasFilters && state.ShowFilters && filterRect.width > 0f && filterRect.height > 0f)
            DrawFilters(ctx, filterRect, filters, ref state.FilterScroll, label + "/Filters");

        List<Entry<T>> visible = BuildVisible(entries, state.Search, filters);
        if (state.SelectedIndex >= visible.Count)
            state.SelectedIndex = visible.Count - 1;
        if (visible.Count == 0)
            state.SelectedIndex = -1;

        int selected = state.SelectedIndex;
        D2ListView.Draw(
            ctx,
            listRect,
            ref state.Scroll,
            visible,
            ref selected,
            (drawCtx, rowRect, item, index, isSelected) =>
            {
                if (rowDrawer != null)
                {
                    rowDrawer(drawCtx, rowRect, item, index, isSelected);
                    return;
                }

                DrawDefaultRow(drawCtx, rowRect, item, index, isSelected, label + "/Rows");
            },
            rowHeightOverride: row,
            zebra: true,
            label: label + "/List");

        bool changed = selected != state.SelectedIndex;
        state.SelectedIndex = selected;
        Entry<T> selectedEntry = state.SelectedIndex >= 0 && state.SelectedIndex < visible.Count ? visible[state.SelectedIndex] : null;
        return new Result<T>(changed, state.SelectedIndex, selectedEntry, visible);
    }

    private static void DrawFilters<T>(UIContext ctx, Rect rect, IList<D2Filters.IFilter<Entry<T>>> filters, ref Vector2 scroll, string label)
    {
        if (filters == null || filters.Count == 0)
            return;

        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
        using (var g = ctx.GroupPanel(label, rect, soft: true, pad: true, drawBackground: true, label: label))
        {
            float total = 0f;
            for (int i = 0; i < filters.Count; i++)
            {
                if (i > 0)
                    total += gap;
                total += Mathf.Max(0f, filters[i].MeasureHeight(ctx));
            }

            Rect outRect = g.Inner;
            Rect view = new(0f, 0f, Mathf.Max(0f, outRect.width - 16f), Mathf.Max(outRect.height, total));
            Widgets.BeginScrollView(outRect, ref scroll, view);
            try
            {
                using (ctx.PushOffset(outRect.position - scroll))
                {
                    float y = 0f;
                    for (int i = 0; i < filters.Count; i++)
                    {
                        float h = Mathf.Max(0f, filters[i].MeasureHeight(ctx));
                        Rect row = new(0f, y, view.width, h);
                        filters[i].Draw(ctx, row, label + "/Filter[" + i + "]");
                        y += h + gap;
                    }
                }
            }
            finally
            {
                Widgets.EndScrollView();
            }
        }
    }

    private static List<Entry<T>> BuildVisible<T>(IList<Entry<T>> entries, string search, IList<D2Filters.IFilter<Entry<T>>> filters)
    {
        var visible = new List<Entry<T>>();
        if (entries == null)
            return visible;

        string needle = (search ?? string.Empty).Trim().ToLowerInvariant();
        for (int i = 0; i < entries.Count; i++)
        {
            Entry<T> entry = entries[i];
            if (entry == null)
                continue;

            if (needle.Length > 0)
            {
                string hay = !string.IsNullOrEmpty(entry.SearchText) ? entry.SearchText : entry.Label;
                hay = (hay ?? string.Empty).ToLowerInvariant();
                if (!hay.Contains(needle))
                    continue;
            }

            bool passed = true;
            if (filters != null)
            {
                for (int f = 0; f < filters.Count; f++)
                {
                    if (!filters[f].Matches(entry))
                    {
                        passed = false;
                        break;
                    }
                }
            }

            if (passed)
                visible.Add(entry);
        }

        return visible;
    }

    private static void DrawDefaultRow<T>(UIContext ctx, Rect rowRect, Entry<T> item, int index, bool selected, string label)
    {
        ctx?.RecordRect(rowRect, UIRectTag.Control_MenuRow, label + "/Row[" + index + "]", null);

        if (ctx != null && ctx.Pass == UIPass.Draw)
        {
            if (selected)
                Widgets.DrawHighlightSelected(rowRect);
            if (!item.Tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(rowRect, item.Tooltip);
            else if (item.Disabled && !item.DisabledReason.NullOrEmpty())
                TooltipHandler.TipRegion(rowRect, item.DisabledReason);
        }

        Rect inner = rowRect.ContractedBy(4f);
        float iconSize = Mathf.Min(20f, inner.height);
        Rect iconRect = new(inner.x, inner.y + Mathf.Max(0f, (inner.height - iconSize) * 0.5f), iconSize, iconSize);
        Rect textRect = new(iconRect.xMax + 6f, inner.y, Mathf.Max(0f, inner.xMax - (iconRect.xMax + 6f)), inner.height);

        if (item.Icon != null && ctx != null && ctx.Pass == UIPass.Draw)
            GUI.DrawTexture(iconRect, item.Icon, ScaleMode.ScaleToFit);

        using (new GUIEnabledScope(!item.Disabled))
        {
            D2Widgets.LabelClipped(ctx, textRect, item.Label ?? string.Empty, label + "/Text[" + index + "]", tooltipOverride: item.Tooltip); // loc-allow-internal: generated row text id
        }
    }
}
