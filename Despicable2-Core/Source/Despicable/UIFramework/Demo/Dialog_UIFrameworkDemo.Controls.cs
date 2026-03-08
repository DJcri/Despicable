using System.Collections.Generic;
using UnityEngine;
using RimWorld;
using Verse;
using Despicable.UIFramework;
using Despicable.UIFramework.Blueprints;
using Despicable.UIFramework.Layout;
using Despicable.UIFramework.Controls;
using Despicable;

namespace Despicable.UIFramework.Demo;
// Guardrail-Reason: Demo control showcase stays grouped for easier browsing of the sample surface.
public sealed partial class Dialog_UIFrameworkDemo
{
    private void DrawControlsColumn(UIContext ctx, Rect rect)
    {
        using (var gp = ctx.GroupPanel("Controls", rect, soft: true, pad: true))
        {
            var v = ctx.VStack(gp.Inner);

            DrawSectionTitle(ctx, D2LayoutHelpers.NextSectionHeader(ctx, ref v, "Controls/Title"), "Controls");

            // Scenario
            Rect scenRow = v.NextRow(UIRectTag.Input, "Controls/ScenarioRow");
            var hSc = new HRow(ctx, scenRow);
            D2Widgets.Label(ctx, hSc.NextFixed(90f), "Scenario", "Controls/ScenarioLabel");
            D2Widgets.DropdownButton(ctx, hSc.Remaining(), _scenario.ToString(), BuildScenarioOptions(), "Controls/Scenario");

            v.NextDivider(1f, "Controls/Div1");

            DrawSectionTitle(ctx, D2LayoutHelpers.NextSectionHeader(ctx, ref v, "Controls/CoreTitle"), "Core widgets");

            bool enabled = _enabled;
            D2Widgets.CheckboxLabeled(ctx, v.NextRow(UIRectTag.Input, "Controls/Enabled"), "Enabled", ref enabled, "Controls/EnabledToggle");
            _enabled = enabled;

            Rect intensityRow = v.NextRow(UIRectTag.Input, "Controls/IntensityRow");
            var hInt = new HRow(ctx, intensityRow);
            D2Widgets.Label(ctx, hInt.NextFixed(90f), "Intensity", "Controls/IntensityLabel");
            _intensity = D2Widgets.HorizontalSlider(ctx, hInt.Remaining(), _intensity, 0f, 100f, showValueLabel: true, label: "Controls/IntensitySlider");

            Rect filterRow = v.NextRow(UIRectTag.Input, "Controls/FilterRow");
            var hFil = new HRow(ctx, filterRow);
            D2Widgets.Label(ctx, hFil.NextFixed(90f), "Filter", "Controls/FilterLabel");
            string newFilter = D2Widgets.TextField(ctx, hFil.Remaining(), _filter ?? string.Empty, 64, "Controls/FilterField");
            if (newFilter != _filter)
            {
                _filter = newFilter;
                _selectedIndex = -1;
            }

            Rect modeRow = v.NextRow(UIRectTag.Input, "Controls/ModeRow");
            var hMode = new HRow(ctx, modeRow);
            D2Widgets.Label(ctx, hMode.NextFixed(90f), "Mode", "Controls/ModeLabel");
            D2Widgets.DropdownButton(ctx, hMode.Remaining(), _mode.ToString(), BuildModeOptions(), "Controls/Mode");

            Rect sizeRow = v.NextRow(UIRectTag.Input, "Controls/SizeRow");
            var hSize = new HRow(ctx, sizeRow);
            D2Widgets.Label(ctx, hSize.NextFixed(90f), "Size", "Controls/SizeLabel");
            DrawSizeSelector(ctx, hSize.Remaining(), "Controls/SizeSelector");

            v.NextDivider(1f, "Controls/Div_Menu");

            DrawSectionTitle(ctx, D2LayoutHelpers.NextSectionHeader(ctx, ref v, "Controls/MenuTitle"), "Menus");
            Rect menuRow = v.NextRow(UIRectTag.Input, "Controls/OpenSearchMenuRow");
            if (D2Widgets.ButtonText(ctx, menuRow, "Open searchable menu", "Controls/OpenSearchMenu"))
            {
                D2FloatMenuBlueprint.Open(BuildSearchMenuOptions(), searchable: true, title: "UI Framework Menu", searchableThreshold: 12);
            }

            Rect cmdTitle = D2LayoutHelpers.NextSectionHeader(ctx, ref v, "Controls/CommandTitle");
            DrawSectionTitle(ctx, cmdTitle, "Command bar");
            Rect cmdRow = v.Next(D2CommandBar.MeasureHeight(ctx), UIRectTag.Input, "Controls/CommandRow");
            var cmds = new List<D2CommandBar.Command>
            {
                new D2CommandBar.Command("Apply", "Apply", () => _commandLog = "Apply") { Tooltip = "Run the primary action.", RememberKey = "DemoAction" },
                new D2CommandBar.Command("Randomize", "Randomize", () => _commandLog = "Randomize")
                {
                    Tooltip = "Run or choose a randomization flavor.",
                    RememberKey = "DemoAction",
                    MenuOptions = new List<D2FloatMenuBlueprint.Option>
                    {
                        new D2FloatMenuBlueprint.Option("Randomize: Soft", () => _commandLog = "Randomize: Soft"),
                        new D2FloatMenuBlueprint.Option("Randomize: Loud", () => _commandLog = "Randomize: Loud"),
                    }
                },
                new D2CommandBar.Command("Repeat", "Repeat Last")
                {
                    Tooltip = "Replay the last remembered command.",
                    RememberKey = "DemoAction",
                    RepeatLast = true,
                    Disabled = !D2CommandBar.HasRemembered("DemoAction"),
                    DisabledReason = "No remembered command yet."
                }
            };
            D2CommandBar.Draw(ctx, cmdRow, cmds, "Controls/CommandBar");
            D2Widgets.LabelClipped(ctx, v.NextRow(UIRectTag.Label, "Controls/CommandLog"), "Last: " + _commandLog, "Controls/CommandLogLabel");

            v.NextDivider(1f, "Controls/Div2");

            DrawSectionTitle(ctx, D2LayoutHelpers.NextSectionHeader(ctx, ref v, "Controls/IconTitle"), "Icons");
            Rect iconRow = v.NextRow(UIRectTag.None, "Controls/IconRow");
            DrawIconRow(ctx, iconRow);

            v.NextDivider(1f, "Controls/Div3");

            bool sandbox = _overlapSandbox;
            D2Widgets.CheckboxLabeled(ctx, v.NextRow(UIRectTag.Input, "Controls/OverlapToggle"), "Enable overlap sandbox", ref sandbox, "Controls/OverlapSandboxToggle");
            _overlapSandbox = sandbox;

            if (_overlapSandbox)
            {
                Rect sandSlot = v.Next(ctx.Style.RowHeight * 2.0f, UIRectTag.PanelSoft, "Controls/OverlapSandboxSlot");
                using (var sand = ctx.GroupPanel("OverlapSandbox", sandSlot, soft: true, pad: true))
                {
                    Rect o = sand.Inner;
                    // Deliberate overlap: two widgets on the same rect.
                    D2Widgets.ButtonText(ctx, o, "Overlap A", "OverlapSandbox/ButtonA");
                    D2Widgets.ButtonText(ctx, o, "Overlap B", "OverlapSandbox/ButtonB");
                }
            }
        }
    }

