using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Searchable catalog browser for appearance/content pickers.
///
/// This is intentionally lightweight:
/// - optional search row
/// - grid or list presentation
/// - item selection with a stable selected index
/// - caller-supplied item specs and draw behavior kept simple
///
/// It is designed to cover the "show lots of face parts / presets / assets" use case
/// without forcing each caller to rebuild the same browser shell.
/// </summary>
public static class D2CatalogBrowser<T>
{
    public enum ViewMode
    {
        Grid = 0,
        List = 1
    }

    public sealed class ItemSpec
    {
        public T Value;
        public string Id;
        public string Label;
        public string SearchText;
        public string Tooltip;
        public Texture2D Icon;
        public bool Disabled;
        public string DisabledReason;

        public ItemSpec(T value, string id, string label)
        {
            Value = value;
            Id = id;
            Label = label;
        }
    }

    public struct State
    {
        public string Search;
        public Vector2 Scroll;
        public int SelectedIndex;
    }

    public struct Result
    {
        public bool Changed;
        public int SelectedIndex;
        public T SelectedValue;
    }

    public static Result Draw(UIContext ctx, Rect rect, IList<ItemSpec> items, ref State state, ViewMode mode = ViewMode.Grid, bool showSearch = true, string searchPlaceholder = "Search...", string label = null)
    {
        ctx?.RecordRect(rect, UIRectTag.PanelSoft, label ?? "CatalogBrowser");

        if (items == null)
            items = new ItemSpec[0];

        List<ItemSpec> filtered = Filter(items, state.Search);
        ClampSelection(filtered.Count, ref state.SelectedIndex);

        var v = new D2VStack(ctx, rect);
        if (showSearch)
        {
            Rect searchRect = v.Next(ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f, UIRectTag.Input, (label ?? "CatalogBrowser") + "/Search");
            string localSearch = state.Search ?? string.Empty;
            D2Fields.SearchBox(ctx, searchRect, ref localSearch, searchPlaceholder, label: (label ?? "CatalogBrowser") + "/SearchBox");
            state.Search = localSearch;
            if (v.RemainingHeight > 0f)
                v.NextSpace(ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f);
            filtered = Filter(items, state.Search);
            ClampSelection(filtered.Count, ref state.SelectedIndex);
        }

        Rect body = v.Remaining();
        if (body.height <= 0f)
            return new Result { Changed = false, SelectedIndex = state.SelectedIndex, SelectedValue = default(T) };

        if (mode == ViewMode.List)
            return DrawList(ctx, body, filtered, ref state, label ?? "CatalogBrowser");

        return DrawGrid(ctx, body, filtered, ref state, label ?? "CatalogBrowser");
    }

    private static Result DrawList(UIContext ctx, Rect rect, IList<ItemSpec> items, ref State state, string label)
    {
        int selected = state.SelectedIndex;
        D2ListView.Draw(ctx, rect, ref state.Scroll, items, ref selected,
            (c, row, item, index, isSelected) => DrawListRow(c, row, item, index, isSelected, label),
            rowHeightOverride: ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f,
            zebra: true,
            label: label + "/List");

        bool changed = selected != state.SelectedIndex;
        state.SelectedIndex = selected;
        T value = selected >= 0 && selected < items.Count ? items[selected].Value : default(T);
        return new Result { Changed = changed, SelectedIndex = selected, SelectedValue = value };
    }

    private static Result DrawGrid(UIContext ctx, Rect rect, IList<ItemSpec> items, ref State state, string label)
    {
        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
        float minTile = 72f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((rect.width + gap) / (minTile + gap)));
        float tileW = Mathf.Max(minTile, (rect.width - ((columns - 1) * gap)) / columns);
        float tileH = tileW;
        int rows = Mathf.CeilToInt(items.Count / (float)columns);
        float contentH = rows > 0 ? ((rows * tileH) + ((rows - 1) * gap)) : 0f;
        Rect view = new(0f, 0f, Mathf.Max(0f, rect.width - 16f), contentH);

        Widgets.BeginScrollView(rect, ref state.Scroll, view);
        try
        {
            int clicked = -1;
            for (int i = 0; i < items.Count; i++)
            {
                int row = i / columns;
                int col = i % columns;
                Rect tile = new((tileW + gap) * col, (tileH + gap) * row, tileW, tileH);
                if (DrawGridTile(ctx, tile, items[i], i, state.SelectedIndex == i, label))
                    clicked = i;
            }

            if (clicked >= 0)
            {
                state.SelectedIndex = clicked;
                return new Result { Changed = true, SelectedIndex = clicked, SelectedValue = items[clicked].Value };
            }
        }
        finally
        {
            Widgets.EndScrollView();
        }

