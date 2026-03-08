using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Simple sortable table primitive.
///
/// The table keeps layout conservative:
/// - fixed header row
/// - proportional column weights
/// - single-column sorting
/// - caller-owned cell drawing when needed
///
/// Vanilla-ish upgrades are opt-in via <see cref="VisualOptions{T}"/> so existing
/// call sites keep their current look unless they explicitly switch features on.
/// </summary>
public static class D2Table
{
    public sealed class Column<T>
    {
        public string Id;
        public string Label;
        public float Weight = 1f;
        public Func<T, string> Text;
        public Func<T, IComparable> SortValue;
        public Action<UIContext, Rect, T> Draw;
        public string Tooltip;

        public Column(string id, string label, Func<T, string> text = null)
        {
            Id = id ?? string.Empty;
            Label = label ?? string.Empty;
            Text = text;
        }
    }

    /// <summary>
    /// Opt-in visual behaviors. All flags default to off so legacy tables remain unchanged.
    /// </summary>
    public sealed class VisualOptions<T>
    {
        public bool UseVanillaSortIcons;
        public bool HighlightRowOnHover;
        public Predicate<T> IsRowSelected;
    }

    public struct State
    {
        public Vector2 Scroll;
        public int SortColumn;
        public bool SortDescending;
    }

    public static void Draw<T>(UIContext ctx, Rect rect, IList<T> items, IList<Column<T>> columns, ref State state, float? rowHeightOverride = null, string label = "Table", VisualOptions<T> visualOptions = null)
    {
        if (columns == null || columns.Count == 0)
            return;

        float row = rowHeightOverride ?? (ctx != null && ctx.Style != null ? ctx.Style.RowH : 28f);
        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;

        Rect header = new(rect.x, rect.y, rect.width, row);
        Rect body = new(rect.x, header.yMax + gap, rect.width, Mathf.Max(0f, rect.height - row - gap));
        DrawHeader(ctx, header, columns, ref state, label + "/Header", visualOptions);

        var sorted = BuildSorted(items, columns, state);
        float contentH = Mathf.Max(body.height, sorted.Count * row);
        Rect view = new(0f, 0f, Mathf.Max(0f, body.width - 16f), contentH);

        if (ctx != null && ctx.Pass == UIPass.Measure)
        {
            using (ctx.PushOffset(body.position))
            {
                for (int i = 0; i < sorted.Count; i++)
                {
                    Rect rowRect = new(0f, i * row, view.width, row);
                    DrawRow(ctx, rowRect, sorted[i], columns, label + "/Rows[" + i + "]");
                }
            }

            return;
        }

        Widgets.BeginScrollView(body, ref state.Scroll, view);
        try
        {
            using (ctx.PushOffset(body.position - state.Scroll))
            {
                for (int i = 0; i < sorted.Count; i++)
                {
                    Rect rowRect = new(0f, i * row, view.width, row);
                    if (ctx != null && ctx.Pass == UIPass.Draw && i % 2 == 1)
                        Widgets.DrawAltRect(rowRect);

                    if (visualOptions != null && ctx != null && ctx.Pass == UIPass.Draw)
                    {
                        if (visualOptions.HighlightRowOnHover)
                            Widgets.DrawHighlightIfMouseover(rowRect);

                        if (visualOptions.IsRowSelected != null && visualOptions.IsRowSelected(sorted[i]))
                            Widgets.DrawHighlightSelected(rowRect);
                    }

                    DrawRow(ctx, rowRect, sorted[i], columns, label + "/Rows[" + i + "]");
                }
            }
        }
        finally
        {
            Widgets.EndScrollView();
        }
    }

