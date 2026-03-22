# Despicable 2

![Despicable 2 preview](Despicable2-Core/About/preview.png)

A modular RimWorld 1.6 mod suite with face parts, facial animation, Hero Karma, custom UI, shared animation systems, and an optional NSFW anatomy/content layer.

## Overview

**Despicable 2** is a modular RimWorld suite built around expressive pawn presentation, reusable systems, and a stronger interface layer.

The repository is centered on **Despicable 2 (Core)**, which works as a standalone mod and as the required foundation for optional add-ons. Core adds face parts, facial expressions, shared animation systems, Hero Karma, custom UI tools, and supporting infrastructure designed for compatibility and long-term maintainability.

An optional **Despicable 2 (NSFW)** add-on extends Core with adult-only intimacy content, anatomy, additional rendering behavior, sound, and integration hooks. It is intentionally separated from Core by design.

## Modules

### Despicable 2 (Core)

The Core package is the backbone of the suite.

It includes:
- face parts and facial expression systems for more expressive pawns
- facial animation support with reusable animation definitions and render nodes
- Anim Group Studio authoring / preview surfaces and shared animation infrastructure
- Hero Karma systems with faction standing, reputation effects, perks, and dedicated UI
- a custom UI framework for windows, tabs, tables, rulers, forms, search, and debug-friendly layouts
- manual interaction menu systems for cleaner interaction-driven UX
- localization support with English source strings and translated language folders
- compatibility-first architecture with guarded integrations and documented seams

### Despicable 2 (NSFW)

The NSFW package is an optional adult-only add-on that depends on Core.

It includes:
- explicit intimacy content and related job logic
- anatomy framework, logical anatomy persistence, and genital rendering
- additional animations, sounds, expressions, and render features
- compatibility bridges for supported adult-content integrations
- a separate dependency boundary so Core remains modular and clean

## Feature summary

### Core highlights
- **FacePartsModule** for face styles, customizers, expressions, preview support, and rendering
- **HeroKarmaModule** for karma events, faction standing, local reputation, perks, diagnostics, and UI
- **AnimModule** for shared animation logic, playback, render support, Anim Group Studio, and tooling
- **UIFramework** for reusable layout helpers, widgets, shells, tables, search, and overlays
- **ManualInteraction** for structured manual interaction flows and request handling

### NSFW highlights
- **Anatomy framework** for logical part resolution, variants, fluid state, appearance overrides, and placement
- **LovinModule** for explicit interactions, jobs, hooks, and runtime state
- **Integration layer** for optional adult-content mod bridges such as GenderWorks

### Design goals
- modular growth without turning the codebase into a knot
- reusable systems instead of one-off hacks
- compatibility-minded integrations
- cleaner custom UI and better presentation
- room for optional content without bloating Core

## Requirements

- **RimWorld 1.6**
- **Harmony**

## Installation

### Core only
1. Install **Harmony**.
2. Add **Despicable 2 (Core)** to your RimWorld 1.6 mods.
3. Enable it in your mod list.

### Core + optional NSFW
1. Install **Harmony**.
2. Enable **Despicable 2 (Core)**.
3. Enable **Despicable 2 (NSFW)** below Core.
4. Keep RimWorld's dependency and load-order warnings satisfied.

## Compatibility

This suite is built with a compatibility-first approach.

- **Core is standalone** and can be used by itself.
- **NSFW requires Core**.
- Optional integrations are kept isolated where possible to reduce hard failures when other mods are absent.
- The codebase favors guarded integrations and stable seams over brittle hard assumptions.

## Languages

The repository currently includes language support for:
- English
- Chinese Simplified
- French
- German
- Russian
- Spanish

## Repository layout

```text
AGENT.md
AGENTS.md
Docs/
Tools/

Despicable2-Core/
  1.6/
  About/
  Config/
  Defs/
  Docs/
  Languages/
  Patches/
  Source/
  Textures/

Despicable2-NSFW/
  1.6/
  About/
  Defs/
  Docs/
  Languages/
  Patches/
  Sounds/
  Source/
  Textures/
```

## Documentation map

- `Docs/README.md`
  - repo-wide docs map
- `Docs/DESPICABLE_SPEC.md`
  - ownership, flow, and handoff field manual
- `Docs/ModderGuides/00_Start_Here.md`
  - outside-modder entry point
- `Despicable2-Core/Docs/README.md`
  - Core docs map
- `Despicable2-NSFW/Docs/README.md`
  - NSFW docs map

## Development notes

This repository is strongly oriented toward modular growth, additive changes, and documented guardrails.

The project generally prefers:
- framework-driven UI layout instead of brittle hardcoded positioning
- isolated optional integrations
- localization as a first-class pipeline
- extending stable systems rather than rewriting broad surfaces at random
- keeping feature notes inside the package that owns the behavior

## Release channels

To keep releases simple, this project uses three release types:
- **alpha** for rough or experimental public testing
- **beta** for wider testing before normal release
- **stable** for standard public releases

Example tags:
- `v0.8.0-alpha.1`
- `v0.8.0-beta.1`
- `v0.8.0`

## GitHub About

**Description**

A modular RimWorld 1.6 mod suite with face parts, facial animation, Hero Karma, custom UI, shared animation systems, and an optional NSFW anatomy layer.

**Tagline**

Expressive pawns, modular systems, and a little frontier grandeur gone rotten.

**Suggested topics**

`rimworld` `rimworld-mod` `rimworld-1-6` `harmony` `csharp` `modding` `animation-system` `ui-framework` `localization` `character-customization`

## Licensing

### Code and software materials

Source code and other software materials in this repository are licensed under **PolyForm Noncommercial 1.0.0**. That includes source code, assemblies, XML defs, patches, config, localization files, and documentation unless a file or directory is explicitly marked otherwise.

In plain terms: noncommercial use, modification, and redistribution are allowed under the license, but commercial redistribution or monetized republishing requires separate permission.

See [`LICENSE`](LICENSE).

## Disclaimer

RimWorld is © Ludeon Studios. This project is an unofficial fan-made mod suite and is not affiliated with or endorsed by Ludeon Studios.
