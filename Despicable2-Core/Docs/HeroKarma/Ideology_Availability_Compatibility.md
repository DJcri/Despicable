# Ideology Availability Compatibility

This pass makes HeroKarma behave cleanly when the Ideology expansion is not active.

## Intended behavior without Ideology

- Cosmic Karma remains available.
- Local Reputation remains available.
- Ideology Standing mechanics do not run.
- Ideology Standing UI surfaces are hidden from the main HeroKarma window.
- Standing-related settings and migration toggles are not shown in Mod Settings.
- Standing patches are skipped cleanly instead of logging missing-target noise.

## Shared gate

Use `HKIdeologyCompat` as the single source of truth for Ideology availability and Standing feature usability. Avoid ad hoc `ModsConfig.IdeologyActive` checks in scattered UI files.

## Validation points

1. Launch without Ideology active.
2. Open HeroKarma and confirm there is no Standing tab.
3. Confirm the header and overview show Karma only.
4. Confirm Mod Settings shows a short Ideology-unavailable note instead of active Standing toggles.
5. Confirm Karma and Local Reputation still function normally.
6. Confirm same-ideology opinion/certainty patches are skipped without error spam.
