# DESPICABLE_SPEC.md

## 1. Purpose, scope, and intended use

**Despicable 2** is a RimWorld 1.6 mod suite built around expressive pawns, reusable animation and rendering systems, custom UI infrastructure, and modular optional content.

This document is the repo field manual for future feature work, bug triage, and AI handoffs. It is not a player guide. Its job is to answer three questions quickly:

1. **Who owns this behavior?**
2. **Where does this runtime flow begin and end?**
3. **Where should edits start without creating a second source of truth?**

At the repository level, “feature-complete” means the suite already has its intended major systems in place:
- Core facial rendering and facial animation
- shared animation/runtime infrastructure
- Anim Group Studio authoring and preview surfaces
- HeroKarma gameplay and UI
- UIFramework for custom windows and layout
- optional NSFW content layered on top of Core

Future work should mostly be polish, targeted extension, compatibility work, and local repairs. This repo is **not** intended to be rewritten from scratch during normal feature work.

---

## 2. Repository and package map

### 2.1 Top-level structure

- `README.md` describes the suite at a high level.
- `AGENT.md` and `AGENTS.md` are the repo-root implementation guardrails.
- `Tools/` contains shared guardrail and localization audit tooling.
- `Despicable2-Core/` is the standalone base package and the architectural center of gravity.
- `Despicable2-NSFW/` is an optional add-on package that depends on Core.

### 2.2 Core package map

Core contains the standard RimWorld package layout:

- `About/` mod metadata and preview assets
- `Config/` persistent configuration data such as the face head blacklist
- `Defs/` XML gameplay, face, facial animation, and UI demo defs
- `Docs/` architecture rules, UI rules, localization rules, regression checklists
- `Languages/` English source plus translations
- `Patches/` XML patch operations
- `Source/Despicable/` C# source of truth
- `Textures/` art assets
- repo-root `Tools/` shared guardrail and localization audit tooling

### 2.3 NSFW package map

NSFW mirrors the same mod-package shape, but it is intentionally narrower in scope:

- explicit-content defs and assets
- additional render nodes and animation hooks
- lovin job logic
- optional external integrations
- its own runtime reset hub and localization guardrails

### 2.4 Practical ownership takeaway

When in doubt, **Core is the system owner** and NSFW is the extender. NSFW may add behavior, hooks, animations, sounds, and integrations, but it should not quietly become the owner of a base shared system.

---

## 3. Source-of-truth docs already in the repo

This spec sits above the smaller docs. Keep these aligned with it:

- `README.md`: high-level project overview
- `AGENT.md` and `AGENTS.md`: repo-wide implementation guardrails
- `Despicable2-Core/Docs/Architecture_Rules.md`: architectural and refactor rules
- `Despicable2-Core/Docs/Consistency-Charter.md`: naming, ownership, file placement, reset rules
- `Despicable2-Core/Docs/UIFramework_RulesOfTheRoad.md`: UI framework contracts
- `Despicable2-Core/Docs/UIFramework_Cookbook.md`: UI usage patterns and examples
- `Despicable2-Core/Docs/Manual_Regression_Checklist.md`: manual testing expectations
- `Despicable2-Core/Docs/Smoke_Test_Policy.md`: smoke-check policy
- `Despicable2-Core/Docs/Localization_Guardrails.md`: localization audit rules
- `Despicable2-Core/Docs/FaceParts_Preview_and_Portraits.md`: face customizer, portrait warmup, and preview notes
- `Despicable2-Core/Docs/AnimGroupStudio_Preview_Workflow.md`: AGS detached preview and renderer notes
- `Despicable2-NSFW/Docs/Anatomy_Def_Framework.md` and `Anatomy_Content_Author_Guide.md`: anatomy ownership, runtime model, and content authoring
- `Despicable2-NSFW/Docs/Localization_Guide.md` and `Localization_CodeStringMap.md`: NSFW-specific localization addenda

Use those for detailed policy. Use this file for system ownership and flow mapping.

---

## 4. Module ownership map

### 4.1 Core bootstrap and shared infrastructure

Primary owners:
- `DespicableBootstrap.cs`
- `ModMain.cs`
- `Settings.cs`
- `Core/Runtime/DespicableRuntimeState.cs`

Ownership rules:
- `DespicableBootstrap` is the **single source of truth for Core startup**.
- `ModMain` owns settings access and delegates startup to the bootstrap.
- `Settings` is the **central settings store**, including NSFW-facing toggles that players should edit in one place.
- `DespicableRuntimeState` is the **cross-system ephemeral reset hub** for Core-owned runtime caches and thread-local preview/render state.

