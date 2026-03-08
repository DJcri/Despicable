using System;
using UnityEngine;
using Verse;
using Despicable.UIFramework;

namespace Despicable.UIFramework.Controls;
/// <summary>
/// Common control wrappers that tend to repeat everywhere.
///
/// These helpers do not invent new behavior. They standardize common glue such as:
/// - clamping and parse rules for numeric fields
/// - consistent padding and icon placement for search boxes
/// - enum dropdown option generation
///
/// Every wrapper records semantic tags so the overlay can reason about UI intent.
/// Kept separate from D2Widgets to avoid bloat.
/// </summary>
public static class D2Fields
{
    // Local text buffers for numeric fields so users can type partial values ("-", "3.") without snapping back.
    // Keyed by a stable id derived from ctx scope + provided label.
    private static readonly System.Collections.Generic.Dictionary<string, string> textBuffersByKey =
        new System.Collections.Generic.Dictionary<string, string>();

    private static string BuildBufferKey(UIContext ctx, string label)
    {
        // If caller doesn't provide a label, fall back to scope path.
        // (Not perfect, but stable enough for demo use and most wrappers.)
        string scope = ctx != null ? ctx.ScopePath : string.Empty;
        if (string.IsNullOrEmpty(label)) return scope;
        if (string.IsNullOrEmpty(scope)) return label;
        return scope + "/" + label;
    }

