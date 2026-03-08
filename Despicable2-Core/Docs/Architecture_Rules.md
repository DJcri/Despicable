# Despicable Architecture Rules

These are the guardrails meant to keep the codebase maintainable as it grows.
Use them for normal feature work, not just large refactors.

The goal is not to make files arbitrarily smaller.
The goal is to keep ownership, failure modes, and responsibility boundaries obvious.

## Core rules
- No empty `catch` blocks unless the ignore is intentional and documented.
- Prefer logging once over swallowing exceptions silently.
- Do not introduce new giant mixed-responsibility classes. Split only when there is a real responsibility boundary.
- Mutable runtime state should not live in broad static globals unless there is a clear justification.
- New optional-integration code belongs in compatibility / integration folders, not general utility folders.
- Namespace should match subsystem and layer whenever practical.
- Favor thin bootstraps that delegate to shared startup helpers.

## Optional integration model
- Optional mod support uses runtime detection, guarded registration, and soft-fail behavior.
- Use small integration modules for startup-time registration or warmup work, then use runtime guards at call sites before touching optional behavior.
- Keep reflection and Harmony integration code isolated by mod so failures stay diagnosable.
- Do not reintroduce load-folder-based integration paths or alternate assembly loading for mod support.

## Feature implementation rules
- Prefer the smallest practical change that solves the problem.
- Prefer extending an existing seam over inventing a one-off pattern.
- Keep new code inside the narrowest owning subsystem.
- Preserve stable public entry points when refactoring internals.
- Treat Harmony and reflection code as live-wire areas: verify target shape, log failures, and avoid brittle assumptions when a safer hook exists.
- Split files when they have more than one real reason to change, not because they tripped an aesthetic line count.
- Prefer separating policy from mechanics when a file starts mixing both.
- Avoid decorative helper extraction that only redistributes code without clarifying ownership.

## State ownership rules
- Window/editor state should be instance-owned.
- Module runtime state should be explicit about lifecycle.
- Do not add new mutable static state for convenience alone.
- If a mutable static is intentional, document the owner and lifecycle near the declaration.

## Smoke test policy
- Follow `Docs/Smoke_Test_Policy.md` for smoke-test expectations.
- Startup, hook-health, and high-risk runtime seams should keep small, current smoke coverage.
- Refactors that preserve behavior should keep existing smoke checks passing or update them to validate the new seam.
- Retired features should remove or replace obsolete smoke coverage.

## Practical heuristics
- Large files are a review trigger, not an automatic split order.
- Files over 300 lines enter review territory: check for a clean responsibility split before they grow further.
- Files over 400 lines need a written reason to stay whole during the current pass.
- Files over 500 lines are mandatory refactor candidates unless they sit on a risky engine seam that needs runtime validation first and the reason is documented.
- Keep public entry points stable when refactoring internals.
- Prefer local fixes with clear ownership over broad decorative rewrites.
- For UI layout, prefer framework-driven measurement, style tokens, and remainder-rect carving over hardcoded fit numbers or screenshot-tuned offsets.
- If a UI change appears to require hardcoded positioning or sizing for fit, stop and document the constraint before landing it.
- When a refactor makes the code look cleaner on paper but adds hidden fragility, stop and reconsider.
- If a change touches more than one subsystem, look for an adapter, hook, or compat seam before widening the edit.

## Guardrail annotations
Use these sparingly when a file or field is intentionally outside the default heuristic:

- `// Guardrail-Reason: <why this file stays whole for now>`
- `// Guardrail-Allow-Static: <owner / lifecycle / why shared state is intentional>`

These are not waivers for convenience. They are breadcrumbs for future-you.

## Vibe-coding safety checks
Before landing a change, ask:
1. Does this fit one subsystem cleanly?
2. Am I reusing an existing pattern or seam?
3. Am I adding new hidden mutable state?
4. Will failure be visible if the behavior breaks?
5. If this file grew, did I create a second reason for it to change?
6. Did this change reduce cognitive load, or just move lines around?


- Documented cohesive exceptions are acceptable. Once a file or static field carries a concrete `Guardrail-Reason` or `Guardrail-Allow-Static`, the checker treats that as satisfied guidance instead of ongoing noise.