### 4.2 AnimModule

AnimModule owns:
- shared animation playback infrastructure
- stage playback backends and stage tag providers
- extended animator components and runtime bridges
- render support for animation-driven props and offsets
- Anim Group Studio model, persistence, preview, export, and UI
- workshop/author preview context helpers

AnimModule does **not** own face-part texture selection logic. It can drive or sample facial animation, but face rendering itself belongs to FacePartsModule.

### 4.3 FacePartsModule

FacePartsModule owns:
- face-part defs, styles, expressions, and facial animation defs
- `CompFaceParts` runtime state and per-pawn facial behavior
- face render nodes and face render workers
- portrait warmup concerns for face readiness
- head blacklist management
- auto eye patch generation/runtime
- face-part customization UI and face management surfaces
- face-related compatibility patches, including Pawn Editor compatibility

`CompFaceParts` is the main runtime owner for face state. Future edits should treat it as the first stop for questions about face enablement, expression sampling, preview overrides, blink timing, and face refresh behavior.

### 4.4 HeroKarmaModule

HeroKarmaModule owns:
- hero assignment and global karma/standing logic
- ideology-sensitive approval/standing logic
- local reputation data and services
- perk definitions, tuning, and effect application
- diagnostics and validation helpers
- HeroKarma UI and display formatting
- gameplay event hooks that emit HeroKarma events

`HKRuntime` is the UI-facing runtime facade. `HeroKarmaBridge` is the public bridge surface. Tuning lives in `HKBalanceTuning.cs`.

### 4.5 UIFramework

UIFramework owns:
- layout primitives and allocation helpers
- style tokens and shared UI metrics
- reusable controls, shells, tables, rulers, previews, search, and action bars
- measure-pass and draw-pass discipline
- rect recording, validation, and debug overlay plumbing
- framework-native settings rendering helpers

UIFramework should absorb repeated custom UI patterns rather than having feature modules re-copy layout logic.

### 4.6 ManualInteraction

ManualInteraction owns:
- right-click interaction menu entry and request construction
- option specification types and builders
- menu host/opening logic
- legacy adapter helpers

The canonical request builder is **not** the menu UI. It is `Core/InteractionEntry.cs`. Menu code should build menu options and delegate request preparation/resolution through that central entry path.

### 4.7 NSFW / LovinModule

NSFW owns:
- explicit-content behavior and assets
- lovin job logic, drivers, givers, runtime state, and UI
- logical anatomy resolution, hidden anatomy persistence, and anatomy debug tooling
- genital defs, appearance/placement rules, and genital render nodes
- NSFW-side hooks and optional adult-content integrations
- NSFW-side runtime reset behavior

NSFW can extend shared systems from Core, but it should not become the canonical owner of shared animation, shared settings persistence, or face-part base logic.

---

## 5. Startup and lifecycle flow

### 5.1 Core startup

Primary path:
- `DespicableBootstrap` static constructor calls `EnsureInitialized()`.
- `EnsureInitialized()` first registers Core runtime pieces, then initializes Harmony.
- `ModMain` also calls `DespicableBootstrap.EnsureInitialized()` so constructor and static-load behavior stay aligned.

Important contract:
- **staging registrations first, Harmony patching second**
- do not reimplement startup logic in parallel entrypoints

Core startup registration currently includes:
- `BasicStagePawnTags`
- `StageClipTimelineBackend`
- `StagePlaybackBackend`

Those are registered into `StageTagProviders` and `StagePlaybackBackends`.

### 5.2 Def/load-time warmup

`ModMain.PlayDataLoader_DoPlayLoad_Patch` is responsible for post-def warmup after RimWorld finishes loading defs:
- `FacePartsUtil.LoadHeadTypeBlacklist()`
- `AutoEyePatchRuntime.EnsureGenerated()`
- `HKBalanceTuning.ApplyPerkDefOverrides()`

This is the correct place for content-dependent warmup that needs loaded defs.

### 5.3 Portrait warmup

`HarmonyPatch_PortraitsCache_Get_DespicableFaceWarmup` exists to ensure face parts are ready before portrait consumers request a render. This is a face-readiness seam and should be treated as render-sensitive.

### 5.4 Runtime reset boundaries

Core reset owner:
- `GameComponent_DespicableRuntimeReset`
- `DespicableRuntimeState.ResetRuntimeState()`