    private void DrawSectionTitle(UIContext ctx, Rect rect, string title)
    {
        using (new TextStateScope(GameFont.Medium, TextAnchor.UpperLeft, true))
        {
            D2Widgets.LabelClipped(ctx, rect, title, "SectionTitle");
        }
    }

    private void DrawSizeSelector(UIContext ctx, Rect rect, string label)
    {
        var h = new HRow(ctx, rect);
        float w = Mathf.Max(42f, rect.width / 3f);

        bool s = _size == DemoSize.S;
        bool m = _size == DemoSize.M;
        bool l = _size == DemoSize.L;

        if (D2Widgets.RadioButton(ctx, h.NextFixed(w), s, "S", label + "/S")) _size = DemoSize.S;
        if (D2Widgets.RadioButton(ctx, h.NextFixed(w), m, "M", label + "/M")) _size = DemoSize.M;
        if (D2Widgets.RadioButton(ctx, h.Remaining(), l, "L", label + "/L")) _size = DemoSize.L;
    }

    private void DrawIconRow(UIContext ctx, Rect rect)
    {
        var h = new HRow(ctx, rect);

        Rect a = h.NextFixed(34f);
        Rect b = h.NextFixed(34f);
        Rect c = h.NextFixed(34f);
        Rect rest = h.Remaining();

        DrawMaybeIcon(ctx, a, TexIcons.HeroGizmo, "Icons/Valid");
        TooltipHandler.TipRegion(a, "Valid icon");

        DrawMaybeIcon(ctx, b, null, "Icons/Empty");
        TooltipHandler.TipRegion(b, "Empty icon (null texture)\nLayout must remain stable.");

        bool prev = GUI.enabled;
        GUI.enabled = false;
        DrawMaybeIcon(ctx, c, TexIcons.HeroGizmo, "Icons/Disabled");
        GUI.enabled = prev;
        TooltipHandler.TipRegion(c, "Disabled icon\nDisabled controls should still have tooltips.");

        D2Widgets.LabelClipped(ctx, rest, "(hover icons for tooltips)", "Icons/Hint");
    }

