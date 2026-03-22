# Manual Regression Checklist

Use this quick pass after structural changes.

## Build
- Build `Despicable2-Core`
- Build `Despicable2-NSFW`

## Core startup
- Launch RimWorld with Core enabled
- Confirm Core bootstrap logs once
- Confirm no Harmony patch-failed spam during startup

## Anim Group Studio
- Open Anim Group Studio
- Open an existing project
- Create or rename one stage or role
- Preview play / stop
- Scrub to a tick and compare it against live preview at the same moment
- Confirm detached preview pawns render instead of pulling live colony pawns into preview casting
- Export once

## Face Parts
- Open the face head blacklist manager
- Confirm head previews render as readable cropped heads, not mostly empty transparent tiles
- Open the face customizer and confirm preview layers render for eyes, brows, mouth, and eye detail
- Confirm portraits with face parts render cleanly on first open
- Toggle one blacklist state and save

## HeroKarma
- Trigger one HeroKarma-related interaction path
- Confirm logs remain clean
- Test one **non-guilty** arrest or neutral attack path and confirm the normal negative event still appears
- Test one **guilty** arrest or neutral attack path and confirm the event is suppressed
- Test one guilty harsh-action path (guest harm, downed kill, or prisoner execution) and confirm penalties are reduced rather than erased
- In a player colony, harm an innocent guest and confirm nearby colony pawns inherit the shared local word-of-mouth through the colony settlement lane

### HeroKarma broader ideology sweep
- Test one **EnslaveAttempt** on a non-player settlement map and confirm settlement word-of-mouth records exactly one explicit settlement hit
- Test one **SellCaptive** trade at a world settlement and confirm settlement word-of-mouth records the sale without duplicate echo
- Confirm new sale events present as **SellCaptive / Sold captive** rather than prisoner-only wording
- Test one **OrganHarvest** with `OrganUse_Abhorrent` and one with `OrganUse_Acceptable` to confirm Reputation ideology traces now react on the pawn-local lane
- Test one `Loyalist` ideology on `ArrestNeutral` and one `Guilty` or `PainIsVirtue` ideology on a harsh-punishment path to confirm meme baselines are active when no stronger exact precept overrides them
- Test one `Loyalist` ideology on `EnslaveAttempt` or `SellCaptive` to confirm the broader order-positive meme baseline now softens coercion/slavery Reputation penalties
- Test one guilty `HarmGuest` path under `Guilty`, `PainIsVirtue`, or `Loyalist` and confirm the trace treats it as a guilt-aware harsh-punishment case rather than an ideology-neutral betrayal case
- With an ideoligion using **Charity_Important**, test one charity action and confirm the trace resolves the exact precept
- With an ideoligion using **Charity_Worthwhile**, repeat the charity action and confirm the trace resolves the exact precept
- With an ideoligion using **Slavery_Classic**, test one slavery-related action and confirm Standing/Reputation ideology logic resolves from the exact precept family
- With an ideoligion using **Execution_HorribleIfInnocent**, test one non-guilty execution path and confirm the trace shows `guilt=innocent`
- With an ideoligion using **Execution_RespectedIfGuilty**, test one guilty execution path and confirm the trace shows `guilt=guilty`
- In Dev Mode or with HeroKarma debug UI enabled, inspect one ideology-sensitive event and confirm the detail text appends an `Ideology:` explanation line
- In diagnostics, confirm an `IdeologyTrace` line appears for one ideology-sensitive event and includes semantic, matched precept, and final lane deltas
- On a non-player settlement map, trigger multiple visible local-reputation events and confirm settlement word-of-mouth starts affecting local pawn reputation only after local score has built up
- For detailed expected outcomes, use `Docs/HeroKarma/Ideology_Validation_Sweep.md`

## NSFW
- Launch with NSFW enabled
- Confirm NSFW initializes once
- Confirm optional integrations register cleanly
- Trigger one basic interaction path
- Confirm manual and autonomous lovin both block blood-related pawns by default
- With Birds of a Feather installed, confirm the related-pawn gate automatically allows those pairings without any player-facing toggle
- In Dev Mode, open **Anatomy Debug** on one pawn and confirm parts, fluids, anchors, and coverage data render
- Confirm natural anatomy no longer shows up as normal permanent health-tab clutter after migration
- If GenderWorks is installed, trigger one anatomy-relevant change and confirm logical anatomy resync still drives rendering / staging correctly

## Harmony-sensitive gameplay
- Test prisoner release
- Test prisoner execution
- Test organ harvest

## Final sanity
- Load an existing save
- Play briefly without red errors

## No-Ideology compatibility

- Launch without Ideology active.
- Confirm HeroKarma hides the Standing tab.
- Confirm the header and overview omit Standing meters.
- Confirm Mod Settings shows Ideology Standing as unavailable instead of active toggles.
- Confirm Karma and Local Reputation still function normally.
- Confirm no Standing patch error spam appears on startup.

## Final freeze / release-candidate checks

- Confirm new live captive-sale events emit **SellCaptive** rather than **SellPrisoner** in diagnostics and UI detail text
- Confirm no-Ideology mode hides Standing surfaces while Karma and Reputation still function
- Confirm the remaining `SellPrisoner` references are only intentional legacy aliases or validation notes
- Confirm HeroKarma docs include the release freeze note and the UIFramework conformance audit