On new game and loaded game:
- Core resets ephemeral runtime state
- `GameComponent_ExtendedAnimatorRuntime.ResetRuntimeState()` also runs
- loaded-game flow schedules `NotifyLoadedGame` after long event completion

NSFW mirrors this with:
- `GameComponent_DespicableNsfwRuntimeReset`
- `DespicableNsfwRuntimeState.ResetRuntimeState()`
- loaded-game rehydration for lovin parts after reset

### 5.5 Lifecycle rule

Any new shared ephemeral runtime state must have:
- one obvious owner
- `ResetRuntimeState()`
- inclusion in the appropriate runtime reset hub

Do not add free-floating mutable static state that survives load/new game boundaries without an owner and reset path.

---

## 6. Settings, ownership, and mutable state rules

### 6.1 Central settings ownership

`Settings.cs` is the single persisted settings owner for the suite’s player-facing toggles. This includes:
- animation enablement
- face parts and auto eye patch toggles
- HeroKarma toggles and dev toggles
- NSFW/lovin toggles and content presentation flags
- workshop export path

Even when a setting primarily affects NSFW behavior, the persistence owner is still Core so the player gets a single settings surface.

### 6.2 Mutable state buckets

The repo’s intended buckets are:
- constants/defaults
- settings/config
- per-instance runtime state
- shared ephemeral runtime state
- caches/registries

Feature work should classify new state into one of those buckets before implementation.

### 6.3 Working rule

Prefer:
- per-instance state on the owning comp/class
- explicit shared runtime-state owners for cross-instance needs

Avoid:
- copied settings mirrors in mutable globals
- convenience “current object” globals without tight scoping
- second sources of truth for caches or enablement state

---

## 7. Animation system overview

### 7.1 Core concepts

The animation system combines several layers:
- stage tags and stage plans
- playback backends
- `CompExtendedAnimator` runtime playback state
- prop render nodes and render subworkers
- Anim Group Studio authoring, persistence, and preview surfaces

Important ownership split:
- **AnimModule owns animation orchestration and sampling**
- **FacePartsModule owns how sampled facial state becomes visible face graphics**

### 7.2 Playback ownership

Key owners:
- `StagePlaybackBackends.cs`
- `CompExtendedAnimator.cs`
- `ExtendedAnimatorPlaybackController.cs`
- `ExtendedAnimatorRuntimeState.cs`
- `GameComponent_ExtendedAnimatorRuntime.cs`

Behavioral intent:
- stage plans should resolve through registered backends
- runtime playback state belongs to animator owners, not random UI code
- preview state should remain separate from live gameplay queues

### 7.3 Render integration

Animation-driven render integration lives in:
- `AnimParts/PropRenderNodes/`
- `AnimParts/RenderSubworkers/`
- animation-related Harmony patches and runtime helpers

These files are where animation affects visual offsets, prop visibility, and additional render requests.

### 7.4 Authoring surfaces

Anim Group Studio is split cleanly by role:
- `Model/` for authoring-side model state
- `Persistence/` for repository/save/load concerns
- `Preview/` for live preview harnesses and preview sampling
- `Export/` for export utilities
- `UI/` for editor dialogs and authoring surfaces

That split should be preserved. Avoid collapsing authoring, preview, and runtime playback into one mixed owner.

---

## 8. Studio preview pipeline

### 8.1 Design goal

`AgsPreviewSession` is a **sampling-based live preview harness**. It is explicitly documented to:
- behave like the old workshop preview
- avoid relying on `CompExtendedAnimator` runtime queues/state for preview playback
- drive `WorkshopRenderContext.Tick` directly
- sample the currently active `AnimationDef` for the current stage and tick

That is a critical architectural choice. Preview is not gameplay playback. It is an authoring-time sampling environment.

### 8.2 Main preview flow

The preview loop runs through:
- `AgsPreviewSession.Playback.cs`
- `AgsPreviewSession.Viewport.cs`
- `WorkshopRenderContext.cs`
- `WorkshopPreviewRenderer.cs`

High-level flow:
1. session starts or resumes playback
2. scheduler advances a stage-local tick
3. `WorkshopRenderContext.Tick` is updated
4. viewport render samples the current animation at that tick
5. preview facial state is applied for that render pass
6. renderer draws a non-portrait workshop preview

### 8.3 Stage-local tick rule

