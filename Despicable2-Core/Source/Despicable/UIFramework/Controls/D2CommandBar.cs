using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Despicable.UIFramework.Blueprints;
using Despicable.UIFramework.Layout;

namespace Despicable.UIFramework.Controls;
// Guardrail-Reason: Command bar draw flow stays co-located with menu construction while action memory remains one control concern.
/// <summary>
/// Context-aware command bar with optional split-button menus and repeat-last support.
///
/// The command bar keeps behavior simple and boring:
/// - primary click runs the command action (or opens its menu if there is no direct action)
/// - split arrow opens the menu
/// - commands can register as the remembered action for a repeat key
/// - a dedicated repeat command can replay the last remembered action for that key
/// </summary>
public static class D2CommandBar
{
    public sealed class Command
    {
        public string Id;
        public string Label;
        public string Tooltip;
        public bool Disabled;
        public string DisabledReason;
        public Action Action;
        public List<D2FloatMenuBlueprint.Option> MenuOptions;
        public float? MinWidthOverride;
        public string RememberKey;
        public bool RepeatLast;

        public Command(string id, string label, Action action = null)
        {
            Id = id;
            Label = label;
            Action = action;
        }
    }

    public struct Result
    {
        public bool Clicked;
        public string ActivatedId;
        public bool Repeated;
    }

    private sealed class RememberedAction
    {
        public string Label;
        public Action Action;
        public string Tooltip;
    }

    private static readonly Dictionary<string, RememberedAction> RememberedActionsByKey = new();

    public static float MeasureHeight(UIContext ctx)
    {
        return ctx != null && ctx.Style != null ? ctx.Style.RowHeight : 28f;
    }

    public static Result Draw(UIContext ctx, Rect rect, IList<Command> commands, string label = null)
    {
        if (ctx != null)
        {
            ctx.RecordRect(rect, UIRectTag.Input, label ?? "CommandBar", "Items=" + (commands != null ? commands.Count.ToString() : "0")); // loc-allow-internal: fallback widget id
        }

        if (commands == null || commands.Count == 0)
        {
            return default(Result);
        }

        var flow = new D2HFlow(ctx, rect, MeasureHeight(ctx), ctx != null && ctx.Style != null ? ctx.Style.Gap : 6f);
        var result = new Result();

        for (int i = 0; i < commands.Count; i++)
        {
            Command cmd = commands[i];
            float width = MeasureCommandWidth(ctx, cmd);
            Rect slot = flow.Next(width);
            if (slot.width <= 0f || slot.height <= 0f)
            {
                continue;
            }

            bool hasMenu = cmd.MenuOptions != null && cmd.MenuOptions.Count > 0;
            bool clicked;

            if (hasMenu)
            {
                D2RectSplit.SplitVertical(slot, Mathf.Max(0f, slot.width - 22f), 2f, out Rect main, out Rect arrow);
                clicked = DrawCommandButton(ctx, main, cmd, runAction: true, openMenuOnPrimaryIfNoAction: true);
                bool menuClicked = DrawMenuButton(ctx, arrow, cmd, label ?? "CommandBar", i); // loc-allow-internal: fallback widget id
                clicked |= menuClicked;
            }
            else
            {
                clicked = DrawCommandButton(ctx, slot, cmd, runAction: true, openMenuOnPrimaryIfNoAction: false);
            }

            if (clicked && !result.Clicked)
            {
                result.Clicked = true;
                result.ActivatedId = cmd.Id ?? cmd.Label;
                result.Repeated = cmd.RepeatLast;
            }
        }

        return result;
    }

    public static bool HasRemembered(string rememberKey)
    {
        return !string.IsNullOrEmpty(rememberKey) && RememberedActionsByKey.ContainsKey(rememberKey);
    }

    public static string RememberedLabel(string rememberKey, string fallback = null)
    {
        if (string.IsNullOrEmpty(rememberKey))
        {
            return fallback;
        }

        RememberedAction remembered;
        if (RememberedActionsByKey.TryGetValue(rememberKey, out remembered) && remembered != null && !string.IsNullOrEmpty(remembered.Label))
        {
            return remembered.Label;
        }

        return fallback;
    }

    private static bool DrawCommandButton(UIContext ctx, Rect rect, Command cmd, bool runAction, bool openMenuOnPrimaryIfNoAction)
    {
        string tooltip = ResolveTooltip(cmd);
        if (!string.IsNullOrEmpty(tooltip) && ctx != null && ctx.Pass == UIPass.Draw)
        {
            TooltipHandler.TipRegion(rect, tooltip);
        }

        if (cmd.Disabled)
        {
            using (new GUIEnabledScope(false))
            {
                return D2Widgets.ButtonText(ctx, rect, cmd.Label ?? string.Empty, cmd.Id ?? cmd.Label ?? "Command"); // loc-allow-internal: fallback command id
            }
        }

        bool clicked = D2Widgets.ButtonText(ctx, rect, cmd.Label ?? string.Empty, cmd.Id ?? cmd.Label ?? "Command"); // loc-allow-internal: fallback command id
        if (!clicked || !runAction)
        {
            return clicked;
        }

        if (cmd.RepeatLast)
        {
            ExecuteRepeat(cmd);
            return true;
        }

        if (cmd.Action != null)
        {
            cmd.Action();
            Remember(cmd);
            return true;
        }

        bool hasMenu = cmd.MenuOptions != null && cmd.MenuOptions.Count > 0;
        if (hasMenu && openMenuOnPrimaryIfNoAction)
        {
            Find.WindowStack.Add(new FloatMenu(BuildMenuOptions(cmd)));
            return true;
        }

        return clicked;
    }