    private void DrawMaybeIcon(UIContext ctx, Rect rect, Texture2D tex, string label)
    {
        if (tex != null)
        {
            D2Widgets.ButtonImageFitted(ctx, rect, tex, null, label);
            return;
        }

        // Still record it as an icon rect for overlay purposes.
        ctx.Record(rect, UIRectTag.Icon, label + "/Icon");
        if (ctx.Pass == UIPass.Measure) return;

        using (new TextStateScope(GameFont.Small, TextAnchor.MiddleCenter, true))
        {
            Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.12f));
            Widgets.Label(rect, "∅");
        }
    }

    private void DrawChips(UIContext ctx, Rect rect, DemoItem item)
    {
        var h = new HFlow(ctx, rect, lineHeight: ctx.Style.RowHeight, gap: ctx.Style.Gap);

        string chipA = _scenario.ToString();
        string chipB = _mode.ToString();
        string chipC = _size.ToString();

        DrawChip(ctx, h.Next(CalcChipWidth(chipA)), chipA, "Chip/Scenario");
        DrawChip(ctx, h.Next(CalcChipWidth(chipB)), chipB, "Chip/Mode");
        DrawChip(ctx, h.Next(CalcChipWidth(chipC)), chipC, "Chip/Size");
        if (item.Disabled)
            DrawChip(ctx, h.Next(CalcChipWidth("Disabled")), "Disabled", "Chip/Disabled");
    }

    private void DrawChip(UIContext ctx, Rect rect, string text, string label)
    {
        ctx.Record(rect, UIRectTag.PanelSoft, label);
        if (ctx.Pass == UIPass.Measure) return;

        Widgets.DrawBoxSolid(rect, new Color(0f, 0f, 0f, 0.10f));
        using (new TextStateScope(GameFont.Small, TextAnchor.MiddleCenter, true))
        {
            Widgets.Label(rect, text);
        }
    }

    private static float CalcChipWidth(string text)
    {
        float w = Text.CalcSize(text ?? string.Empty).x;
        return Mathf.Clamp(w + 18f, 52f, 160f);
    }

    private void DrawBulletBlock(UIContext ctx, ref VStack v, string labelRoot)
    {
        var bullets = GetBullets();
        if (bullets == null || bullets.Count == 0) return;

        // Convert to TaggedString so we can reuse the same API we'd use in real localized screens.
        var tagged = new List<TaggedString>(bullets.Count);
        for (int i = 0; i < bullets.Count; i++)
            tagged.Add(bullets[i]);

        v.NextBulletList(
            ctx,
            tagged,
            GameFont.Small,
            padding: 0f,
            bulletIndent: 18f,
            bulletGap: 4f,
            tag: UIRectTag.Text_Bullet,
            labelPrefix: labelRoot + "/Bullet");
    }

    private void DrawGizmoGrid(UIContext ctx, Rect rect, string labelRoot)
    {
        // Simple 3x2 grid, sized to the panel.
        float gap = ctx.Style.Gap;
        var grid = D2Grid.Simple(rect, cols: 3, rows: 2, gap: gap);

        for (int r = 0; r < 2; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                int idx = (r * 3) + c;
                var cell = grid.Cell(r, c);

                bool disabled = (idx == 4);
                bool selected = (_gizmoSelected == idx);
                string hotkey = (idx == 0) ? "1" : (idx == 1 ? "2" : null);

                var spec = new D2GizmoBlueprint.GizmoSpec(
                    icon: TexIcons.HeroGizmo,
                    label: "Cmd " + (idx + 1),
                    action: () =>
                    {
                        _gizmoSelected = idx;
                        Messages.Message("Clicked gizmo " + (idx + 1), MessageTypeDefOf.NeutralEvent, false);
                    },
                    tooltip: "Demo gizmo " + (idx + 1),
                    disabled: disabled,
                    disabledReason: disabled ? "Disabled gizmo (demo)" : null,
                    selected: selected,
                    hotkeyLabel: hotkey);

                D2GizmoBlueprint.DrawGizmo(ctx, cell, spec);
            }
        }
    }


    private void DrawListRow(UIContext ctx, Rect row, DemoItem item, int idx, bool selected)
    {
        var inner = row.ContractedBy(6f, 2f);
        var h = new HRow(ctx, inner);

        Rect icon = h.NextFixed(22f);
        DrawMaybeIcon(ctx, icon, item.HasIcon ? TexIcons.HeroGizmo : null, "List/Row[" + idx + "]/Icon");

        Rect labelRect = h.Remaining();
        RectSplit.SplitVertical(labelRect, Mathf.Max(0f, labelRect.width - 34f), ctx.Style.Gap, out Rect labelOnly, out Rect trailing);

        if (item.Disabled)
        {
            bool prev = GUI.enabled;
            GUI.enabled = false;
            D2Widgets.LabelClipped(ctx, labelOnly, item.Label, "List/Row[" + idx + "]/Label");
            D2Widgets.ButtonText(ctx, trailing, "...", "List/Row[" + idx + "]/More");
            GUI.enabled = prev;
            TooltipHandler.TipRegion(row, "Disabled row (demo)\nDisabled controls should still have tooltips.");
        }
        else
        {
            D2Widgets.LabelClipped(ctx, labelOnly, item.Label, "List/Row[" + idx + "]/Label");
            D2Widgets.ButtonText(ctx, trailing, "...", "List/Row[" + idx + "]/More");
        }
    }

}