Workshop preview uses a **stage-local tick**, not a continuously accumulated absolute animation tick for all purposes. This matters at stage transitions and loops. Bugs that appear only on loops usually come from a mismatch between:
- visible scheduler tick
- stage-local sampling tick
- preview override state that did not reset or redraw when the stage restarted

### 8.4 Scrub vs live playback rule

Scrubbing and live preview should show the same result for a given stage/tick sample.

If scrub is correct and live preview is wrong, suspect one of these seams first:
- preview override state not being cleared or rewritten at loop boundaries
- render graphics not being dirtied when preview expression changes
- stage-local tick reset not matching the facial sampling tick
- render-tree recache behavior differing between scrub and live playback

### 8.5 Preview-specific runtime state

`WorkshopRenderContext` is a thread-local scoped owner for:
- whether the current render pass is a workshop preview pass
- the current preview tick

It is intentionally isolated and reset through `DespicableRuntimeState`.

Do not replace it with broad global flags.

### 8.6 Current known preview-sensitive seam

In the current codebase, `CompFaceParts.ApplyPreviewFacialAt()` and `ClearPreviewFacialOverride()` explicitly dirty graphics when preview facial expression changes. The code comments make the current contract clear:
- offsets can read `animExpression` directly
- mouth/eye textures are still cache-sensitive
- changing preview expression must dirty graphics so cached `GraphicFor` state rebuilds on the same render pass

That is the correct shape for this seam: **dirty when preview expression changes**, not “rebuild everything constantly” as a default strategy.

---

## 9. FaceParts and facial animation pipeline

### 9.1 Data model

Core data files live under:
- `Defs/FacePartsModule/ExpressionDefs.xml`
- `Defs/FacePartsModule/FacePartDefs.xml`
- `Defs/FacePartsModule/FacePartStyleDefs.xml`
- `Defs/FacePartsModule/FacialAnimDefs.xml`
- `Defs/FacePartsModule/HeadBlacklistDef.xml`

Runtime C# defs live under:
- `FaceParts/ExpressionDef.cs`
- `FaceParts/FacePartDef.cs`
- `FaceParts/FacePartStyleDef.cs`
- `FaceParts/FacialAnimDef.cs`

### 9.2 Main runtime owner

`CompFaceParts` is the main owner for per-pawn face behavior:
- enabled state
- blink scheduling
- base expression and animation expression
- current facial animation and animation tick
- face structure dirtiness and warmup state
- expression sampling and visual-state caching
- preview override application and clearing

If a bug involves facial timing, face enablement, blink behavior, preview expression overrides, or face refresh, start here.

### 9.3 Face render pipeline

The render path is split across:
- `PawnRenderNodeWorker_FacePart.cs`
- `RenderNodes/Mouth/PawnRenderNode_Mouth.cs`
- `RenderNodes/Eyes/PawnRenderNode_EyeAddon.cs`
- worker variants for mouth, eye addon, and auto eye patch

Important distinction:
- **offsets/positioning** can read current expression state directly
- **graphics/textures** may still be cached and need render invalidation to update

That distinction explains why a face can look partly correct while still showing stale texture state.

### 9.4 Facial animation flow

General flow:
1. authored facial animation data exists as `FacialAnimDef`
2. `CompFaceParts` samples either live facial animation or a preview facial animation at a tick
3. sampled `ExpressionDef` becomes `animExpression`
4. render workers and nodes read the resulting expression and face-part state
5. if the texture-facing state changed, graphics must be dirtied so nodes rebuild

### 9.5 Preview override rule

Preview facial state is an override for the workshop/authoring surface. It must:
- not permanently mutate gameplay-owned face state
- be reversible on scrub, stop, stage change, and loop restart
- trigger redraw/rebuild when the visible face texture should change

### 9.6 Face-readiness and warmup rule

Face readiness is sensitive around portrait generation and preview pawn setup. The repo already contains warmup logic and preview-pawn refresh helpers. Future edits should preserve the pattern of:
- initialize face state first
- dirty or refresh renderers second
- only then rely on portrait or preview consumers

### 9.7 Auto eye patch ownership

Auto eye patch generation/runtime belongs to `FacePartsModule/Runtime/AutoEyePatch/`.

Use that folder for:
- patch generation
- eligibility checks
- diagnostics
- pending face refresh state tied to generated content

Do not spread auto-eye-patch ownership across generic utility files.

---

## 10. HeroKarma system overview

### 10.1 Scope

