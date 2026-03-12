# HeroKarma Ideology Validation Sweep

Status: Ready for local compile-and-play validation  
Scope: settlement writes, exact-precept ideology reactions, explainability trace review  
Last updated: 2026-03-06

## Purpose

This document is the **broader sweep** that follows the explainability and exact-precept unification passes.

The goal is not to add new mechanics yet. The goal is to prove that the current mechanics are:

- firing on the correct event paths
- writing to the correct lanes
- resolving ideology from the intended exact precepts
- exposing enough trace detail to make tuning trustworthy

## Test order

Run the sweep in this order so the newest plumbing is verified before the broader ideology matrix.

1. `EnslaveAttempt`
2. `SellCaptive`
3. `Charity_Important`
4. `Charity_Worthwhile`
5. `Slavery_Classic`
6. `Execution_HorribleIfInnocent`
7. `Execution_RespectedIfGuilty`

## Pre-test setup

Before running the sweep:

- compile the current source-edited build locally
- enable RimWorld Dev Mode
- enable HeroKarma diagnostics or debug UI, if available
- use a colony / test save where ideology-sensitive actions can be triggered repeatedly without long setup time

## Event-family validation matrix

### 1) `EnslaveAttempt`

Run on a **non-player settlement map**.

Expected behavior:

- pawn-local reputation changes
- faction-local reputation changes
- settlement-local word-of-mouth changes
- no duplicate passive settlement echo on top of the explicit settlement delta
- event detail appends an `Ideology:` line when dev-facing surfaces are active

Expected trace checkpoints:

- `event=EnslaveAttempt`
- `semantic=CoercionSlavery`
- `settlement` is non-zero
- reputation lane is populated if ideology logic evaluates
- standing lane may be populated depending on the observer ideoligion and configured path

Notes:

- this test confirms the settlement-map-parent context is actually making it to the event processor
- if settlement does not move, check context capture before checking ideology math

### 2) `SellCaptive`

Run through a **normal trade execution path at a world settlement**.

Expected behavior:

- selling either a prisoner or a slave through normal trade produces the same unified event family
- pawn-local reputation changes
- faction-local reputation changes
- settlement-local word-of-mouth changes
- no duplicate passive settlement echo
- visible label should read `Sold captive`

Expected trace checkpoints:

- `event=SellCaptive` for newly generated events
- `semantic=CoercionSlavery`
- `settlement` is non-zero
- trace and detail text use captive terminology, not prisoner-only wording

Legacy alias note:

- the code still recognizes legacy `SellPrisoner` references for compatibility
- new live events should prefer `SellCaptive`
- seeing a legacy alias in old data is not automatically a bug; seeing new events emit only the old name would be a rename regression

### 3) `Charity_Important`

Run one charity-positive action with an ideoligion that uses this exact precept.

Expected behavior:

- ideology-sensitive charity reactions resolve from the exact precept, not fuzzy meme guessing
- trace should identify a charity semantic and the matched exact precept defName

Expected trace checkpoints:

- `semantic=Charity`
- `repPrecept=Charity_Important` or compact detail equivalent
- Reputation and/or Standing should move in a way consistent with the configured modifier path

### 4) `Charity_Worthwhile`

Repeat the charity action with this exact precept.

Expected behavior:

- exact-precept matching still occurs
- any difference from `Charity_Important` should be visible in the trace rather than inferred by feel

Expected trace checkpoints:

- `semantic=Charity`
- `repPrecept=Charity_Worthwhile` or compact detail equivalent

### 5) `Slavery_Classic`

Run one slavery-related action that should enter the ideology path.

Expected behavior:

- exact-precept matching resolves from `Slavery_Classic`
- coercion/slavery semantic is used instead of a generic harm bucket

Expected trace checkpoints:

- `semantic=CoercionSlavery`
- `repPrecept=Slavery_Classic` and/or `standingPrecept=Slavery_Classic`

### 6) `Execution_HorribleIfInnocent`

Trigger a **non-guilty** execution path.

Expected behavior:

- guilt-aware execution logic should classify the context as innocent
- ideology reaction should reflect the harsh judgment path

Expected trace checkpoints:

- `semantic=HarshPunishment`
- `guilt=innocent`
- matched precept reflects the innocent-focused execution rule

### 7) `Execution_RespectedIfGuilty`

Trigger a **guilty** execution path.

Expected behavior:

- guilt-aware execution logic should classify the context as guilty
- ideology reaction should reflect the more permissive or respected guilty-punishment path

Expected trace checkpoints:

- `semantic=HarshPunishment`
- `guilt=guilty`
- matched precept reflects the guilty-respected execution rule

## Failure triage map

When a result looks wrong, check in this order:

1. **Event path**
   - Did the intended hook actually fire?
   - Is the event key the one you expected?

2. **Context capture**
   - Did the settlement or guilt context get stamped onto the event?

3. **Semantic resolution**
   - Did the trace resolve to the expected ideology semantic?

4. **Exact-precept match**
   - Did the trace show the exact precept you expected?
   - If not, verify the observer ideoligion truly contains the intended precept in the active test state.

5. **Math / lane application**
   - Was the modifier mode correct?
   - Did the Reputation / Standing / settlement lanes move as expected?

This order matters. A missing settlement delta is often a context problem before it is a balance problem.

## Pass completion criteria

The broader sweep is successful when all of the following are true:

- `EnslaveAttempt` writes exactly one explicit settlement-local delta on the intended path
- `SellCaptive` writes exactly one explicit settlement-local delta on the intended path
- charity, slavery, and execution families resolve from the expected exact precepts
- guilt-aware execution traces clearly distinguish guilty from innocent outcomes
- live detail text and diagnostics make any mismatch easy to localize

## What should happen after this sweep

Only after this validation pass is green should HeroKarma broaden ideology depth again.

Recommended next feature expansion:

- a **small meme-baseline pass** for carefully curated memes such as `Loyalist`, `Guilty`, and `PainIsVirtue`

That expansion should remain conservative. Exact precepts should continue to act as the stronger signal.

## Meme follow-up checks

After the exact-precept sweep passes, run three meme-baseline spot checks:

- `Loyalist` on `EnslaveAttempt` or `SellCaptive` should show `semantic=coercion/slavery` with a `Loyalist_CoercionSlavery` reason when no stronger slavery precept overrides it.
- `Guilty` on a guilty `HarmGuest` case should show `semantic=harsh punishment`, `guilt=guilty`, and a `Guilty_HarmGuest_Guilty` reason.
- `PainIsVirtue` on a guilty `HarmGuest` case should show `semantic=harsh punishment`, `guilt=guilty`, and a `PainIsVirtue_HarmGuest_Guilty` reason.
