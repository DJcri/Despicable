# UI Framework Rules of the Road

These rules keep layouts stable (no flicker, no overlap surprises) and keep overlays/validation meaningful.

## Documentation upkeep (required)

- If you change **UIFramework** code (widgets, fields, layout helpers, blueprints, styles), you must update:
  - `Docs/UIFramework_RulesOfTheRoad.md`
  - `Docs/UIFramework_Cookbook.md`
  - and the `UIFramework conventions` section in `Docs/Consistency-Charter.md` when relevant.
- Keep docs in sync with behavior: if a helper’s default rendering/interaction changes, update the recipe that demonstrates it.

## Reusable UI patterns (blueprints and helpers)

- If you introduce a UI element/layout pattern that is:
  - used in **2+ places**, or
  - likely to be reused soon (dialogs, ITabs, inspectors, lists, transfer rows),
  prefer creating a **UIFramework blueprint/helper** instead of copy-pasting.
- Keep these helpers **additive and side-effect free**:
  - measure pass must remain pure
  - do not mutate global UI state
  - do not silently change unrelated styles/layout outside the provided rects
- If a pattern is truly one-off, keep it local and don’t over-framework it.


## Measure vs Draw
- **Measure pass**: compute sizes, allocate rects, update scroll content heights.
  - Avoid opening float menus, changing game state, or doing expensive allocations.
- **Draw pass**: draw widgets, accept input, open dialogs/menus.

## Allocation
- Prefer **`VStack`** / **`HRow`** / **`HFlow`** for layout.
- When a screen has a clear space budget (header, tabs, toolbar, body, footer), carve from one remainder rect with **`RectTake.TakeTop/Bottom/Left/Right(...)`** instead of rebuilding positions with hardcoded offsets.
- Prefer content-driven header/body sizing. Use framework measurement, style line heights, and returned shell rects before introducing fixed fit numbers.
- `Style.Pad` is a broad default, not a license to accumulate invisible whitespace. If a blueprint window needs a different body inset, use **`BodyPadX` / `BodyPadY`** or per-edge body insets like **`BodyTopPadY` / `BodyBottomPadY`** on the style instead of page-local offset math.
- Do not “fix” overlap or whitespace by nudging rects with ad hoc constants. If a hardcoded fit value truly appears unavoidable, document why and notify the user before landing it.
- Prefer **`VStack.NextRow()`** for controls (checkboxes, dropdowns, sliders).
- For paragraphs: use **`VStack.NextTextBlock(...)`**. It measures deterministically and records metadata so validation can catch under-allocation.

## Drawing
- Prefer **`D2Widgets`** wrappers so rects are recorded consistently.
- Icon-only micro-actions (`x`, `+`, `-`, dropdown arrows, sort arrows) must use **`D2Widgets.ButtonIcon(...)`** or **`D2Widgets.ButtonIconInvisible(...)`** rather than `ButtonText(...)`.
- Prefer **`D2VanillaTex`** for vanilla widget textures instead of scattering raw texture paths in feature code.
- Use opt-in vanilla helpers such as **`D2Fields.SearchBoxVanilla(...)`**, **`IntStepperVanilla(...)`**, **`FloatStepperVanilla(...)`**, **`D2Widgets.RadioButtonVanilla(...)`**, and **`D2Widgets.MenuButtonVanilla(...)`** when a page intentionally upgrades its chrome.
- When using attached vanilla tabs, prefer **`D2Tabs.VanillaAttachedTabBody(...)`** over page-local gap math. The panel owns the tabs: reserve **31 px per tab row** above the panel, let the tabs overlap the panel by the shared one-pixel join, and keep the content rect derived from the returned shell layout.
- For sortable tables, prefer **`D2Table.VisualOptions<T>`** for additive polish such as vanilla sort icons, row hover highlight, and selected-row highlight instead of concatenating sort glyphs into header labels.
- Prefer existing semantic helpers like **`D2MeterRow`** for repeated status summaries instead of re-implementing icon/value/bar rows in feature code.
- If you must draw manually (textures, custom highlights), call **`ctx.RecordRect(...)`** so overlays/validation can still see it.

## Text state
- Do not leave `Text.Font`, `Text.Anchor`, or `Text.WordWrap` changed.
  - Use a scope (`TextStateScope`) any time you change text state.

## Scopes and labels
- Labels are hierarchical (`ScopePath/ElementLabel`). Keep them stable.
- If you need extra context for debug, use the `meta` overload (`ctx.RecordRect(rect, tag, label, meta)`).

## Debug overlay tips
- If overlays "disappear": make sure DevMode is on and `D2UIDebugSettings.Enabled` is true.
- Use **IssuesOnly** most of the time.
- Switch to **AllRects** when debugging clipping/scroll bounds.

## Manual interaction menus
- Prefer `ManualMenuRequest` + `ManualMenuHost` for new manual-interaction menus instead of assembling ad hoc `FloatMenuOption` lists in feature code.
- Building/opening manual menus is a draw/input concern. Do not create or open menus during measure/layout passes.
- For disabled entries, keep the label readable and put the reason in `DisabledReason` or `Tooltip`.
- For UI-origin menus, keep `givesColonistOrders = false` unless the menu is intentionally acting like a map order picker.
- When a menu needs icons, revalidation, checkbox rows, or extra right-side widgets, prefer `ManualMenuOptionSpec` fields over page-local constructor spelunking.



- Use centralized semantic text colors for focused/pinned modifier text. Prefer `ctx.Style.PositiveTextColor` / `NegativeTextColor` over ad hoc per-screen color constants.
