# Despicable Smoke Test Policy

This policy defines the minimum smoke-test expectations for normal feature work.
It complements the architectural guardrails.
It does not replace judgment, and it is not a demand for exhaustive automated testing.

The goal is to keep a small, reliable smoke suite that catches:
- startup drift
- hook drift after game updates
- regressions in risky runtime seams
- breakage introduced by behavior-preserving refactors

## Hybrid model
The project uses a hybrid smoke model.
That means the smoke suite should keep coverage in three buckets:

- **Startup diagnostics**: assembly load, startup wiring, Harmony ownership, asset / packaging sanity.
- **Hook health**: critical reflection / Harmony target existence and ownership checks.
- **Runtime behavior**: a small set of canary checks for the highest-risk live behavior paths.

Do not try to turn the smoke suite into a full integration test framework.
Prefer a few high-value checks that are stable and cheap to run.

## When smoke coverage must be reviewed
Review smoke coverage whenever a change does one or more of the following:

- changes bootstrap, startup wiring, or assembly registration
- changes Harmony target registration, reflection targets, or patch ownership
- changes a risky runtime seam (animation playback, render-node generation, save/load restoration, compatibility bridges, queue progression, state reset)
- changes a public contract while intending behavior to stay the same
- removes or retires a feature that already has smoke coverage

## Required action by change type
### Startup / wiring changes
- Add or update startup diagnostics when bootstrap ownership, assembly loading, or packaging assumptions change.

### Hook / patch changes
- Add or update hook-health checks when critical patch targets move, are renamed, or change registration behavior.

### Runtime behavior changes
- Add or update at least one runtime smoke check when changing a risky behavior seam.
- Prefer checks that verify the smallest meaningful contract, such as start, reset, queue advance, render-node build, or post-load restore.

### Refactors
- If a refactor preserves behavior but changes internal seams, existing smoke checks must still pass or be updated to validate the new seam.
- Use refactor-adjacent smoke checks to protect the new boundary, not just the old implementation details.

### Feature removal
- Remove, retire, or replace smoke checks that no longer map to a live feature.
- Do not keep dead smoke checks as cargo cult artifacts.

## Practical selection rule
When adding or updating smoke coverage, prefer this order:
1. Keep startup diagnostics current.
2. Keep hook-health checks current.
3. Add runtime behavior checks for the top 1 to 3 highest-risk paths touched by the change.
4. Favor stable contract checks over broad scenario simulations.

## Organization
- Keep the central orchestration runner in Core.
- Keep module-specific checks near the module they validate.
- Keep debug actions as thin entry points that delegate into the harness.
- Log pass / warn / fail per check and a final summary for grouped runs.

## Current expectation
The suite should always have:
- startup diagnostics coverage
- hook-health coverage for critical modules
- runtime behavior checks for the most recent high-risk refactors or systems

## Enforcement
The current guardrail checker does not automatically enforce this policy.
For now, this policy is enforced by review discipline and refactor checklists.
If the workflow proves useful, lightweight checker support can be added later.
