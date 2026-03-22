# Anatomy migration strategy

## Goal

Keep the live NSFW anatomy system independent from a custom genital body part while still offering a migration path for older saves that were created when the Human body tree contained `D2_ExternalGenitals`.

## Mainline rules

- The main `Despicable2-NSFW` package no longer patches the vanilla Human body tree.
- Runtime anatomy queries should prefer stable anchors in this order:
  1. `SEX_Reproduction` when another mod provides it
  2. `Pelvis`
  3. Legacy `D2_ExternalGenitals` only as a last-resort compatibility fallback
- Hidden resolved anatomy remains the source of truth for normal runtime checks.
- Legacy natural anatomy hediffs remain migration input and are removed after import.

## Legacy save bridge

Old saves that were authored while `D2_ExternalGenitals` was injected into the Human body tree should be loaded with the optional `Despicable2-NSFW-LegacyAnatomyBridge` companion package enabled.

That bridge package exists only to:

- reintroduce the old Human body-tree patch during load
- let old save data deserialize against the expected body tree
- allow the main NSFW package to import legacy anatomy into hidden resolved state

After players load and resave on the bridge, future cleanup can retire the bridge entirely.

## Future anatomy work

When native surgeries, implants, or bionics are added later:

- target the stable anchor instead of `D2_ExternalGenitals`
- treat the legacy body part as read-only migration history
- keep runtime rendering and interaction checks driven by logical anatomy, not body-tree shape
