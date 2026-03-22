# Despicable2-Core

Shared gameplay, animation, face-part, UI framework, and Hero Karma systems for the Despicable 2 RimWorld 1.6 mod suite.

This package is the Core half of the project. The optional explicit-content and anatomy layer lives in the separate `Despicable2-NSFW` package.

## What Core owns

- shared animation framework and Anim Group Studio
- face parts, facial expressions, portrait warmup, and preview helpers
- Hero Karma, local reputation, perks, and related UI
- custom UI framework used across the suite
- manual interaction menu infrastructure
- compatibility-safe extension seams used by optional add-ons

## Standalone use

Core works on its own.
You do **not** need the NSFW package for face parts, Hero Karma, Anim Group Studio, or the UI framework.

## Optional pairing

Pair this with `Despicable2-NSFW` only if you want the adult-only lovin', anatomy, and explicit-content features.

## Feature notes

### Face parts and facial animation

Core owns the face system and preview behavior.
You can disable Despicable facial animation if you prefer another facial-animation mod path.

### Anim Group Studio

Core includes the shared authoring and preview surfaces used for staged animation work.
AGS preview uses detached preview pawns and workshop-scoped render state rather than normal gameplay playback.

### Hero Karma

Hero Karma is optional inside Core.
You can use the suite without assigning a hero, but the system is there if you want karma-driven perks, penalties, and standing/reputation behavior.

## Docs

- `Docs/README.md`
- `Docs/AnimGroupStudio_Preview_Workflow.md`
- `Docs/FaceParts_Preview_and_Portraits.md`
- `Docs/Manual_Regression_Checklist.md`
- `../Docs/DESPICABLE_SPEC.md`

## Practical note

If something seems missing after a workshop update, do not assume Steam actually refreshed the files cleanly. Verify the local mod contents before chasing phantom bugs.
