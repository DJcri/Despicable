# Despicable 2 docs map

This folder is the **repo-level map**.

Put docs here when they describe the suite as a whole, cross-package ownership, or modder-facing entry points.

## Start here

- `DESPICABLE_SPEC.md`
  - Repo field manual.
  - Use this for ownership, runtime flow, edit-start paths, and handoff context.
- `DESPICABLE_SPEC_MAINTENANCE.md`
  - Keep the spec alive instead of letting it fossilize.
- `ModderGuides/00_Start_Here.md`
  - Fast entry point for outside modders.

## Package-owned docs

Move package-specific notes into the package that owns the behavior.

### Core-owned docs

Look under `../Despicable2-Core/Docs/` for:
- UI framework rules and cookbook
- face preview and portrait notes
- Anim Group Studio preview workflow
- HeroKarma notes and validation sweeps
- localization and regression docs

### NSFW-owned docs

Look under `../Despicable2-NSFW/Docs/` for:
- anatomy framework and content authoring
- anatomy migration notes, including the built-in legacy bridge
- GenderWorks integration notes
- NSFW localization notes
- texture override notes

## Rule of thumb

- **Repo-wide or cross-package**: keep it here.
- **Core-only**: move it under `Despicable2-Core/Docs/`.
- **NSFW-only**: move it under `Despicable2-NSFW/Docs/`.
- **Feature patch notes**: keep them next to the owning module, not floating at repo root.