    private static void DrawHeader<T>(UIContext ctx, Rect rect, IList<Column<T>> columns, ref State state, string label, VisualOptions<T> visualOptions)
    {
        Rect[] cells = AllocateColumns(rect, columns);
        if (state.SortColumn < 0 || state.SortColumn >= columns.Count)
            state.SortColumn = 0;

        bool useVanillaSortIcons = visualOptions != null && visualOptions.UseVanillaSortIcons;

        for (int i = 0; i < columns.Count; i++)
        {
            Rect cell = cells[i];
            string text = columns[i].Label ?? string.Empty;
            bool isSortedColumn = i == state.SortColumn;

            bool clicked;
            if (useVanillaSortIcons)
            {
                Texture2D icon = null;
                if (isSortedColumn)
                    icon = state.SortDescending ? D2VanillaTex.SortingDescending : D2VanillaTex.Sorting;

                clicked = DrawVanillaHeaderButton(ctx, cell, text, icon, label + "/Col[" + i + "]");
            }
            else
            {
                if (isSortedColumn)
                    text += state.SortDescending ? " ▼" : " ▲";

                clicked = D2Widgets.ButtonText(ctx, cell, text, label + "/Col[" + i + "]");
            }

            if (clicked)
            {
                if (state.SortColumn == i)
                    state.SortDescending = !state.SortDescending;
                else
                {
                    state.SortColumn = i;
                    state.SortDescending = false;
                }
            }

            if (ctx != null && ctx.Pass == UIPass.Draw)
            {
                string tooltip = columns[i].Tooltip;
                if (useVanillaSortIcons && isSortedColumn)
                {
                    string sortState = state.SortDescending ? "Sorted descending" : "Sorted ascending";
                    tooltip = tooltip.NullOrEmpty() ? sortState : tooltip + "\n" + sortState;
                }

                if (!tooltip.NullOrEmpty())
                    TooltipHandler.TipRegion(cell, tooltip);
            }
        }
    }

    private static bool DrawVanillaHeaderButton(UIContext ctx, Rect rect, string text, Texture2D sortIcon, string label)
    {
        ctx?.Record(rect, UIRectTag.Button, label ?? "HeaderButton");
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return false;

        bool clicked;
        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            clicked = Widgets.ButtonText(rect, string.Empty);
        }

        float inset = 6f;
        float gap = 4f;
        Rect contentRect = rect.ContractedBy(inset);
        Rect labelRect = contentRect;

        if (sortIcon != null)
        {
            float iconSlotW = Mathf.Min(contentRect.height, ctx?.Style?.MinClickSize ?? 24f);
            Rect iconSlot = new(contentRect.xMax - iconSlotW, contentRect.y, iconSlotW, contentRect.height);
            Rect iconRect = iconSlot.ContractedBy(ctx?.Style?.IconInset ?? 2f);
            labelRect.width = Mathf.Max(0f, iconSlot.x - gap - labelRect.x);
            D2Widgets.DrawTextureFitted(ctx, iconRect, sortIcon, label + "/SortIcon");
        }

        D2Widgets.LabelClippedAligned(ctx, labelRect, text ?? string.Empty, TextAnchor.MiddleCenter, label + "/Text");
        return clicked;
    }

    private static void DrawRow<T>(UIContext ctx, Rect rect, T item, IList<Column<T>> columns, string label)
    {
        Rect[] cells = AllocateColumns(rect, columns);
        for (int i = 0; i < columns.Count; i++)
        {
            Rect cell = cells[i].ContractedBy(4f);
            Column<T> col = columns[i];
            if (col.Draw != null)
                col.Draw(ctx, cell, item);
            else
                D2Widgets.LabelClipped(ctx, cell, col.Text != null ? col.Text(item) : string.Empty, label + "/Cell[" + i + "]");
        }
    }

    private static List<T> BuildSorted<T>(IList<T> items, IList<Column<T>> columns, State state)
    {
        var sorted = new List<T>();
        if (items != null)
            for (int i = 0; i < items.Count; i++)
                sorted.Add(items[i]);

        if (columns == null || columns.Count == 0 || state.SortColumn < 0 || state.SortColumn >= columns.Count)
            return sorted;

        Column<T> col = columns[state.SortColumn];
        if (col == null || col.SortValue == null)
            return sorted;

        sorted.Sort((a, b) =>
        {
            IComparable av = col.SortValue(a);
            IComparable bv = col.SortValue(b);
            int cmp;
            if (ReferenceEquals(av, bv)) cmp = 0;
            else if (av == null) cmp = -1;
            else if (bv == null) cmp = 1;
            else cmp = av.CompareTo(bv);
            return state.SortDescending ? -cmp : cmp;
        });

        return sorted;
    }

    private static Rect[] AllocateColumns<T>(Rect rect, IList<Column<T>> columns)
    {
        var cells = new Rect[columns.Count];
        float totalWeight = 0f;
        for (int i = 0; i < columns.Count; i++)
            totalWeight += Mathf.Max(0.0001f, columns[i].Weight);

        float x = rect.x;
        for (int i = 0; i < columns.Count; i++)
        {
            float weight = Mathf.Max(0.0001f, columns[i].Weight);
            float w = i == columns.Count - 1 ? rect.xMax - x : rect.width * (weight / totalWeight);
            cells[i] = new Rect(x, rect.y, Mathf.Max(0f, w), rect.height);
            x += w;
        }

        return cells;
    }
}
