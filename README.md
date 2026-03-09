# Despicable 2

A modular RimWorld 1.6 mod suite with face parts, facial animation, Hero Karma, custom UI, and shared animation systems.

## Overview

**Despicable 2** is a modular suite for **RimWorld 1.6** built around expressive pawns, stronger presentation, and reusable systems.

The repository is centered on **Despicable 2 (Core)**, which works as a standalone mod and as the required foundation for optional add-ons. Core adds face parts, facial expressions, shared animation systems, Hero Karma, custom UI tools, and supporting frameworks designed for compatibility and long-term maintainability.

An optional **Despicable 2 (NSFW)** add-on extends Core with adult-only intimacy content, additional animation hooks, extra rendering features, sound, and expression support. It is intentionally separated from Core by design.

## Features

### Despicable 2 (Core)
- **Face parts and facial expression systems** for more expressive pawn presentation.
- **Facial animation support** with reusable animation definitions and render nodes.
- **Hero Karma** gameplay systems with faction standing, reputation effects, perks, and dedicated UI.
- **Custom UI framework** used to build consistent windows, tabs, panels, rulers, tables, forms, and debug-friendly layouts.
- **Shared animation infrastructure** for playback, offsets, visual events, and authoring workflows.
- **Manual interaction menu systems** for cleaner interaction-driven UX.
- **Compatibility-first architecture** with guarded integrations, runtime detection, and soft-fail behavior where possible.
- **Localization support** with structured English source strings and translated language folders.

### Optional NSFW Add-on
- Adult-only intimacy content built on top of Core.
- Extra animations, sounds, expressions, and render features.
- Separate package and dependency boundary to keep Core clean and modular.

## Modules at a glance

### Core
The Core package is the backbone of the suite. Based on the repository structure, it includes several major subsystems:

- **FacePartsModule** for facial styles, expressions, customizers, and rendering.
- **HeroKarmaModule** for karma events, faction standing, local reputation, perks, diagnostics, and UI.
- **AnimModule** for shared animation logic, playback, render support, and editor tooling.
- **UIFramework** for reusable layout helpers, widgets, blueprints, shells, tables, search, and debug overlays.
- **ManualInteraction** for structured manual interaction menus and request handling.

### NSFW
The NSFW package is an optional add-on that depends on Core and includes:

- lovin/intimacy job drivers and playback hooks
- additional render logic and visual events
- compatibility bridges for supported adult-content integrations
- its own runtime state and hook bootstrap layer

## Installation

### Core only
1. Install **Harmony**.
2. Add **Despicable 2 (Core)** to your RimWorld 1.6 mods.
3. Enable it in your mod list.

### Core + optional NSFW
1. Install **Harmony**.
2. Enable **Despicable 2 (Core)**.
3. Enable **Despicable 2 (NSFW)** below Core.
4. Keep RimWorld's dependency/load-order warnings satisfied.

## Compatibility

This suite is built with a compatibility-first approach.

- **Core is standalone** and can be used by itself.
- **NSFW requires Core**.
- The repository includes explicit `loadAfter` metadata for **Harmony** and certain optional compatibility targets.
- The codebase uses runtime guards and isolated integration code to reduce hard failures when optional mods are absent.

## Supported RimWorld version

- **RimWorld 1.6**

## Languages

The repository currently includes language folders for:

- English
- Chinese Simplified
- French
- German
- Russian
- Spanish

## Development notes

This repository is strongly oriented toward **modular growth, additive changes, and documented guardrails**.

Highlights from the included docs and source layout:

- UI work is built around a reusable **UIFramework** rather than page-specific hardcoded layouts.
- Localization is treated as a first-class pipeline with audit rules and English source mirrors.
- Runtime state ownership is carefully separated to reduce hidden globals and brittle cross-system behavior.
- Optional integrations are isolated behind compatibility modules and reflection/Harmony guardrails.
- The codebase favors extending stable seams over broad rewrites.

Relevant docs in `Despicable2-Core/Docs/` include:

- `Architecture_Rules.md`
- `Consistency-Charter.md`
- `UIFramework_RulesOfTheRoad.md`
- `UIFramework_Cookbook.md`
- `Localization_Guardrails.md`
- `Localization_Guide.md`
- `Smoke_Test_Policy.md`

## Why this repo stands out

Despicable 2 is not just a content drop. It is also a **systems repo**.

It combines gameplay mechanics, presentation layers, render extensions, UI tooling, and compatibility infrastructure in a way that makes it useful both as a playable mod suite and as a long-term technical foundation for future expansion.