    private static bool DrawMenuButton(UIContext ctx, Rect rect, Command cmd, string label, int index)
    {
        if (cmd.MenuOptions == null || cmd.MenuOptions.Count == 0)
        {
            return false;
        }

        string tip = cmd.Disabled ? (cmd.DisabledReason ?? cmd.Tooltip) : (cmd.Tooltip ?? "D2C_UI_MoreOptions".Translate().ToString());
        if (!string.IsNullOrEmpty(tip) && ctx != null && ctx.Pass == UIPass.Draw)
        {
            TooltipHandler.TipRegion(rect, tip);
        }

        if (cmd.Disabled)
        {
            using (new GUIEnabledScope(false))
            {
                return D2Widgets.ButtonText(ctx, rect, "▾", (label ?? "CommandBar") + "/Menu[" + index + "]"); // loc-ignore: symbolic menu arrow and internal widget id
            }
        }

        if (!D2Widgets.ButtonText(ctx, rect, "▾", (label ?? "CommandBar") + "/Menu[" + index + "]")) // loc-ignore: symbolic menu arrow and internal widget id
        {
            return false;
        }

        Find.WindowStack.Add(new FloatMenu(BuildMenuOptions(cmd)));
        return true;
    }

    private static void ExecuteRepeat(Command cmd)
    {
        if (string.IsNullOrEmpty(cmd.RememberKey))
        {
            return;
        }

        RememberedAction remembered;
        if (!RememberedActionsByKey.TryGetValue(cmd.RememberKey, out remembered) || remembered == null)
        {
            return;
        }

        remembered.Action?.Invoke();
    }

    private static void Remember(Command cmd)
    {
        if (cmd == null || cmd.Action == null || string.IsNullOrEmpty(cmd.RememberKey))
        {
            return;
        }

        RememberedActionsByKey[cmd.RememberKey] = new RememberedAction
        {
            Label = cmd.Label,
            Action = cmd.Action,
            Tooltip = cmd.Tooltip
        };
    }


    private static List<FloatMenuOption> BuildMenuOptions(Command cmd)
    {
        var menu = new List<D2FloatMenuBlueprint.Option>();
        if (cmd == null || cmd.MenuOptions == null)
        {
            return D2FloatMenuBlueprint.BuildOptions(menu);
        }

        for (int i = 0; i < cmd.MenuOptions.Count; i++)
        {
            var opt = cmd.MenuOptions[i];
            if (!opt.Disabled && opt.Action != null && !string.IsNullOrEmpty(cmd.RememberKey))
            {
                var local = opt;
                menu.Add(new D2FloatMenuBlueprint.Option(
                    local.Label,
                    () =>
                    {
                        local.Action();
                        RememberedActionsByKey[cmd.RememberKey] = new RememberedAction
                        {
                            Label = local.Label,
                            Action = local.Action,
                            Tooltip = local.Tooltip
                        };
                    },
                    disabled: false,
                    disabledReason: null,
                    tooltip: local.Tooltip));
            }
            else
            {
                menu.Add(opt);
            }
        }

        return D2FloatMenuBlueprint.BuildOptions(menu);
    }

    private static float MeasureCommandWidth(UIContext ctx, Command cmd)
    {
        if (cmd != null && cmd.MinWidthOverride.HasValue)
        {
            return Mathf.Max(0f, cmd.MinWidthOverride.Value);
        }

        float minClick = ctx != null && ctx.Style != null ? ctx.Style.MinClickSize : 24f;
        float pad = ctx != null && ctx.Style != null ? ctx.Style.Pad : 10f;
        float labelW;
        using (new TextStateScope(GameFont.Small, TextAnchor.UpperLeft, false))
        {
            labelW = Text.CalcSize((cmd != null ? cmd.Label : null) ?? string.Empty).x;
        }

        bool hasMenu = cmd != null && cmd.MenuOptions != null && cmd.MenuOptions.Count > 0;
        float arrowW = hasMenu ? 24f : 0f;
        float splitGap = hasMenu ? 4f : 0f;
        return Mathf.Max(minClick * 1.5f, labelW + (pad * 2f) + arrowW + splitGap);
    }

    private static string ResolveTooltip(Command cmd)
    {
        if (cmd == null)
        {
            return null;
        }

        if (cmd.Disabled && !string.IsNullOrEmpty(cmd.DisabledReason))
        {
            return cmd.DisabledReason;
        }

        if (cmd.RepeatLast && !string.IsNullOrEmpty(cmd.RememberKey))
        {
            RememberedAction remembered;
            if (RememberedActionsByKey.TryGetValue(cmd.RememberKey, out remembered) && remembered != null && !string.IsNullOrEmpty(remembered.Label))
            {
                return string.IsNullOrEmpty(cmd.Tooltip) ? ("Repeat: " + remembered.Label) : (cmd.Tooltip + "\nLast: " + remembered.Label);
            }
        }

        return cmd.Tooltip;
    }
    /// <summary>
    /// Clears remembered repeat-last actions so command state does not leak across game/load boundaries.
    /// </summary>
    public static void ResetRuntimeState()
    {
        RememberedActionsByKey.Clear();
    }

    public static void ClearRemembered()
    {
        ResetRuntimeState();
    }

}
