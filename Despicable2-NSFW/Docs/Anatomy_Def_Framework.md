# Anatomy Def Framework

This pass moves logical anatomy from hardcoded penis/vagina booleans toward a generic resolved-part model.

## New core defs

- `AnatomyPartDef`
  - Canonical anatomy content def.
  - Owns slot, tags, capabilities, base render properties, and default neutral/aroused textures.
  - Can now define size generation bounds, optional default fluid templates, and optional size-to-texture buckets.
- `FluidDef`
  - Canonical fluid identity def, such as Milk or Semen.
- `AnatomySlotDef`
  - Declares anchor strategy and parent render tag.
- `AnatomyProfileDef`
  - Assigns anatomy parts to matching pawns with def selectors, including optional `geneDefs`.
- `AnatomyPartVariantDef`
  - Selects installed part variants, such as bionic or gene-specialized versions of an existing part, using selectors like `hediffDefs`, `geneDefs`, or `raceDefs`.
- `AnatomyAppearanceOverrideDef`
  - Swaps textures for matching pawns without new C#. Supports `variantDefs`, so installed variants can override gene and race art cleanly.
- `AnatomyPlacementDef`
  - Applies idle placement offsets for matching pawns without baking offsets into the part itself.

## Runtime model

- `CompAnatomyBootstrap` persists resolved `AnatomyPartInstance` entries as the hidden logical anatomy owner.
- Each instance currently stores:
  - `partDef`
  - `installedVariant`
  - `size`
  - `List<AnatomyFluidInstance> fluids`
- Size is generated once per pawn/part pair and then saved.
- Fluid entries are generated from XML fluid templates and then saved per pawn/part/fluid pair.
- Existing saves migrate old resolved-part names and penis/vagina booleans into the new instance list.

## Tracker-first rule

Normal runtime checks should read logical anatomy from `CompAnatomyBootstrap`, not from visible natural-anatomy hediffs.

Current practical order is:

1. hidden resolved part instances on `CompAnatomyBootstrap`
2. compatibility signals such as GenderWorks when seeding or resyncing
3. visible gender fallback only when building a fresh preview or initial seed

Legacy natural-anatomy hediffs are migration input, not the live runtime source of truth.

## Author guide

See `Anatomy_Content_Author_Guide.md` for step-by-step examples covering new parts, race-specific parts, gene-specific parts, appearance overrides, placement, and gene-driven size/fluid modifiers.

## Current shipped baseline

- `GenitalDef` now inherits `AnatomyPartDef`.
- `Genital_Penis` and `Genital_Vagina` now declare:
  - `slot`
  - `tags`
  - `capabilities`
  - animation visibility policy
- Default humanlike male/female anatomy now comes from `AnatomyProfileDef` defs.

## Proof of concept in this pass

- Added `Chest` as a generic `AnatomySlotDef`.
- Added `AnatomyPart_Breasts` as a generic internal-only `AnatomyPartDef`.
- Added a default female humanlike profile that grants breasts.
- Breasts now persist per-pawn generated `size` plus a `Milk` fluid entry without requiring textures or render nodes.
- `Genital_Penis` now ships with a default `Semen` fluid template as a second proof that fluids are part-driven, not breast-specific.
- Apparel coverage remains slot-aware for visible parts:
  - `ExternalGenitals` respects pants coverage.
  - `Chest` respects shirt coverage.

## Intentional compatibility bridges

- Legacy hidden save booleans are migrated into resolved part instances.
- `HasPenis()` and `HasVagina()` remain as wrappers over part tags.
- `D2GenitalTextureOverrideDef` still works and is read by the new appearance resolver.
- `BodyTypeGenitalsOffsetDef` still works as a fallback for `ExternalGenitals` parts.
- GenderWorks signals can participate in seed/resync decisions without becoming the saved anatomy owner.

## Content-author workflow target

Adding a new part should require:

1. an `AnatomyPartDef`
2. one or more `AnatomyProfileDef` or appearance/placement override defs
3. optional textures if the part is meant to render

No new per-part C# should be required.

## Current limitations of this pass

- Runtime injection/rendering still targets humanlike pawns only.
- The generic placement resolver currently applies offset tweaks only.
- Breasts are currently an internal-only proof of concept. They persist generated values and default fluid support but do not render.
- Slot anchor resolution currently includes the external-genitals path plus torso/chest support, but not a full body-wide animal anatomy map yet.

## Gene-aware appearance and generation

Genes can now participate in anatomy resolution in two static ways:

