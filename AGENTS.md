# Repository guardrails for humans and AI

## Companion file note

- `AGENTS.md` is the quick-reference guardrail sheet.
- `AGENT.md` is the longer policy version with fuller rationale and state-ownership rules.
- Keep both files aligned when one changes.

This repository is optimized for narrow, subsystem-local changes.
These rules exist to keep feature work from quietly creating future refactor debt.

## Default implementation policy
- UI Framework changes must update `Despicable2-Core/Docs/UIFramework_RulesOfTheRoad.md` and `Despicable2-Core/Docs/UIFramework_Cookbook.md` in the same pass.
- Repeated UI patterns should be promoted to a small UIFramework helper/blueprint (additive, measure-safe) instead of copy-paste.
- Prefer the smallest practical change that solves the problem.
- Prefer extending an existing subsystem seam before inventing a new pattern.
- Keep changes local to the owning module whenever possible.
- Preserve existing public entry points when refactoring internals.
- If a change increases future refactor risk, call it out before widening scope.


## Localization
- Any player-facing C# text must be `"Key".Translate(...)` with the key defined in `Languages/English/Keyed/*.xml`.
- Def changes to `label`/`description`/`reportString` must keep English DefInjected mirrors in sync (audit auto-adds missing).
- Core must not rely on NSFW language files.
- Build runs the shared `Tools/LocalizationAudit.ps1` after build (skip via `D2SkipLocalizationAudit=true` temporarily only).
- Core-only: keep NSFW interaction icons and `UI/Interaction/...` references out of Core.

## Placement rules
- Animation Studio UI changes belong in `Despicable2-Core/Source/Despicable/AnimModule/AnimGroupStudio/UI`.
- Animation Studio model/export changes belong in the AGS `Model` or `Export` folders.
- Face parts UI changes belong in `Despicable2-Core/Source/Despicable/FacePartsModule/UI`.
- External mod support belongs in compatibility / integrations folders, not general utility folders, using runtime detection and guarded registration.
- Hero Karma logic belongs in `HeroKarmaModule`, and Harmony patches belong in `HeroKarmaModule/Patches`.

## State ownership rules
- Do not add new broad mutable static state unless there is a clear lifecycle reason.
- Window state should be owned by the relevant window/session object.
- Gameplay runtime state should be owned by the relevant runtime path, not a convenience global.

## Error handling rules
- Do not add empty `catch` blocks.
- If a failure is non-fatal, log once and continue.
- If a reflection or Harmony hook can fail, log enough context to make the failure diagnosable.

## Refactor rules
- Large files are a review trigger, not an automatic split order.
- Split only where there is a real responsibility boundary.
- Avoid decorative rewrites that make code look cleaner on paper but widen risk.
- Prefer additive wrappers/adapters over broad destructive rewrites.

## Change review checklist
Before landing a change, check:
1. Does it fit one subsystem cleanly?
2. Does it reuse an existing seam or pattern?
3. Does it avoid new mutable static state?
4. Will failure be visible in logs if the behavior is non-obvious?
5. Can the change be rolled back cleanly if needed?

## High-risk zones
These areas are easy to destabilize and should be changed only for a concrete reason:
- broad startup/bootstrap wiring
- Harmony patch target discovery
- shared utility hubs used across many modules
- cross-assembly contracts between Core and NSFW

## Localization guardrails
- Keyed rule: every Translate() key must exist in Languages/English/Keyed/*.xml.
- Unused Keyed keys are pruned unless kept in Tools/LocalizationKeepKeys.txt.
- DefInjected mirrors are auto-generated for English.

- DefInjected orphan rule: DefInjected nodes whose defName no longer exists in Defs are pruned from D2_NSFW_LovinTypeDef_DefInjected.xml and must not exist in other DefInjected files unless whitelisted in Tools/DefInjectedKeep.txt.
- Avoid `->` navigation arrows and raw `<` / `>` characters in translation strings (they can trigger RimWorld translation load errors). Use `→` or `/` instead.
- Guardrails also validate: duplicate keys and malformed `{0}` placeholders/braces.