HeroKarma is a compiled gameplay system with these major lanes:
- hero selection
- global karma
- ideology-sensitive global standing
- local reputation
- perk activation and effects
- event diagnostics and display text
- dedicated UI

### 10.2 Runtime and bridge surfaces

Primary entrypoints:
- `HKRuntime.cs` for UI-facing runtime access
- `HeroKarmaBridge.cs` for public bridge-style access
- `HKServices.cs` and related services for internal resolution and service ownership
- `HKBalanceTuning.cs` for numbers and thresholds

Working rule:
- `HKRuntime` is a facade, not the place to invent a second gameplay system
- tuning belongs in tuning owners, not duplicated in UI text or Defs

### 10.3 Event and pipeline ownership

Gameplay hooks emit events into HeroKarma’s pipeline through patch files and token/pipeline classes. Relevant ownership areas include:
- `Patches/` for event capture and Harmony-sensitive gameplay seams
- `Pipeline/` for tokens, token application, and event debouncing
- `Services/` for calculation and runtime accessors
- `LocalReputation/` for settlement/pawn-local reputation data

### 10.4 Diagnostics and validation

HeroKarma includes first-class diagnostics and validation helpers. Use:
- `HKDiagnostics`
- docs under `Despicable2-Core/Docs/HeroKarma/`
- manual regression checklist items

For ideology-sensitive bugs, diagnostics and sweep docs are part of the intended workflow, not optional garnish.

### 10.5 UI ownership

HeroKarma UI belongs under `HeroKarmaModule/UI/` and is built on UIFramework surfaces such as tab hosts, ruler rows, shells, and shared helpers.

UI text and displayed numbers should pull from runtime/tuning owners, not hardcoded copies.

---

## 11. UIFramework architecture and rules

### 11.1 Core contract

UIFramework exists to prevent custom window code from becoming a heap of hardcoded rectangles and duplicated micro-patterns.

Main owners include:
- `UIContext`
- `D2UIStyle`
- `UIRectRegistry`
- layout helpers such as `D2VStack`, `D2HRow`, `D2RectTake`, `D2Layout`, `D2ScrollView`
- controls such as `D2Tabs`, `D2Table`, `D2BandRulerRow`, `D2ActionBar`, `D2Fields`
- blueprint wrappers for dialogs, float menus, gizmos, and tabs

### 11.2 Measure-pass and draw-pass discipline

`UIContext` explicitly supports:
- a measure pass for layout computation
- a draw pass for actual widget emission
- scope-aware rect recording for validation/debugging

That is a core framework contract. Avoid mixing “figure out size” and “emit widgets” in a way that defeats measurement.

### 11.3 Layout philosophy

The repo consistently prefers:
- framework-driven fitting
- remainder-rect carving
- style-token spacing
- reusable helpers and blueprints

It explicitly discourages:
- screenshot-tuned offsets
- brittle hand-placed micro-rectangles
- spreading the same pattern across many windows without a helper

If a UI change appears to require hardcoded positioning, document why before landing it.

### 11.4 Validation and overlay ownership

`UIRectRegistry` is the owner for per-window rect recording and layout issue detection. It supports:
- overlap allowances
- validation passes
- rate-limited issue logging
- debug overlay-sensitive exceptions

This is part of the framework’s intended maintenance story. New complex UI should play nicely with it.

### 11.5 Settings page pattern

`D2ModSettingsRenderer` is the framework-native settings page renderer and a good reference for:
- attached vanilla-style tabs
- grouped sections
- scroll views with measured heights
- settings split into visible tabs based on content availability and dev mode

Because settings live in Core, even optional-content settings should appear through this centralized rendering path unless there is a strong reason not to.

---

## 12. Manual interaction architecture

### 12.1 Vanilla entrypoint

The right-click interaction entry comes from the Core Harmony patch against `FloatMenuMakerMap.GetOptions` in `ModMain.cs`.

That patch should stay thin. Its job is to route into the interaction menu system, not to grow into a second interaction framework.

### 12.2 Main flow

Key files:
- `InteractionMenu.cs`
- `ManualMenuRequest.cs`
- `ManualMenuOptionSpec.cs`
- `ManualMenuBuilder.cs`
- `ManualMenuHost.cs`
- `ManualMenuLegacyAdapter.cs`
- `Core/InteractionEntry.cs`

Flow:
1. right-click patch adds menu category entries
2. `InteractionMenu` gathers valid targets and creates manual menu requests
3. `ManualMenuHost` opens the menu
4. `ManualMenuBuilder` turns option specs into float-menu options
5. actual interaction resolution goes through `InteractionEntry.ResolveManual(...)`

