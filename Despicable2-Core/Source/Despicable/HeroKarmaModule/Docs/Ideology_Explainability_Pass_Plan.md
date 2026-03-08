# HeroKarma Ideology Explainability Pass Plan

Status: Implemented, initial dev-trace pass  
Scope: HeroKarma ideology evaluation, debugging, and event-detail explanation  
Last updated: 2026-03-06

## Purpose

The next recommended HeroKarma pass is an **ideology explainability pass**.

## Implementation note

The initial implementation now exists in source form:

- a shared `HKIdeologyEvaluationTrace` rides on `KarmaEvent`
- Reputation writes semantic, matched precept, modifier mode, and final direct pawn-local delta
- Standing writes issue key, exact matched precept, inferred score, and final standing delta
- explicit settlement-local writes add their final settlement delta into the same trace
- when RimWorld Dev Mode or the HeroKarma debug UI is active, event detail text appends a compact `Ideology:` explanation line
- when HeroKarma diagnostics are enabled, logs emit a greppable `IdeologyTrace` line

This is intentionally a developer-facing first pass rather than a broad player-facing UI rewrite.


We already have two important mechanics in place:

- settlement-local reputation writes for coercive events such as `EnslaveAttempt` and `SellCaptive`
- unified exact-precept handling for ideology-sensitive evaluation so Reputation and Ideology Standing read from the same supported precept catalog

That means the next risk is no longer missing structure. The next risk is **silent mis-tuning**.

When an event is evaluated, a player or developer should be able to answer:

- which event semantic was resolved
- whether guilt / innocence affected the outcome
- which exact ideology precept matched, if any
- which ideology modifier was applied
- which result lane changed: Reputation, Ideology Standing, settlement reputation, or multiple lanes

This pass is meant to make the system legible before we broaden ideology depth further.

## Why this pass should happen before broader ideology expansion

Future ideology work will likely include one or both of these:

- expanding supported exact-precept families
- adding small meme-level baseline effects for carefully chosen memes such as `Loyalist`, `Guilty`, and `PainIsVirtue`

Both become safer once the current system can explain itself.

Without explainability, balancing and bug-hunting become guesswork. With explainability, a strange reaction can be traced to a specific stage instead of being treated like a mysterious final number.

## Core design goal

For each ideology-sensitive event, the system should be able to produce a compact explanation trail that answers:

1. What event meaning did we resolve?
2. What context flags mattered?
3. What ideology rule matched?
4. What math did we apply?
5. What final deltas did we write?

This explanation must be **developer-friendly first** and **player-safe second**.

## Recommended outputs

### 1) Dev-mode explanation trail

Add a compact dev-facing explanation trail that can be emitted when HeroKarma diagnostics or RimWorld dev mode is active.

Suggested content per evaluated event:

- event key
- resolved ideology semantic
- target guilt state, where relevant
- exact matched precept defName, or `none`
- ideology modifier chosen
- final Reputation delta
- final Standing delta
- final settlement delta, if present
- whether any lane was intentionally skipped

Suggested tone:

- technical
- concise
- deterministic
- easy to grep in logs

### 2) Optional UI detail text / tooltip support

Expose a reduced explanation in the HeroKarma event detail surface so in-game inspection is possible without opening logs.

Suggested content for UI detail:

- semantic label
- guilt-aware label when applicable
- ideology rule matched, if any
- short note such as `Ideology modifier applied` or `No exact ideology rule matched`

The UI should avoid flooding the player with raw debug internals. It should explain the decision, not dump the entire spreadsheet.

## Recommended implementation shape

### A. Central explanation model

Create a small internal record or data object representing ideology evaluation details.

Suggested fields:

- `EventKey`
- `Semantic`
- `WasGuiltyContext`
- `MatchedPreceptDefName`
- `IdeologyModifier`
- `ReputationDelta`
- `StandingDelta`
- `SettlementDelta`
- `Notes`

This should be plain data first. Rendering can happen later.

