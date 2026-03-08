# Despicable UI Framework Cookbook

This is a practical copy-paste guide for building RimWorld UI with the Despicable UI Framework.

Principle: **allocate first (Measure-safe), draw second (Draw pass)**.

Vanilla-style changes are opt-in. Existing pages keep their current look unless they explicitly switch to helpers such as `D2Tabs.VanillaTabStrip(...)` or `D2Widgets.ButtonIcon(...)`.

## Maintenance rules (required)

- This cookbook is part of the UI Framework contract. If you change UIFramework behavior or add new primitives, **update this file** and `UIFramework_RulesOfTheRoad.md`.
- When you find yourself copy-pasting the same UI pattern, consider adding a small **blueprint/helper** to UIFramework (additive, measure-safe) and then update the cookbook with the new recipe.


---

## Recipe: Window with header, scroll body, footer

```cs
public override void DoWindowContents(Rect inRect)
{
    var ctx = UIContext.ForWindow("MyWindow", inRect, style: D2UIStyle.Default);

    var bp = new D2WindowBlueprint(ctx);
    bp.Begin(inRect);

    // Header
    bp.Header(v => {
        v.NextLine(UIRectTag.Header, "Title");
        D2Widgets.Label(ctx, v.NextLine(), "My Window");
    });

    // Body (scroll)
    bp.BodyScroll(v => {
        v.NextTextBlock(ctx, "Long paragraph that can wrap and localize safely...", GameFont.Small, padding: 2f, label: "Intro");
        v.NextBulletList(ctx, new []{
            (TaggedString)"Bullet 1",
            (TaggedString)"Bullet 2",
        });
    });

    // Footer
    bp.Footer(v => {
        var row = ctx.HRow(v.NextButton(), tag: UIRectTag.Footer, label: "Buttons");
        if (D2Widgets.ButtonText(ctx, row.NextFixed(120f), "OK")) Close();
        if (D2Widgets.ButtonText(ctx, row.NextFixed(120f), "Cancel")) Close();
    });

    bp.End();
}
```

---

## Recipe: ITab with header + searchable list + details

```cs
public class ITab_MyTab : ITab
{
    private Vector2 _scroll;
    private string _search = "";
    private int _sel = -1;

    protected override void FillTab()
    {
        Rect r = new Rect(0f, 0f, size.x, size.y);
        var ctx = UIContext.ForITab("MyTab", r, style: D2UIStyle.Default);

        var bp = new D2ITabBlueprint(ctx);
        bp.Begin(r);

        bp.Header(v => {
            D2Widgets.Label(ctx, v.NextLine(), "My Tab");
        });

        bp.BodyScroll(ref _scroll, v => {
            v.NextRow(UIRectTag.Control_Search, "Search");
            _search = D2Fields.SearchBox(ctx, v.NextRow(UIRectTag.Control_Search, "SearchRow"), _search, "Filter...");

            // List panel
            Rect listRect = v.Next(180f, UIRectTag.Panel, "ListPanel");
            var items = GetItemsFiltered(_search);
            _sel = D2ListView.Draw(ctx, listRect, items.Count, rowIndex => {
                var it = items[rowIndex];
                D2Widgets.LabelClipped(ctx, ctx.HRow(listRect).NextFill(), it.Label);
            }, selectedIndex: _sel);

            // Details
            if (_sel >= 0 && _sel < items.Count)
            {
                v.NextTextBlock(ctx, items[_sel].Description, GameFont.Small, padding: 2f, label: "Details");
            }
        });

        bp.End();
    }
}
```

---

## Recipe: Selector row (single-select)

```cs
var row = ctx.HRow(v.NextRow(UIRectTag.Control_Selector, "Mode"), tag: UIRectTag.Control_Selector, label: "ModeRow");

if (D2Widgets.SelectorButton(ctx, row.NextFill(), "Recent", selected: mode == 0)) mode = 0;
if (D2Widgets.SelectorButton(ctx, row.NextFill(), "High",   selected: mode == 1)) mode = 1;
if (D2Widgets.SelectorButton(ctx, row.NextFill(), "Low",    selected: mode == 2)) mode = 2;
```

---

## Recipe: Searchable FloatMenu

```cs
var opts = new List<D2FloatMenuBlueprint.Option>();
for (int i = 0; i < 30; i++)
    opts.Add(new D2FloatMenuBlueprint.Option($"Option {i}", action: () => Log.Message($"Picked {i}")));

D2FloatMenuBlueprint.Open(opts, searchable: true, title: "Pick one");
```

---


## Recipe: Manual interaction menus with vanilla FloatMenu behavior