### 12.3 Ownership rule

Menu code must not manually construct ad hoc interaction resolution logic all over the repo. `InteractionEntry` is the canonical request/resolve builder for manual interactions.

### 12.4 UI rule

Menu creation belongs in the draw/input phase only. Option specs may include disabled reasons, tooltips, icons, and toggle actions, but they are still data for the menu host/builder pipeline.

---

## 13. Core ↔ NSFW boundary contract

### 13.1 Dependency direction

The intended dependency direction is strict:
- Core is standalone
- NSFW requires Core
- Core must not assume NSFW assets, language files, or explicit-content defs

### 13.2 Startup layering

NSFW startup goes through:
- `DespicableNSFWBootstrap`
- `Despicable.NSFW.ModMain.EnsureInitialized()`
- `HookBootstraps.EnsureRegistered()`
- `NsfwCompatBootstrap.EnsureInitialized()`

This keeps NSFW hook registration and compatibility initialization centralized.

### 13.3 Shared seams

Allowed shared seams include:
- Core-owned settings toggles that enable or disable NSFW behavior
- Core staging/tag systems that NSFW registers into
- shared animation/render systems that NSFW extends
- runtime detection and guarded integration points

### 13.4 Isolation rules

Keep these isolated:
- explicit assets and sounds
- adult-only localization keys and docs
- optional integration reflection code
- NSFW runtime caches and reset logic

Core should not become coupled to NSFW content files or assumptions merely because the combined suite often runs together.

---

## 14. Compatibility, Harmony, and hook strategy

### 14.1 Harmony philosophy

The repo’s intended Harmony style is:
- smallest correct patch
- runtime signature checks where needed
- guarded optional integrations
- centralized bootstrap ownership

Treat Harmony and reflection code as high-voltage wiring.

### 14.2 Optional integrations

Optional support should use:
- isolated integration folders
- runtime detection
- soft-fail behavior
- warn-once diagnostics where useful
- resettable reflection caches if they are ephemeral

### 14.3 Render-sensitive compatibility seams

Notable fragile areas include:
- portrait warmup hooks
- Pawn Editor compatibility
- render-tree append/request behavior
- face-node cache invalidation
- workshop preview rendering versus general portrait rendering

When a patch touches render-node or portrait behavior, prefer a local targeted fix over broad global invalidation.

---

## 15. Localization, assets, and data conventions

### 15.1 Localization ownership

English is the source language. Player-facing code strings must be keyed and translatable.

Core rules:
- no new player-facing literals in code without `Translate()`
- Def text belongs in Defs and is mirrored into English DefInjected as needed
- Core-only keys remain in Core
- NSFW-only keys remain in NSFW

### 15.2 Guardrail rules

The localization pipeline already enforces important safety rules:
- avoid `->` in translation strings
- avoid raw angle brackets in translation strings
- keep placeholders balanced and stable
- avoid duplicate keys
- wrapper-based UI APIs are also scanned for likely unlocalized literals

### 15.3 Asset/data placement

Follow the existing package split:
- face data and assets in Core face-part folders
- UI demo/data in Core UI folders
- HeroKarma content in HeroKarma-owned folders and docs
- NSFW assets in NSFW package folders only

Do not duplicate source-of-truth defs or translation ownership across both packages.

---

## 16. Edit map: where to start for common tasks

### 16.1 Studio preview bugs

Start with:
- `AnimModule/AnimGroupStudio/Preview/AgsPreviewSession*.cs`
- `AnimModule/UI/WorkshopRenderContext.cs`
- `AnimModule/UI/WorkshopPreviewRenderer.cs`

Then inspect FaceParts if the bug is facial-only:
- `FacePartsModule/Comp/CompFaceParts.cs`
- face render nodes/workers

### 16.2 Facial expression or face rendering bugs

Start with:
- `FacePartsModule/Comp/CompFaceParts.cs`
- `FacePartsModule/Comp/CompFaceParts.Refresh.cs`
- `FacePartsModule/RenderNodes/`
- `FacePartsModule/Runtime/FacePartsEventRuntime.cs`
- portrait warmup or preview helpers if the bug only affects portraits or preview pawns

### 16.3 Animation playback/runtime bugs

Start with:
- `CompExtendedAnimator`
- playback controller/runtime state
- stage playback backends and stage plans
- prop render nodes/render subworkers

