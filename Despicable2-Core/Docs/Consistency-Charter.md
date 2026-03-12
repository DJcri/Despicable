# Despicable2 consistency charter

This file defines the house style for Despicable2. Every cleanup pass should move code toward these rules instead of inventing one-off local conventions.

## 1. Naming

- Types use PascalCase and should describe a concrete role.
- Methods use PascalCase verbs: `Register`, `Resolve`, `Build`, `ResetRuntimeState`, `TryX`.
- Booleans read like questions when practical: `IsActive`, `HasChannel`, `CanRender`, `ShouldProcess`.
- Private fields use `_camelCase` only when they represent long-lived object state or one-time bootstrap flags.
- Private static readonly collections use descriptive `camelCase` names that explain their contents, not vague nouns.
- Avoid duplicate source-of-truth names. If a value is a setting, the setting name wins everywhere.

## 2. State ownership

- Fixed defaults are `const` or exposed through read-only properties.
- User-tunable values live in `Settings` and are read through accessors.
- Per-pawn or per-instance runtime state belongs to the owning component.
- Cross-system ephemeral runtime caches must expose `ResetRuntimeState()` and be cleared from a central reset hub.
- Thread-local patch guards may use `[ThreadStatic]` when they are strictly re-entrancy protection.
- New mutable global gameplay state is not allowed unless there is a documented lifecycle and reset path.

## 3. File and folder layout

## 3.1. File size guardrails

- Files over 300 lines are on the refactor watchlist and should not keep growing without a good reason.
- Files over 400 lines should only stay whole when there is a clear, documented ownership boundary.
- Files over 500 lines are mandatory cleanup candidates and should be split once there is a safe seam to do it.
- Dialog, patch, and utility files should prefer thin orchestration plus focused helpers over one giant mixed-responsibility file.

- Core code lives under `Core/` and should prefer these buckets when relevant:
  - `Bootstrap/`
  - `Compatibility/`
  - `Constants/`
  - `Hooks/`
  - `Identity/`
  - `Model/`
  - `Runtime/`
  - `Staging/`
- Runtime cache owners belong in `Runtime/` when they coordinate multiple systems.
- Patch helpers stay next to the patch unless they become shared across multiple patches.

## 4. Runtime reset convention

- The canonical method name for clearing ephemeral global state is `ResetRuntimeState()`.
- Legacy `Clear()` or `ClearAll()` methods may remain as temporary compatibility wrappers, but new code should call `ResetRuntimeState()`.
- `DespicableRuntimeState` is the single orchestration hub for cross-system reset on game load/new game.


## 4.1. AnimModule runtime holders

- AnimModule runtime caches, warning registries, and preview contexts should also expose `ResetRuntimeState()`.
- Patch-owned caches may keep older names like `ClearCache()` as wrappers, but the canonical call site should migrate to `ResetRuntimeState()`.
- Warn-once registries and deferred-startup queues should use explicit pluralized names that describe their keys, and they should expose `ResetRuntimeState()` when their contents are ephemeral.
- Diagnostic warn-once registries should describe the key they store in the field name, not generic names like `warned`.
- Workshop preview context state should stay encapsulated behind `WorkshopRenderContext` and reset through its owner, not via direct thread-static field access.


## 4.2. FacePartsModule conventions

- Use `ModMain.IsNlFacialInstalled` as the readable compat gate instead of the legacy lowercase alias in new edits.
- Face-part settings refresh should flow through component helpers (for example `RefreshEnabledFromSettings()`), not by scattering direct settings reads across UI and render code.
- Initialization helpers should use descriptive verbs such as `InitializeFacePartState()`; older `Try...` names may remain as delegating wrappers during migration.
- Compatibility patches that cache reflected members should name the cached field after the reflected type (for example `pawnFieldInfo`) and expose `ResetRuntimeState()` when the cache can be safely cleared.

## 4.3. HeroKarmaModule conventions

- HeroKarma runtime holders should expose `ResetRuntimeState()` as the canonical lifecycle method; older `Clear...` names may remain as wrappers during migration.
- Diagnostics sinks should keep mutable backing state private and expose read-only accessors for surfaced session details.
- Short-lived attribution helpers such as goodwill and gift contexts should reset through their owning type instead of being cleared by scattered field writes.
- Patch-local caches and debounce registries should prefer descriptive collection names that explain the interaction key they store.


## 4.4. UIFramework conventions


- Any change to UIFramework behavior or public surface area requires updating the UIFramework docs:
  - `Docs/UIFramework_RulesOfTheRoad.md`
  - `Docs/UIFramework_Cookbook.md`