```cs
var request = new ManualMenuRequest("ManualInteraction/Social")
{
    GivesColonistOrders = false,
    VanishIfMouseDistant = false
};

request.Options.Add(new ManualMenuOptionSpec
{
    Label = "Chat",
    Action = () => StartChat(targetPawn),
    Priority = MenuOptionPriority.High,
    RevalidateClickTarget = targetPawn
});

request.Options.Add(ManualMenuOptionSpec.DisabledOption(
    "Marriage",
    disabledReason: "Requires lover or fiance status"));

ManualMenuHost.Open(request);
```

Use `ManualMenuRequest` + `ManualMenuOptionSpec` when a manual interaction menu wants real vanilla `FloatMenu` features instead of custom mimicry. The builder maps specs to the best available vanilla constructor and falls back gracefully if a specific overload is unavailable.

Patterns to prefer:
- Disabled options: keep the label clean and put the reason in `DisabledReason` / `Tooltip`.
- Icons: set one of `ShownItemForIcon`, `IconThing`, or `IconTex`.
- Revalidation: set `RevalidateClickTarget` or `RevalidateWorldClickTarget` for long-lived menus.
- UI-origin menus: leave `GivesColonistOrders = false` and usually `VanishIfMouseDistant = false`.

---

## Recipe: `D2Table` with opt-in vanilla sort icons

```cs
var columns = new List<D2Table.Column<MyRow>>
{
    new D2Table.Column<MyRow>("name", "Name", row => row.Label)
    {
        SortValue = row => row.Label,
        Tooltip = "Pawn or entry label"
    },
    new D2Table.Column<MyRow>("value", "Value", row => row.Value.ToString("0"))
    {
        SortValue = row => row.Value
    }
};

var visuals = new D2Table.VisualOptions<MyRow>
{
    UseVanillaSortIcons = true,
    HighlightRowOnHover = true,
    IsRowSelected = row => ReferenceEquals(row, selectedRow)
};

D2Table.Draw(ctx, tableRect, rows, columns, ref tableState, label: "MyTable", visualOptions: visuals);
```

Notes:
- `UseVanillaSortIcons = true` keeps the header label clean and draws `Sorting` / `SortingDescending` on the right edge instead of concatenating glyphs into the translated string.
- `HighlightRowOnHover` and `IsRowSelected` are optional and off by default. Existing tables keep their previous look unless a caller opts in.
- `rowHeightOverride` still exists for exceptional cases, but the default path already uses `ctx.Style.RowH`.

---

## Recipe: Meter row (icon + value + bar)

```cs
Rect row = v.Next(ctx.Style.RowHeight, UIRectTag.Input, "Status/KarmaRow");
D2MeterRow.Draw(
    ctx,
    row,
    HKUIConstants.KarmaIcon,
    karma,
    HKRuntime.KarmaMin,
    HKRuntime.KarmaMax,
    "Status/Karma",
    tooltip: "Cosmic Karma\nValue: " + karma,
    labelText: "Karma",
    labelWidth: 56f,
    valueWidth: 48f);
```

Use this for repeated status summaries instead of open-coding `GUI.DrawTexture + Widgets.Label + Widgets.FillableBar` in each screen.

---

## Recipe: Gizmo grid panel

```cs
Rect panel = v.Next(140f, UIRectTag.Panel, "Gizmos");
var grid = D2Grid.Simple(panel, cols: 3, rows: 2, gap: ctx.Style.Gap);

for (int i = 0; i < 6; i++)
{
    var spec = new D2GizmoBlueprint.GizmoSpec
    {
        Label = $"G{i}",
        Tooltip = "Demo gizmo",
        Disabled = (i == 4),
        DisabledReason = (i == 4) ? "Disabled for demo" : null,
        Selected = (selected == i),
        Hotkey = (i == 0) ? "F" : null,
        Icon = TexButton.Ok
    };

    if (D2GizmoBlueprint.DrawGizmo(ctx, grid.Cell(i), spec)) selected = i;
}
```



- `D2TextPairRow`: use for left/right clipped text rows with shared hover, hitbox, selection, and tooltip behavior.
- `D2Widgets.LabelClippedAligned(...)`: use when a clipped label must preserve left/center/right alignment instead of defaulting to upper-left.
- `D2IconTextRow`: use for compact icon + text legend/help rows with shared tooltip/hitbox behavior.
- `D2IconTile`: use for icon tiles with hover fill, border, fitted icon, and tooltip hotspot before adding feature-specific click handling.
- `D2Widgets.DrawTextureFitted(...)` and `D2Widgets.DrawBox(...)`: prefer these wrappers over raw `GUI.DrawTexture(...)` / `Widgets.DrawBox(...)` when a framework-visible rect should be recorded.


---

## Recipe: Carve a layout from one remainder rect

```cs
Rect remaining = inRect;
Rect header = RectTake.TakeTop(ref remaining, ctx.Style.HeaderHeight);
Rect tabs = RectTake.TakeTop(ref remaining, ctx.Style.RowH);
Rect footer = RectTake.TakeBottom(ref remaining, ctx.Style.FooterHeight);
Rect body = remaining;

D2Widgets.Label(ctx, header, "My Window");
selectedTab = D2Tabs.VanillaTabStrip(ctx, tabs, selectedTab, tabLabels, "MyWindow/Tabs");
```