### 16.4 HeroKarma gameplay changes

Start with the lane that owns the behavior:
- event hook patches for capture
- `Pipeline/` for token flow
- `Services/` for calculation/runtime access
- `Tuning/HKBalanceTuning.cs` for numbers
- `LocalReputation/` for settlement/pawn-local consequences
- UI only after the underlying lane is correct

### 16.5 UI changes

Start in the feature UI file, then decide whether the pattern belongs in UIFramework.

If a pattern repeats or is likely to repeat, promote it to:
- `UIFramework/Controls/`
- `UIFramework/Layout/`
- `UIFramework/Blueprints/`

Update the UI docs in the same pass.

### 16.6 Manual interaction changes

Start with:
- `InteractionMenu.cs`
- `ManualMenu*`
- `Core/InteractionEntry.cs`

Keep menu concerns separate from request-resolution concerns.

### 16.7 NSFW feature work

Start in NSFW-owned folders:
- `LovinModule/`
- `Integrations/`
- `Runtime/`

Only cross into Core when the change truly affects a shared system boundary.

---

## 17. Testing and regression expectations

### 17.1 Build and startup

At minimum, validate:
- Core build health
- NSFW build health
- startup logs
- one-time initialization behavior
- no obvious hook drift after startup-sensitive changes

### 17.2 High-risk Core canaries

After changing risky Core seams, manually check:
- studio preview open/play/stop behavior
- stage scrubbing versus live preview consistency
- portrait face warmup behavior
- face head blacklist tooling if touched
- runtime reset behavior on new game/load

### 17.3 Face and preview-specific checks

For facial or preview changes, explicitly compare:
- scrubbed stage sample
- first live loop
- later loops
- stop/restart behavior
- stage transition behavior
- portrait versus non-portrait preview if relevant

### 17.4 HeroKarma checks

Use the existing regression checklist and diagnostics for:
- ideology-sensitive outcomes
- local reputation behavior
- UI surface correctness
- diagnostics/detail text consistency

### 17.5 NSFW checks

If NSFW was touched:
- startup and integration registration
- one basic interaction path
- lovin-part rehydration or runtime reset if relevant
- anatomy migration / logical anatomy rehydration if relevant
- one dev-mode Anatomy Debug pass if anatomy or rendering changed

### 17.6 Release sanity

Before release candidate state, confirm:
- save/load without red errors
- no-Ideology compatibility behavior
- no obvious legacy-alias regressions
- docs are still aligned with the shipped behavior

---

## 18. Stability zones and danger zones

### 18.1 Stability zones

These are the repo’s intended “stable bones”:
- thin bootstrap delegation
- central settings ownership in Core
- explicit runtime reset hubs
- AnimModule vs FaceParts ownership split
- UIFramework-first layout strategy
- isolated compatibility/integration folders
- Core standalone, NSFW additive

### 18.2 Danger zones

These areas deserve extra caution:
- render-tree invalidation and graphics dirtiness
- portrait warmup and preview-only render behavior
- scrub/live divergence in preview systems
- mutable shared runtime state without reset ownership
- Harmony patches on fragile engine signatures
- localization string formatting mistakes
- duplicated gameplay tuning or displayed numbers

### 18.3 Rule of thumb

When a change seems to require touching many of these at once, the most likely problem is missing ownership clarity, not a need for a bigger rewrite.

---

## 19. Known legacy seams and cleanup targets

This repo intentionally keeps some transitional seams for safety and compatibility.

Examples of acceptable legacy retention:
- wrappers kept during naming or lifecycle migration
- compatibility aliases still read for older settings or saves
- large files that still sit on risky render/engine seams and have not yet earned a safe split

Cleanup is welcome when it solves a real problem, but cleanup should not outrun behavior ownership. A prettier file map is not worth breaking preview, render, compatibility, or save behavior.

---

## 20. Working rules for future AI and human changes

1. **Start from the owner, not from the symptom’s most visible UI.**
2. **Prefer the smallest correct patch.**
3. **Do not create a second source of truth for settings, runtime state, tuning, or caches.**
4. **Treat preview as sampling-based authoring infrastructure, not gameplay playback.**
5. **Treat face graphics invalidation as a distinct concern from face offset sampling.**
6. **Promote repeated UI patterns into UIFramework instead of cloning them.**
7. **Keep Core standalone and NSFW additive.**
8. **When adding shared runtime state, define the owner and reset path immediately.**
9. **When touching Harmony or reflection seams, verify target shape and fail clearly.**
10. **Update the smaller rule docs when the behavior contract actually changes.**