        T value = state.SelectedIndex >= 0 && state.SelectedIndex < items.Count ? items[state.SelectedIndex].Value : default(T);
        return new Result { Changed = false, SelectedIndex = state.SelectedIndex, SelectedValue = value };
    }

    private static void DrawListRow(UIContext ctx, Rect row, ItemSpec item, int index, bool selected, string label)
    {
        ctx?.RecordRect(row, UIRectTag.Control_MenuRow, label + "/Row[" + index + "]");
        if (ctx != null && ctx.Pass == UIPass.Draw && selected)
            Widgets.DrawHighlightSelected(row);

        if (!string.IsNullOrEmpty(item.Tooltip) && ctx != null && ctx.Pass == UIPass.Draw)
            TooltipHandler.TipRegion(row, item.Tooltip);
        else if (item.Disabled && !string.IsNullOrEmpty(item.DisabledReason) && ctx != null && ctx.Pass == UIPass.Draw)
            TooltipHandler.TipRegion(row, item.DisabledReason);

        Rect inner = row.ContractedBy(4f);
        Rect iconRect = new(inner.x, inner.y + Mathf.Max(0f, (inner.height - 20f) * 0.5f), 20f, 20f);
        Rect textRect = new(iconRect.xMax + 6f, inner.y, Mathf.Max(0f, inner.xMax - (iconRect.xMax + 6f)), inner.height);

        if (item.Icon != null && ctx != null && ctx.Pass == UIPass.Draw)
            GUI.DrawTexture(iconRect, item.Icon, ScaleMode.ScaleToFit);

        using (new GUIEnabledScope(!item.Disabled))
        {
            D2Widgets.LabelClipped(ctx, textRect, item.Label ?? string.Empty, label + "/RowLabel[" + index + "]", tooltipOverride: item.Tooltip); // loc-allow-internal: generated row label id
        }
    }

    private static bool DrawGridTile(UIContext ctx, Rect rect, ItemSpec item, int index, bool selected, string label)
    {
        ctx?.RecordRect(rect, UIRectTag.Button, label + "/Tile[" + index + "]");

        if (!string.IsNullOrEmpty(item.Tooltip) && ctx != null && ctx.Pass == UIPass.Draw)
            TooltipHandler.TipRegion(rect, item.Tooltip);
        else if (item.Disabled && !string.IsNullOrEmpty(item.DisabledReason) && ctx != null && ctx.Pass == UIPass.Draw)
            TooltipHandler.TipRegion(rect, item.DisabledReason);

        if (ctx != null && ctx.Pass == UIPass.Draw)
        {
            Widgets.DrawMenuSection(rect);
            if (selected)
                Widgets.DrawHighlightSelected(rect);
        }

        Rect inner = rect.ContractedBy(6f);
        float iconH = Mathf.Max(0f, inner.height - 24f - 6f);
        Rect iconRect = new(inner.x, inner.y, inner.width, iconH);
        Rect textRect = new(inner.x, inner.yMax - 24f, inner.width, 24f);

        if (item.Icon != null && ctx != null && ctx.Pass == UIPass.Draw)
            GUI.DrawTexture(iconRect, item.Icon, ScaleMode.ScaleToFit);

        using (new GUIEnabledScope(!item.Disabled))
        {
            D2Widgets.LabelClipped(ctx, textRect, item.Label ?? string.Empty, label + "/TileLabel[" + index + "]", tooltipOverride: item.Tooltip); // loc-allow-internal: generated tile label id
        }

        if (item.Disabled)
            return false;

        return ctx == null || ctx.Pass != UIPass.Draw ? false : Widgets.ButtonInvisible(rect);
    }

    private static List<ItemSpec> Filter(IList<ItemSpec> items, string search)
    {
        var list = new List<ItemSpec>();
        if (items == null) return list;

        string needle = (search ?? string.Empty).Trim();
        if (needle.Length == 0)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null)
                    list.Add(items[i]);
            return list;
        }

        needle = needle.ToLowerInvariant();
        for (int i = 0; i < items.Count; i++)
        {
            ItemSpec item = items[i];
            if (item == null) continue;

            string hay = !string.IsNullOrEmpty(item.SearchText) ? item.SearchText : item.Label;
            hay = (hay ?? string.Empty).ToLowerInvariant();
            if (hay.Contains(needle))
                list.Add(item);
        }

        return list;
    }

    private static void ClampSelection(int count, ref int selectedIndex)
    {
        if (count <= 0)
        {
            selectedIndex = -1;
            return;
        }

        if (selectedIndex < 0)
            selectedIndex = 0;
        else if (selectedIndex >= count)
            selectedIndex = count - 1;
    }
}