- `AnatomyAppearanceOverrideDef` supports a `geneDefs` selector. Gene-matched appearance overrides outrank race-only overrides through specificity scoring, so a gene can swap the texture family the same way a race override does, but win when both apply.
- `AnatomyGeneModifierDef` provides static generation modifiers for per-part `size` and per-fluid capacity / initial amount. These modifiers are applied when a new `AnatomyPartInstance` is created or a missing fluid entry is generated.

Design boundary:

- static gene-driven setup belongs in defs
- dynamic over-time fluid refill / drain logic still belongs in C#

### Appearance notes

`AnatomyAppearanceEntry` now supports optional `sizeTextureVariants`. This lets race or gene appearance overrides define their own size buckets instead of only fixed texture paths.

### Gene modifier notes

`AnatomyGeneModifierDef` stacks all matching defs for the pawn. For this pass it supports:

- part `sizeMultiplier` / `sizeOffset`
- fluid `capacityMultiplier` / `capacityOffset`
- fluid `initialAmountMultiplier` / `initialAmountOffset`

These are generation-time modifiers. Existing saved part instances are preserved rather than silently rerolled when genes change later.

## Runtime fluid production layer

A small runtime fluid layer now sits on top of the saved anatomy instances.

- `AnatomyFluidTemplate.refillPerDay` defines base passive refill/production in XML.
- `AnatomyGeneModifierDef` can now modify runtime refill with:
  - `refillRateMultiplier`
  - `refillRateOffset`
- `CompAnatomyBootstrap.CompTickRare()` runs `AnatomyFluidRuntime.TickRare(...)` to refill saved fluid amounts over time.
- This runtime layer changes only current `amount`. It does not silently reroll saved `size` or `capacity` values when genes change.

Current shipped defaults in this pass:

- `AnatomyPart_Breasts` uses `Milk` with a passive refill baseline.
- `Genital_Penis` uses `Semen` with a passive refill baseline.

Design boundary remains intentional:

- static generation and appearance selection stay def-driven
- over-time fluid amount changes live in C#

## Detached preview pawns

Detached preview pawns now have a clean anatomy path.

Current behavior:

- if a preview pawn mirrors from a real source pawn, `CompAnatomyBootstrap` mirrors the source pawn's hidden resolved part instances
- if there is no source mirror, preview anatomy can seed from the pawn's current gender-driven resolution
- preview seeding still dirties graphics after visible anatomy state changes

That keeps preview anatomy aligned with real runtime anatomy without forcing the preview system to depend on live gameplay ownership.

## Milestone 5/6 status

- Built-in penis/vagina placement rules now live in generic `AnatomyPlacementDef` defs instead of `BodyTypeGenitalsOffsetDef` content.
- `CompLovinParts` uses slot-aware apparel coverage: `ExternalGenitals` checks pants coverage and `Chest` checks shirt coverage.
- Core appearance resolution now prefers generic `AnatomyAppearanceOverrideDef` content; built-in anatomy no longer depends on the old genital-only override path.
- Legacy genital-only classes remain in source as compatibility scaffolding, but built-in content is migrated onto the generic anatomy framework.

## Dev inspection

In RimWorld dev mode, pawns with `CompAnatomyBootstrap` now expose an **Anatomy Debug** gizmo.
It opens a scrollable report showing resolved anatomy parts, sizes, fluids, refill rates, anchors, resolved textures, and current apparel coverage by slot.

## Installed variants

Installed variants are the clean path for bionics and other "same organ, different version" cases.

- Base part identity stays the same, such as `Genital_Penis` or `AnatomyPart_Breasts`.
- `AnatomyPartVariantDef` selects a variant for the pawn, typically using `hediffDefs` for bionics or `geneDefs` for specialized organic versions.
- `AnatomyAppearanceOverrideDef` can target `variantDefs`, and variant-targeted appearance beats gene-only or race-only appearance.
- Current precedence for appearance is:
  - variant > gene > race > base part art

Variants can also apply static generation modifiers for size and fluid capacity/refill without creating a second anatomy part identity.

## Legacy save bridge

NSFW now embeds a small `LegacyBridge` module for old workshop saves. It only keeps the legacy `D2_Genital_Penis` / `D2_Genital_Vagina` defs available long enough for old saves to deserialize, migrate into anatomy part instances on load, and then clean the old hediffs away. New content should never target these legacy defs.

## Related docs

- `Anatomy_Content_Author_Guide.md`
- `Anatomy_Migration_Strategy.md`
- `Legacy_Save_Bridge.md`
- `Integrations/GenderWorks_RouteA_PatchNotes.md`