---

## 21. Fast handoff summary

When handing this repo to another AI or another contributor, include this minimum packet:

- the bug or feature goal in one paragraph
- the owning subsystem from this spec
- the likely entry files from the edit map
- whether the issue is runtime, preview-only, portrait-only, UI-only, or compatibility-only
- any known guardrails, especially UI layout, localization, or Core/NSFW boundaries
- whether behavior should stay identical except for the target fix

That keeps future sessions from wasting half their budget rediscovering the map.


---

## 22. Claude handoff template

Use this whenever handing a bug or feature task to another AI after triage.

```text
Task:
[One-paragraph description of the bug or feature]

Goal:
[What should be true after the change]

Scope:
- Package: [Core / NSFW / both]
- Subsystem owner: [AnimModule / FacePartsModule / HeroKarma / UIFramework / ManualInteraction / NSFW]
- Change type: [runtime / preview-only / portrait-only / UI-only / compatibility-only / data-only]

Observed behavior:
- [bullet]
- [bullet]

Expected behavior:
- [bullet]
- [bullet]

Relevant files:
- [path]
- [path]
- [path]

Relevant methods / entrypoints:
- [type.method]
- [type.method]
- [type.method]

Known flow:
1. [entrypoint]
2. [owner]
3. [downstream effect]

Likely root cause:
[Short hypothesis]

Constraints:
- preserve behavior outside the target fix
- no second source of truth
- keep Core standalone / NSFW additive
- prefer smallest correct patch
- [any task-specific guardrails]

Testing to perform:
- [check]
- [check]
- [check]

Deliverable requested:
- exact patch
- explanation of why this is the smallest correct fix
- note any risks or alternate fixes considered
```

### 22.1 Fast-fill variant

```text
Bug/feature:
Owner:
Change type:
Files:
Entrypoints:
Symptom:
Expected:
Hypothesis:
Constraints:
Tests:
```

### 22.2 Handoff rule

Do the repo triage first, then hand off only the smallest relevant file set and flow summary. Do not paste the entire repo into another AI unless the task genuinely spans multiple owners.

---

## 23. Spec maintenance contract

This spec should be updated whenever a change modifies any of the following:
- ownership of a subsystem or runtime state
- startup/bootstrap order
- preview/live/render flow
- face rendering or facial animation contracts
- HeroKarma event, runtime, tuning, or UI ownership
- UIFramework layout rules or promoted reusable controls
- Core ↔ NSFW boundaries or settings ownership
- compatibility/hook strategy
- edit-start guidance for common tasks
- testing expectations for risky seams

### 23.1 When an update is required

Update `DESPICABLE_SPEC.md` in the same change set when a PR, patch, or feature does any of the following:
1. adds a new subsystem or major folder with clear ownership
2. moves ownership from one module to another
3. introduces a new runtime reset owner or shared mutable cache
4. changes a key entrypoint, bootstrap path, or preview/render contract
5. adds a new reusable UI pattern to UIFramework
6. changes Core/NSFW dependency assumptions
7. changes the recommended edit map for a common bug/feature class

### 23.2 When an update is optional

A spec update is usually not required for:
- local bug fixes that do not change ownership or flow
- small internal refactors within an already documented owner
- purely cosmetic UI polish that does not change framework patterns
- tuning-only number changes
- localization-only content additions without architectural impact

### 23.3 Definition of done

A feature or architecture-affecting change is not fully done until all three are true:
1. code is updated
2. relevant smaller docs/checklists are updated if their contract changed
3. `DESPICABLE_SPEC.md` still accurately describes the owner, flow, and edit-start path

### 23.4 Required PR / change checklist snippet

Copy this into your workflow notes, PR template, or release checklist:

```text
Spec impact check:
- [ ] Does this change alter ownership, flow, bootstrap, preview/render behavior, UI framework patterns, or Core/NSFW boundaries?
- [ ] If yes, did I update DESPICABLE_SPEC.md?
- [ ] Did I update any smaller docs that this spec points to?
- [ ] Does the Edit Map still point to the right starting files?
- [ ] Did I note any new danger zone or testing requirement?
```

### 23.5 Practical maintenance rule

Prefer small frequent updates over rare giant rewrites. If a change affects the map, update the map while the change is still fresh. A stale spec becomes decorative cardboard very quickly.