Use this when a screen has a clear header/tabs/body/footer budget. It keeps rect math local and avoids layout drift from hardcoded offsets.

If a blueprint window needs tighter or looser body insets, prefer style overrides such as `BodyPadY = 0f` or a per-edge inset like `BodyTopPadY = 6f` over page-local `y += 10f` / `height -= 10f` nudges.

---

## Recipe: Let a blueprint header size from content

```cs
private static readonly D2UIStyle _style = new D2UIStyle
{
    Pad = 10f,
    HeaderHeight = 0f,
    BodyPadY = 0f,
    BodyTopPadY = 6f, // optional breathing room above the body without symmetric bottom padding
};

protected override float GetHeaderContentHeight(Rect outRect)
    => HeaderTopRowHeight + HeaderGap + HeaderBottomRowHeight;
```

Use this when the header should fit its actual rows instead of preserving an older hardcoded budget. Keep text lanes on framework heights such as `ctx.Style.Line` so validation and measurement agree.

---

## Recipe: Icon-only vanilla micro-actions

```cs
Rect row = v.Next(ctx.Style.RowH, UIRectTag.Input, "SearchRow");
var h = new HRow(ctx, row);
Rect fieldRect = h.NextFill();
Rect clearRect = h.NextFixed(ctx.Style.MinClickSize);

_search = D2Widgets.TextField(ctx, fieldRect, _search, label: "SearchRow/Field");
if (!string.IsNullOrEmpty(_search)
    && D2Widgets.ButtonIcon(ctx, clearRect, D2VanillaTex.CloseXSmall, tooltip: "Clear", label: "SearchRow/Clear"))
{
    _search = string.Empty;
}
```

Rule of thumb: once the label disappears, switch to `ButtonIcon(...)` and give the control a tooltip.

---

## Recipe: Opt-in vanilla tabs

```cs
string[] tabs = { "Overview", "Details", "History" };
_selectedTab = D2Tabs.VanillaTabStrip(ctx, tabRect, _selectedTab, tabs, "Demo/Tabs");
```

Use this when a page wants vanilla `TabDrawer` visuals without changing older selector-based tab strips elsewhere.

---

## Recipe: Attached vanilla tabs with a shared panel

```cs
var shell = D2Tabs.VanillaAttachedTabBody(
    ctx,
    rect,
    ref _selectedTab,
    tabLabels,
    "Demo/PrimaryTabs",
    innerPad: 10f,
    minOverflowTabWidth: 120f,
    maxTabWidth: D2Tabs.VanillaTabMaxWidth);

Rect contentRect = shell.InnerRect;
```

Use this when the tabs should feel like vanilla folder tabs that belong to the controlled panel. The helper treats the provided rect as a single shell, reserves **31 px per tab row above the panel**, draws `Widgets.DrawMenuSection(...)` for the shared body, and keeps the one-pixel visual join that makes the tabs feel attached.

---

## Recipe: Vanilla texture registry

```cs
Texture2D plusTex = D2VanillaTex.Plus;
Texture2D minusTex = D2VanillaTex.Minus;
Texture2D sortTex = D2VanillaTex.SortingDescending;
```

Keep vanilla texture lookups in `D2VanillaTex` so feature code stays discoverable and translation-safe.


---

## Recipe: Opt-in vanilla search row

```cs
D2Fields.SearchBoxVanilla(
    ctx,
    searchRect,
    ref _search,
    placeholder: "Search...",
    tooltip: "Filter the list",
    showSearchIcon: true,
    label: "Browser/Search");
```

Use this when a page wants the vanilla-ish `[magnifier][field][clear x]` pattern without changing older search rows elsewhere.

---

## Recipe: Vanilla steppers without changing rect math

```cs
D2Fields.IntStepperVanilla(ctx, amountRect, ref _amount, step: 1, min: 0, label: "Edit/Amount");
D2Fields.FloatStepperVanilla(ctx, weightRect, ref _weight, step: 0.1f, min: 0f, label: "Edit/Weight");
```

These keep the same left / field / right rect split as the existing stepper helpers and only swap the click primitive to icon-only vanilla buttons.

---

## Recipe: Vanilla radio and dropdown variants

```cs
if (D2Widgets.RadioButtonVanilla(ctx, radioRect, _mode == Mode.Advanced, "Advanced", label: "Settings/Advanced"))
    _mode = Mode.Advanced;

D2Fields.EnumDropdownVanilla(ctx, modeRect, _mode, v => _mode = v, tooltip: "Choose a mode", label: "Settings/Mode");
```

Prefer these opt-in variants when you want vanilla icon treatment but need to keep older pages visually unchanged by default.
