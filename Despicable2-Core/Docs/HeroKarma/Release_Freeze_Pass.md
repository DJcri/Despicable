# HeroKarma Release Freeze Pass

This pass is the conservative stabilization layer before release. It deliberately avoids new mechanics and instead locks down naming, compatibility intent, and release expectations.

## Goals

- keep one canonical live event name for captive sale: `SellCaptive`
- preserve a narrow legacy adapter for `SellPrisoner` where old settings or stale references may still surface
- reduce drift by routing alias handling through one shared helper instead of repeating two-name switch branches everywhere
- leave a release-ready paper trail for what is intentionally custom, intentionally compatible, and intentionally out of scope

## What changed in this freeze pass

- added canonical event-key helpers to `HKSettingsUtil`
- updated core Karma, Reputation, Standing, and settlement-local paths to canonicalize event keys before switching on them
- kept explicit legacy alias support only where it is still useful:
  - settings/dev hook lookup
  - tuning constant aliases
  - event-display alias entry for older data
  - validation docs that mention rename compatibility

## Intentional legacy shims kept for release

These are **intentional**, not forgotten leftovers:

- `SellPrisoner` tuning aliases in `HKBalanceTuning`
- `SellPrisoner` display alias in `EventDisplayCatalog`
- old serialized dev-setting key support for captive-sale hook toggles
- validation notes that call out the rename and expected behavior

## What should be considered canonical now

- event key: `SellCaptive`
- UI label: `Sold captive`
- hook patch: `HarmonyPatch_SellCaptive`
- coercion/slavery settlement write: explicit, single-hit, non-duplicating

## Freeze criteria

A release candidate is ready to freeze when all of the following are true:

1. HeroKarma compiles cleanly.
2. No-Ideology mode hides Standing cleanly while Karma and Reputation remain usable.
3. `EnslaveAttempt` and `SellCaptive` each write exactly one explicit settlement-local delta on their intended path.
4. The ideology trace explains the exact-precept and first-wave meme cases without contradiction.
5. UIFramework-native controls are used wherever a framework-native equivalent now exists, and the remaining custom draw pockets are deliberate.
6. New live captive-sale events emit `SellCaptive`, not `SellPrisoner`.

## Out of scope for the freeze

- broad new ideology families
- new event categories
- save-data migrations that rewrite historic event keys
- aggressive deletion of all legacy aliases before release validation is complete
