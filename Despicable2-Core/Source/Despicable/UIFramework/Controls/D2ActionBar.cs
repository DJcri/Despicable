using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Framework-native action row that auto-sizes items from content and wraps when needed.
///
/// Supports:
/// - normal buttons
/// - selector buttons
/// - checkboxes
/// </summary>
public static class D2ActionBar
{
    public enum ItemKind
    {
        Button = 0,
        Selector = 1,
        Checkbox = 2
    }

    public struct Item
    {
        public string Id;
        public string Label;
        public string Tooltip;
        public ItemKind Kind;
        public bool Selected;
        public bool Disabled;
        public string DisabledReason;
        public bool Checked;
        public float? MinWidthOverride;

        public Item(string id, string label, ItemKind kind = ItemKind.Button, string tooltip = null)
        {
            Id = id;
            Label = label;
            Tooltip = tooltip;
            Kind = kind;
            Selected = false;
            Disabled = false;
            DisabledReason = null;
            Checked = false;
            MinWidthOverride = null;
        }
    }

    public static Item ItemKey(string id, string labelKey, ItemKind kind = ItemKind.Button, string tooltipKey = null)
    {
        string label = string.IsNullOrEmpty(labelKey) ? string.Empty : labelKey.Translate().ToString();
        string tooltip = string.IsNullOrEmpty(tooltipKey) ? null : tooltipKey.Translate().ToString();
        return new Item(id, label, kind, tooltip);
    }

    public readonly struct Result
    {
        public readonly bool Clicked;
        public readonly string ActivatedId;
        public readonly Dictionary<string, bool> CheckboxValues;

        public Result(bool clicked, string activatedId, Dictionary<string, bool> checkboxValues)
        {
            Clicked = clicked;
            ActivatedId = activatedId;
            CheckboxValues = checkboxValues ?? new Dictionary<string, bool>();
        }
    }

    public static float MeasureHeight(UIContext ctx, Rect rect, IList<Item> items)
    {
        if (items == null || items.Count == 0)
            return 0f;

        float gap = ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f;
        float line = ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f;
        float x = rect.xMin;
        float y = rect.yMin;
        int lines = 1;

        for (int i = 0; i < items.Count; i++)
        {
            float w = MeasureItemWidth(ctx, items[i]);
            bool wrap = x > rect.xMin && (x + w) > rect.xMax;
            if (wrap)
            {
                x = rect.xMin;
                y += line + gap;
                lines++;
            }

            x += Mathf.Min(w, rect.width) + gap;
        }

        return Mathf.Max(0f, (lines * line) + (Mathf.Max(0, lines - 1) * gap));
    }

    public static Result Draw(UIContext ctx, Rect rect, IList<Item> items, string label = null)
    {
        if (ctx != null)
            ctx.RecordRect(rect, UIRectTag.Input, label ?? "ActionBar", "Items=" + (items != null ? items.Count.ToString() : "0"));

        if (items == null || items.Count == 0)
            return new Result(false, null, new Dictionary<string, bool>());

        var flow = new HFlow(ctx, rect, ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f, ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f);
        string activated = null;
        bool clicked = false;
        var checkboxValues = new Dictionary<string, bool>();

        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            float w = MeasureItemWidth(ctx, item);
            Rect slot = flow.Next(w);
            bool itemClicked = false;

            switch (item.Kind)
            {
                case ItemKind.Selector:
                    itemClicked = D2Selectors.SelectorButton(ctx, slot, item.Label, item.Selected, item.Disabled, item.DisabledReason, item.Tooltip, item.Id);
                    break;

                case ItemKind.Checkbox:
                    bool newVal = item.Checked;
                    if (!string.IsNullOrEmpty(item.Tooltip) && ctx != null && ctx.Pass == UIPass.Draw)
                        TooltipHandler.TipRegion(slot, item.Tooltip);
                    D2Widgets.CheckboxLabeled(ctx, slot, item.Label, ref newVal, item.Id ?? item.Label);
                    checkboxValues[item.Id ?? item.Label ?? ("Checkbox[" + i + "]")] = newVal;
                    itemClicked = (newVal != item.Checked);
                    break;

                default:
                    if (!string.IsNullOrEmpty(item.Tooltip) && ctx != null && ctx.Pass == UIPass.Draw)
                        TooltipHandler.TipRegion(slot, item.Tooltip);
                    if (item.Disabled && !string.IsNullOrEmpty(item.DisabledReason) && ctx != null && ctx.Pass == UIPass.Draw)
                        TooltipHandler.TipRegion(slot, item.DisabledReason);
                    if (item.Disabled)
                    {
                        using (new GUIEnabledScope(false))
                            itemClicked = D2Widgets.ButtonText(ctx, slot, item.Label, item.Id ?? item.Label);
                    }
                    else
                    {
                        itemClicked = D2Widgets.ButtonText(ctx, slot, item.Label, item.Id ?? item.Label);
                    }
                    break;
            }

            if (itemClicked && !clicked)
            {
                clicked = true;
                activated = item.Id ?? item.Label;
            }
        }

        return new Result(clicked, activated, checkboxValues);
    }

    private static float MeasureItemWidth(UIContext ctx, Item item)
    {
        if (item.MinWidthOverride.HasValue)
            return Mathf.Max(0f, item.MinWidthOverride.Value);

        float minClick = ctx != null && ctx.Style != null ? ctx.Style.MinClickSize : 28f;
        float pad = ctx != null && ctx.Style != null ? ctx.Style.Pad : 10f;
        float labelW = MeasureLabelWidth(item.Label);

        switch (item.Kind)
        {
            case ItemKind.Checkbox:
                return Mathf.Max(minClick, 24f + pad + labelW);
            default:
                return Mathf.Max(minClick * 1.5f, labelW + (pad * 2f));
        }
    }

    private static float MeasureLabelWidth(string label)
    {
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, false))
        {
            return Text.CalcSize(label ?? string.Empty).x;
        }
    }
}
