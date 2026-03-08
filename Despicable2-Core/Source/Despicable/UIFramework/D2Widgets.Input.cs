using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.UIFramework;
public static partial class D2Widgets
{
    public static bool CheckboxLabeled(UIContext ctx, Rect rect, string labelText, ref bool checkOn, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Checkbox, label ?? ("Checkbox:" + (labelText ?? "Unnamed")));
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return checkOn;

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            Widgets.CheckboxLabeled(rect, labelText, ref checkOn);
        }

        return checkOn;
    }

    public static bool CheckboxLabeledKey(UIContext ctx, Rect rect, string key, ref bool checkOn, string label = null, params object[] args)
    {
        return CheckboxLabeled(ctx, rect, ResolveKeyText(key, args), ref checkOn, label ?? key);
    }

    /// <summary>
    /// Checkbox + clipped label. Uses a dedicated label region so long labels do not overflow.
    /// If clipped, adds a tooltip with the full label.
    /// </summary>
    public static bool CheckboxLabeledClipped(UIContext ctx, Rect rect, string labelText, ref bool checkOn, float? checkboxWidth = null, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Checkbox, label ?? ("CheckboxClipped:" + (labelText ?? "Unnamed")));
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return checkOn;

        if (labelText == null)
            labelText = string.Empty;

        float cbW = checkboxWidth ?? 24f;

        Rect cbRect = new(rect.x, rect.y + (rect.height - cbW) * 0.5f, cbW, cbW);
        Rect labelRect = new(rect.x + cbW + 4f, rect.y, rect.width - (cbW + 4f), rect.height);

        ctx?.Record(cbRect, UIRectTag.Icon, (label ?? "Checkbox") + "/Icon");
        ctx?.Record(labelRect, UIRectTag.Input, (label ?? "Checkbox") + "/LabelHitbox");

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.MiddleLeft, true))
        {
            bool prev = checkOn;
            Widgets.Checkbox(cbRect.position, ref checkOn, cbW);

            if (Widgets.ButtonInvisible(labelRect))
                checkOn = !prev;

            string truncated = labelText.Truncate(labelRect.width);
            Widgets.Label(labelRect, truncated);
            if (truncated != labelText)
                TooltipHandler.TipRegion(labelRect, labelText);
        }

        return checkOn;
    }

    public static bool CheckboxLabeledClippedKey(UIContext ctx, Rect rect, string key, ref bool checkOn, float? checkboxWidth = null, string label = null, params object[] args)
    {
        return CheckboxLabeledClipped(ctx, rect, ResolveKeyText(key, args), ref checkOn, checkboxWidth, label ?? key);
    }

    public static float HorizontalSlider(UIContext ctx, Rect rect, float value, float leftValue, float rightValue, bool showValueLabel = true, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Slider, label ?? "Slider");
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return value;

        rect = ControlRect(ctx, rect);

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            if (!showValueLabel)
                return Widgets.HorizontalSlider(rect, value, leftValue, rightValue, false);

            string valStr = value.ToString("0.##");
            string minStr = leftValue.ToString("0.##");
            string maxStr = rightValue.ToString("0.##");
            float labelW = Mathf.Max(Text.CalcSize(minStr).x, Text.CalcSize(maxStr).x) + 10f;
            labelW = Mathf.Clamp(labelW, 34f, Mathf.Max(34f, rect.width * 0.45f));
            float gap = ctx?.Style != null ? ctx.Style.Gap : 6f;
            float sliderW = Mathf.Max(0f, rect.width - labelW - gap);

            var sliderRect = new Rect(rect.x, rect.y, sliderW, rect.height);
            var valueRect = new Rect(
                sliderRect.xMax + gap,
                rect.y,
                Mathf.Max(0f, rect.xMax - (sliderRect.xMax + gap)),
                rect.height);

            ctx?.Record(sliderRect, UIRectTag.Slider, (label ?? "Slider") + "/Rail");
            ctx?.Record(valueRect, UIRectTag.Label, (label ?? "Slider") + "/ValueLabel");

            float newVal = Widgets.HorizontalSlider(sliderRect, value, leftValue, rightValue, false);

            var prevAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(valueRect, valStr);
            Text.Anchor = prevAnchor;

            return newVal;
        }
    }

    public static string TextField(UIContext ctx, Rect rect, string text, int maxLength = 9999, string label = null)
    {
        ctx?.Record(rect, UIRectTag.TextField, label ?? "TextField");
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return text;

        rect = ControlRect(ctx, rect);

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            return Widgets.TextField(rect, text, maxLength);
        }
    }

    public static string TextArea(UIContext ctx, Rect rect, string text, bool readOnly = false, string label = null)
    {
        ctx?.Record(rect, UIRectTag.TextArea, label ?? "TextArea");
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return text;

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            return Widgets.TextArea(rect, text, readOnly);
        }
    }

    /// <summary>
    /// Dropdown button pattern: on click, opens a FloatMenu with provided options.
    /// Returns true if the menu was opened.
    /// </summary>
    public static bool DropdownButton(UIContext ctx, Rect rect, string text, List<FloatMenuOption> options, string label = null)
    {
        rect = ControlRect(ctx, rect);
        ctx?.Record(rect, UIRectTag.Input, label ?? ("Dropdown:" + (text ?? "Unnamed")));
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return false;

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            if (Widgets.ButtonText(rect, text))
            {
                if (options != null && options.Count > 0)
                    Find.WindowStack.Add(new FloatMenu(options));
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Opt-in vanilla-ish dropdown/button hybrid: a regular text button plus a dedicated drop icon hitbox.
    /// Clicking either region opens the provided FloatMenu.
    /// </summary>
    public static bool MenuButtonVanilla(UIContext ctx, Rect rect, string text, List<FloatMenuOption> options, string tooltip = null, string label = null)
    {
        rect = ControlRect(ctx, rect);
        string id = label ?? ("MenuButtonVanilla:" + (text ?? "Unnamed"));

        ctx?.Record(rect, UIRectTag.Input, id);
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return false;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        float gap = 4f;
        float iconW = Mathf.Min(ctx?.Style?.MinClickSize ?? 24f, Mathf.Max(18f, rect.width * 0.18f));
        Rect mainRect = new(rect.x, rect.y, Mathf.Max(0f, rect.width - iconW - gap), rect.height);
        Rect iconRect = new(mainRect.xMax + gap, rect.y, Mathf.Max(0f, rect.xMax - (mainRect.xMax + gap)), rect.height);

        bool clicked = D2Widgets.ButtonText(ctx, mainRect, text ?? string.Empty, id + "/Main");

        if (D2Widgets.ButtonIcon(ctx, iconRect, D2VanillaTex.Drop, tooltip: tooltip ?? "More options", label: id + "/Drop"))
            clicked = true;

        if (!clicked || options == null || options.Count == 0)
            return false;

        Find.WindowStack.Add(new FloatMenu(options));
        return true;
    }

    public static bool DropdownButtonVanilla(UIContext ctx, Rect rect, string text, List<FloatMenuOption> options, string tooltip = null, string label = null)
    {
        return MenuButtonVanilla(ctx, rect, text, options, tooltip, label ?? ("DropdownVanilla:" + (text ?? "Unnamed")));
    }

    public static bool RadioButton(UIContext ctx, Rect rect, bool active, string labelText, string label = null)
    {
        ctx?.Record(rect, UIRectTag.Tab, label ?? ("Radio:" + (labelText ?? "Unnamed")));
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return active;

        using (new GUIEnabledScope(true))
        using (new GUIColorScope(Color.white))
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, true))
        {
            string text = active ? "● " + labelText : "○ " + labelText;
            if (Widgets.ButtonText(rect, text))
                return true;

            return false;
        }
    }

    public static bool RadioButtonVanilla(UIContext ctx, Rect rect, bool active, string labelText, string tooltip = null, string label = null)
    {
        string id = label ?? ("RadioVanilla:" + (labelText ?? "Unnamed"));
        ctx?.Record(rect, UIRectTag.Tab, id);
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return active;

        if (!tooltip.NullOrEmpty())
            TooltipHandler.TipRegion(rect, tooltip);

        float gap = 4f;
        float iconW = Mathf.Min(ctx?.Style?.MinClickSize ?? 24f, rect.width);
        Rect iconSlot = new(rect.x, rect.y, iconW, rect.height);
        Rect iconRect = iconSlot.ContractedBy(ctx?.Style?.IconInset ?? 2f);
        Rect labelRect = new(iconSlot.xMax + gap, rect.y, Mathf.Max(0f, rect.width - iconW - gap), rect.height);

        if (Mouse.IsOver(rect))
            Widgets.DrawHighlightIfMouseover(rect);

        D2Widgets.DrawTextureFitted(ctx, iconRect, active ? D2VanillaTex.RadioButOn : D2VanillaTex.RadioButOff, id + "/Icon");
        D2Widgets.LabelClippedAligned(ctx, labelRect, labelText ?? string.Empty, TextAnchor.MiddleLeft, id + "/Label");

        return Widgets.ButtonInvisible(rect);
    }
}