    public static bool SearchBox(UIContext ctx, Rect rect, ref string text, string placeholder = null, string tooltip = null, string label = "SearchBox")
    {
        ctx?.RecordRect(rect, UIRectTag.Control_Search, label);

        if (ctx == null || ctx.Pass != UIPass.Draw)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        // Layout: [ TextField ][ X ]
        float clearW = 24f;
        bool showClear = !string.IsNullOrEmpty(text);
        Rect fieldRect = rect;
        Rect clearRect = Rect.zero;

        if (showClear)
        {
            fieldRect.width = Mathf.Max(0f, rect.width - (clearW + 4f));
            clearRect = new Rect(fieldRect.xMax + 4f, rect.y, clearW, rect.height);
            ctx.RecordRect(clearRect, UIRectTag.Button, label + "/Clear");
        }

        string before = text ?? string.Empty;
        text = Widgets.TextField(fieldRect, before);

        // Placeholder hint (visual only). RimWorld doesn't provide a built-in placeholder.
        if (string.IsNullOrEmpty(text) && !placeholder.NullOrEmpty())
        {
            var hint = fieldRect.ContractedBy(4f);
            using (new GUIColorScope(new Color(1f, 1f, 1f, 0.45f)))
                Widgets.Label(hint, placeholder);
        }

        bool changed = text != before;

        if (showClear)
        {
            // Keep it boring and reliable: a tiny "x" text button.
            if (Widgets.ButtonText(clearRect, "x", drawBackground: true, doMouseoverSound: true, active: true))
            {
                text = string.Empty;
                return true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Opt-in vanilla search row: optional left search icon, text field, and icon-only clear action.
    /// Existing SearchBox visuals stay unchanged unless callers switch to this helper.
    /// </summary>
    public static bool SearchBoxVanilla(UIContext ctx, Rect rect, ref string text, string placeholder = null, string tooltip = null, bool showSearchIcon = false, string label = "SearchBoxVanilla")
    {
        ctx?.RecordRect(rect, UIRectTag.Control_Search, label);

        float gap = 4f;
        float iconW = ctx?.Style?.MinClickSize ?? 24f;
        bool showClear = !string.IsNullOrEmpty(text);

        Rect remaining = rect;
        Rect searchRect = Rect.zero;
        Rect clearRect = Rect.zero;

        if (showSearchIcon)
        {
            searchRect = new Rect(remaining.x, remaining.y, Mathf.Min(iconW, remaining.width), remaining.height);
            remaining = new Rect(searchRect.xMax + gap, remaining.y, Mathf.Max(0f, remaining.width - (searchRect.width + gap)), remaining.height);
            ctx?.RecordRect(searchRect, UIRectTag.Icon, label + "/SearchIcon");
        }

        if (showClear)
        {
            float clearW = Mathf.Min(iconW, remaining.width);
            clearRect = new Rect(Mathf.Max(remaining.x, remaining.xMax - clearW), remaining.y, clearW, remaining.height);
            remaining.width = Mathf.Max(0f, clearRect.x - gap - remaining.x);
            ctx?.RecordRect(clearRect, UIRectTag.Button, label + "/Clear");
        }

        Rect fieldRect = remaining;
        string before = text ?? string.Empty;

        if (ctx == null || ctx.Pass != UIPass.Draw)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        if (showSearchIcon && D2VanillaTex.SearchInspector != null)
        {
            Rect iconRect = searchRect.ContractedBy(ctx?.Style?.IconInset ?? 2f);
            D2Widgets.DrawTextureFitted(ctx, iconRect, D2VanillaTex.SearchInspector, label + "/SearchIcon");
        }

        text = D2Widgets.TextField(ctx, fieldRect, before, label: label + "/Field");

        if (string.IsNullOrEmpty(text) && !placeholder.NullOrEmpty())
        {
            Rect hint = fieldRect.ContractedBy(4f);
            using (new GUIColorScope(new Color(1f, 1f, 1f, 0.45f)))
                Widgets.Label(hint, placeholder);
        }

        bool changed = text != before;

        if (showClear && D2Widgets.ButtonIcon(ctx, clearRect, D2VanillaTex.CloseXSmall, tooltip: "Clear", label: label + "/Clear"))
        {
            text = string.Empty;
            return true;
        }

        return changed;
    }

    public static bool IntField(UIContext ctx, Rect rect, ref int value, int? min = null, int? max = null, string tooltip = null, string label = "IntField")
    {
        ctx?.RecordRect(rect, UIRectTag.Control_Field, label);

        if (ctx == null || ctx.Pass != UIPass.Draw)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        string k = BuildBufferKey(ctx, label);
        if (!textBuffersByKey.TryGetValue(k, out var buffer) || buffer == null)
            buffer = value.ToString();

        string newBuffer = Widgets.TextField(rect, buffer);
        textBuffersByKey[k] = newBuffer;

        // Only commit if it's a valid int.
        if (!int.TryParse(newBuffer, out var parsed))
            return false;

        if (min.HasValue) parsed = Math.Max(min.Value, parsed);
        if (max.HasValue) parsed = Math.Min(max.Value, parsed);

        if (parsed == value) return false;
        value = parsed;
        textBuffersByKey[k] = value.ToString();
        return true;
    }

    public static bool FloatField(UIContext ctx, Rect rect, ref float value, float? min = null, float? max = null, string tooltip = null, string label = "FloatField")
    {
        ctx?.RecordRect(rect, UIRectTag.Control_Field, label);

        if (ctx == null || ctx.Pass != UIPass.Draw)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        string k = BuildBufferKey(ctx, label);
        if (!textBuffersByKey.TryGetValue(k, out var buffer) || buffer == null)
            buffer = value.ToString("0.###");

        string newBuffer = Widgets.TextField(rect, buffer);
        textBuffersByKey[k] = newBuffer;

        // Only commit if it's a valid float.
        if (!float.TryParse(newBuffer, out var parsed))
            return false;

        if (min.HasValue) parsed = Mathf.Max(min.Value, parsed);
        if (max.HasValue) parsed = Mathf.Min(max.Value, parsed);

        if (Math.Abs(parsed - value) < 0.0001f) return false;
        value = parsed;
        textBuffersByKey[k] = value.ToString("0.###");
        return true;
    }


    public static bool IntStepper(UIContext ctx, Rect rect, ref int value, int step = 1, int? min = null, int? max = null, string tooltip = null, string label = "IntStepper")
    {
        ctx?.RecordRect(rect, UIRectTag.Control_Field, label);

        float buttonW = Mathf.Min(24f, Mathf.Max(18f, rect.width * 0.18f));
        Rect left = new(rect.x, rect.y, buttonW, rect.height);
        Rect right = new(rect.xMax - buttonW, rect.y, buttonW, rect.height);
        Rect field = new(left.xMax + 4f, rect.y, Mathf.Max(0f, rect.width - (buttonW * 2f) - 8f), rect.height);

        bool changed = false;
        int before = value;

        if (ctx == null || ctx.Pass != UIPass.Draw)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        if (D2Widgets.ButtonText(ctx, left, "-", label + "/Dec"))
        {
            int next = value - Mathf.Max(1, step);
            if (min.HasValue) next = Math.Max(min.Value, next);
            if (max.HasValue) next = Math.Min(max.Value, next);
            value = next;
            changed = true;
        }

        if (IntField(ctx, field, ref value, min, max, tooltip: null, label: label + "/Field"))
            changed = true;

        if (D2Widgets.ButtonText(ctx, right, "+", label + "/Inc"))
        {
            int next = value + Mathf.Max(1, step);
            if (min.HasValue) next = Math.Max(min.Value, next);
            if (max.HasValue) next = Math.Min(max.Value, next);
            value = next;
            changed = true;
        }

        return changed || value != before;
    }

    public static bool IntStepperVanilla(UIContext ctx, Rect rect, ref int value, int step = 1, int? min = null, int? max = null, string tooltip = null, string label = "IntStepperVanilla")
    {
        ctx?.RecordRect(rect, UIRectTag.Control_Field, label);

        float buttonW = Mathf.Min(24f, Mathf.Max(18f, rect.width * 0.18f));
        Rect left = new(rect.x, rect.y, buttonW, rect.height);
        Rect right = new(rect.xMax - buttonW, rect.y, buttonW, rect.height);
        Rect field = new(left.xMax + 4f, rect.y, Mathf.Max(0f, rect.width - (buttonW * 2f) - 8f), rect.height);

        bool changed = false;
        int before = value;

        if (ctx == null || ctx.Pass != UIPass.Draw)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        if (D2Widgets.ButtonIcon(ctx, left, D2VanillaTex.Minus, tooltip: "Decrease", label: label + "/Dec"))
        {
            int next = value - Mathf.Max(1, step);
            if (min.HasValue) next = Math.Max(min.Value, next);
            if (max.HasValue) next = Math.Min(max.Value, next);
            value = next;
            changed = true;
        }

        if (IntField(ctx, field, ref value, min, max, tooltip: null, label: label + "/Field"))
            changed = true;

        if (D2Widgets.ButtonIcon(ctx, right, D2VanillaTex.Plus, tooltip: "Increase", label: label + "/Inc"))
        {
            int next = value + Mathf.Max(1, step);
            if (min.HasValue) next = Math.Max(min.Value, next);
            if (max.HasValue) next = Math.Min(max.Value, next);
            value = next;
            changed = true;
        }

        return changed || value != before;
    }

    public static bool FloatStepper(UIContext ctx, Rect rect, ref float value, float step = 0.1f, float? min = null, float? max = null, string tooltip = null, string label = "FloatStepper")
    {
        ctx?.RecordRect(rect, UIRectTag.Control_Field, label);

        float buttonW = Mathf.Min(24f, Mathf.Max(18f, rect.width * 0.18f));
        Rect left = new(rect.x, rect.y, buttonW, rect.height);
        Rect right = new(rect.xMax - buttonW, rect.y, buttonW, rect.height);
        Rect field = new(left.xMax + 4f, rect.y, Mathf.Max(0f, rect.width - (buttonW * 2f) - 8f), rect.height);

        bool changed = false;
        float before = value;

        if (ctx == null || ctx.Pass != UIPass.Draw)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        float delta = Mathf.Abs(step) < 0.0001f ? 0.1f : Mathf.Abs(step);

        if (D2Widgets.ButtonText(ctx, left, "-", label + "/Dec"))
        {
            float next = value - delta;
            if (min.HasValue) next = Mathf.Max(min.Value, next);
            if (max.HasValue) next = Mathf.Min(max.Value, next);
            value = next;
            changed = true;
        }

        if (FloatField(ctx, field, ref value, min, max, tooltip: null, label: label + "/Field"))
            changed = true;

        if (D2Widgets.ButtonText(ctx, right, "+", label + "/Inc"))
        {
            float next = value + delta;
            if (min.HasValue) next = Mathf.Max(min.Value, next);
            if (max.HasValue) next = Mathf.Min(max.Value, next);
            value = next;
            changed = true;
        }

        return changed || Math.Abs(value - before) > 0.0001f;
    }

    public static bool FloatStepperVanilla(UIContext ctx, Rect rect, ref float value, float step = 0.1f, float? min = null, float? max = null, string tooltip = null, string label = "FloatStepperVanilla")
    {
        ctx?.RecordRect(rect, UIRectTag.Control_Field, label);

        float buttonW = Mathf.Min(24f, Mathf.Max(18f, rect.width * 0.18f));
        Rect left = new(rect.x, rect.y, buttonW, rect.height);
        Rect right = new(rect.xMax - buttonW, rect.y, buttonW, rect.height);
        Rect field = new(left.xMax + 4f, rect.y, Mathf.Max(0f, rect.width - (buttonW * 2f) - 8f), rect.height);

        bool changed = false;
        float before = value;

        if (ctx == null || ctx.Pass != UIPass.Draw)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        float delta = Mathf.Abs(step) < 0.0001f ? 0.1f : Mathf.Abs(step);

        if (D2Widgets.ButtonIcon(ctx, left, D2VanillaTex.Minus, tooltip: "Decrease", label: label + "/Dec"))
        {
            float next = value - delta;
            if (min.HasValue) next = Mathf.Max(min.Value, next);
            if (max.HasValue) next = Mathf.Min(max.Value, next);
            value = next;
            changed = true;
        }

        if (FloatField(ctx, field, ref value, min, max, tooltip: null, label: label + "/Field"))
            changed = true;

        if (D2Widgets.ButtonIcon(ctx, right, D2VanillaTex.Plus, tooltip: "Increase", label: label + "/Inc"))
        {
            float next = value + delta;
            if (min.HasValue) next = Mathf.Max(min.Value, next);
            if (max.HasValue) next = Mathf.Min(max.Value, next);
            value = next;
            changed = true;
        }

        return changed || Math.Abs(value - before) > 0.0001f;
    }

    public static bool EnumDropdown<TEnum>(UIContext ctx, Rect rect, TEnum current, Action<TEnum> set, Func<TEnum, string> labeler = null, string tooltip = null)
        where TEnum : struct, Enum
    {
        ctx?.RecordRect(rect, UIRectTag.Control_Field, "EnumDropdown");

        if (ctx == null || ctx.Pass != UIPass.Draw)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        string buttonLabel = labeler != null ? labeler(current) : current.ToString();
        bool clicked = Widgets.ButtonText(rect, buttonLabel);

        if (!clicked) return false;

        var opts = new System.Collections.Generic.List<FloatMenuOption>();
        foreach (var v in (TEnum[])Enum.GetValues(typeof(TEnum)))
        {
            var local = v;
            string optionLabel = labeler != null ? labeler(local) : local.ToString();
            opts.Add(new FloatMenuOption(optionLabel, () => set?.Invoke(local)));
        }

        Find.WindowStack.Add(new FloatMenu(opts));
        return true;
    }

    public static bool EnumDropdownVanilla<TEnum>(UIContext ctx, Rect rect, TEnum current, Action<TEnum> set, Func<TEnum, string> labeler = null, string tooltip = null, string label = "EnumDropdownVanilla")
        where TEnum : struct, Enum
    {
        var opts = new System.Collections.Generic.List<FloatMenuOption>();
        foreach (var v in (TEnum[])Enum.GetValues(typeof(TEnum)))
        {
            var local = v;
            string optionLabel = labeler != null ? labeler(local) : local.ToString();
            opts.Add(new FloatMenuOption(optionLabel, () => set?.Invoke(local)));
        }

        string buttonLabel = labeler != null ? labeler(current) : current.ToString();
        return D2Widgets.MenuButtonVanilla(ctx, rect, buttonLabel, opts, tooltip, label);
    }
    /// <summary>
    /// Clears per-control text buffers so transient editing state does not outlive the active UI session.
    /// </summary>
    public static void ResetRuntimeState()
    {
        textBuffersByKey.Clear();
    }

    public static void ClearBuffers()
    {
        ResetRuntimeState();
    }

}