- Prefer adding reusable UI patterns as small, measure-safe helpers/blueprints instead of copy-pasting, when the pattern is used in 2+ places.
- New vanilla-style micro-actions should prefer `D2Widgets.ButtonIcon(...)` / `ButtonIconInvisible(...)` plus `D2VanillaTex`, not tiny `ButtonText(...)` labels.
- Pages that want vanilla folder-tab attachment should prefer `D2Tabs.VanillaAttachedTabBody(...)` instead of hand-placing a tab strip above a nearby panel. Keep the panel as the owner of the tab chrome and let the helper handle multi-row reservation.
- New header/body/footer layouts should prefer remainder-rect carving (`D2RectTake`) over reconstructing positions with hardcoded offsets.
- Prefer framework-measured header/body sizing and style tokens over hardcoded fit numbers. If a layout needs a different body inset, use a style-level override such as `BodyPadX` / `BodyPadY` or per-edge body insets like `BodyTopPadY` / `BodyBottomPadY` instead of page-local rect nudges.
- If a layout fix seems to require hardcoded positioning/sizing, document the constraint first and do not land it silently.
- Repeated status summaries should prefer shared helpers such as `D2MeterRow` instead of re-implementing icon/value/bar compositions in feature modules.
- UIFramework runtime holders should expose `ResetRuntimeState()` as the canonical lifecycle method; older reset helpers may remain as wrappers during migration.
- Private static caches should describe both the value and the key they store (for example `rememberedActionsByKey`, `textBuffersByKey`, `lastLogTicksByWindowName`).
- Small UI helper methods should use descriptive verb phrases such as `BuildBufferKey()` instead of vague names like `Key()`.
- Text and widget forwarders should keep XML comments and indentation clean, because these files are the most frequently scanned utility layer in the project.
- Manual interaction menus should prefer the shared `ManualMenuRequest` / `ManualMenuHost` path so disabled reasons, icons, and revalidation stay consistent across Core and NSFW migrations.

## 4.5. NSFW and integration conventions

- NSFW-side ephemeral runtime holders should expose `ResetRuntimeState()` as the canonical lifecycle method, even when the public bridge keeps older verbs such as `Clear(...)` for pair-specific operations.
- Small bridge helpers should name cached runtime owners descriptively (for example `runtimeState`) and prefer verb phrases like `BuildPairKey(...)` over generic names such as `Key(...)`.
- Optional integration reflection caches should keep mutable cache state encapsulated behind a single owner and expose `ResetRuntimeState()` when the cache can be safely rebuilt.
- NSFW load-boundary cleanup should flow through a dedicated NSFW reset hub instead of relying on Core to know about NSFW-owned caches.

## 5. Class layout

Recommended order inside a class:

1. constants / static readonly fields
2. mutable fields
3. properties
4. constructors / static constructors
5. public methods
6. protected methods
7. private helpers
8. nested types

Keep public API above private helpers so file scanning stays predictable.

## 6. Comments and formatting

- Comments should explain intent, invariants, or engine-specific traps.
- Do not leave roadmap notes or speculative TODO lists in production-facing code files unless they directly guide the local file.
- Prefer short XML summaries on shared utility types and runtime state holders.
- Use brace and spacing style already established in the cleaned Core files.

## 7. Migration policy

- During cleanup passes, additive compatibility shims are acceptable when they reduce churn.
- When renaming an API for consistency, keep a delegating wrapper temporarily if many files still depend on the old name.
- Each subsystem pass should leave the codebase more uniform than it started, even if some legacy names remain until a later chunk.

## Analyzer checkpoint

- Treat naming drift, unused usings, unused private members, broad catches, and missed `readonly` opportunities as review-time fixes, not backlog decoration.
- Raise new analyzer warnings during cleanup passes, then decide separately when they are stable enough to become build-blocking in release workflows.


- `D2TextPairRow`: use for left/right clipped text rows with shared hover, hitbox, selection, and tooltip behavior.
- `D2Widgets.LabelClippedAligned(...)`: use when a clipped label must preserve left/center/right alignment instead of defaulting to upper-left.
- `D2IconTextRow`: use for compact icon + text legend/help rows with shared tooltip/hitbox behavior.
- `D2IconTile`: use for icon tiles with hover fill, border, fitted icon, and tooltip hotspot before adding feature-specific click handling.
- `D2Widgets.DrawTextureFitted(...)` and `D2Widgets.DrawBox(...)`: prefer these wrappers over raw `GUI.DrawTexture(...)` / `Widgets.DrawBox(...)` when a framework-visible rect should be recorded.

- When upgrading a page toward vanilla visuals, prefer the opt-in framework variants (`SearchBoxVanilla`, `IntStepperVanilla`, `FloatStepperVanilla`, `RadioButtonVanilla`, `MenuButtonVanilla`) before inventing page-local copies of the same patterns.
- For sortable tables, keep translated header labels clean and draw sort state as icons through `D2Table.VisualOptions<T>` instead of baking `▲/▼` into strings.


- Focused/pinned modifier values should use centralized semantic text colors from the UI style (`PositiveTextColor`, `NegativeTextColor`) instead of page-local color literals.