### B. Populate it at the source of truth

Prefer populating the explanation trail close to the actual ideology evaluation logic rather than reconstructing it afterward.

Primary candidates:

- `Source/Despicable/HeroKarmaModule/Pipeline/HKKarmaProcessor.Helpers.cs`
- `Source/Despicable/HeroKarmaModule/IdeologyRep/HKRepIdeology.cs`
- `Source/Despicable/HeroKarmaModule/Services/ApprovalResolver.Ideology.cs`

The goal is to explain **the actual branch taken**, not an approximation generated later.

### C. Keep Reputation and Standing aligned

Because exact-precept support now runs through shared ideology-precept helpers, the explanation layer should preserve that same alignment.

If Reputation and Standing ever disagree, the explanation must make the split obvious.

Examples:

- Reputation changed, Standing did not
- Standing changed, Reputation did not
- settlement-local write occurred in addition to direct pawn-local write

## Specific cases this pass should make easy to verify

The explanation trail should make it immediately obvious how these cases are being interpreted:

- `Charity_Important`
- `Charity_Worthwhile`
- `Slavery_Classic`
- `Execution_HorribleIfInnocent`
- `Execution_RespectedIfGuilty`

For execution events in particular, the trail should clearly expose whether the target was treated as guilty or innocent for the purposes of ideology evaluation.

## Recommended file touch targets

This pass will likely affect some combination of the following files:

- `Source/Despicable/HeroKarmaModule/Pipeline/HKKarmaProcessor.Helpers.cs`
- `Source/Despicable/HeroKarmaModule/Pipeline/KarmaEvent.cs`
- `Source/Despicable/HeroKarmaModule/IdeologyRep/HKRepIdeology.cs`
- `Source/Despicable/HeroKarmaModule/Services/ApprovalResolver.Ideology.cs`
- `Source/Despicable/HeroKarmaModule/Dev/HKDiagnostics.cs`
- `Source/Despicable/HeroKarmaModule/UI/Dialog_HeroKarma.Helpers.cs`
- `Source/Despicable/HeroKarmaModule/UI/Tab_LocalRep.Sections.cs`
- `Source/Despicable/HeroKarmaModule/UI/Tab_Standing.cs`

Not every file needs to change. The point is to keep the explanation close to the evaluated result and surface it where it is useful.

## Acceptance criteria

This pass is successful if all of the following are true:

1. A developer can inspect an event and see the resolved ideology semantic.
2. A developer can tell whether guilt status changed the result.
3. A developer can identify the exact matched precept, or know that none matched.
4. Reputation and Standing ideology decisions can be compared without guesswork.
5. Settlement-local side effects are visible when they occur.
6. The system remains quiet and unobtrusive outside dev-facing surfaces or compact detail views.

## Out of scope for this pass

To keep the work surgical, the following should stay out of scope unless needed for a compile-safe implementation:

- adding broad meme-baseline ideology behavior
- adding large new event families
- rewriting the HeroKarma UI structure
- changing balance values unless explanation reveals an obvious bug

## Recommended follow-up after this pass

The immediate follow-up is now a **broader validation sweep** using the new trace surfaces.

See also:

- `Source/Despicable/HeroKarmaModule/Docs/Ideology_Validation_Sweep.md`

That sweep should validate `EnslaveAttempt`, `SellCaptive`, the charity precepts, `Slavery_Classic`, and the guilt-aware execution cases before HeroKarma broadens ideology depth again.

Once explainability is in place and verified in-game, the next recommended feature step is a **small meme-baseline ideology pass** for a tightly curated set of memes.

Initial candidates already identified from available defs:

- `Loyalist`
- `Guilty`
- `PainIsVirtue`

That follow-up should remain conservative. Memes should set the background climate, while exact precepts remain the main legal code.

## Summary

The explainability pass is the next recommended action because it turns ideology-sensitive HeroKarma behavior into a glass box instead of a sealed crate.

That gives future tuning a map instead of a fog bank.
