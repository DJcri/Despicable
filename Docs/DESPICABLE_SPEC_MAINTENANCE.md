# DESPICABLE Spec Maintenance Contract

This file exists to keep `DESPICABLE_SPEC.md` alive instead of turning it into stale wallpaper.

## Update the spec whenever a change modifies:
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

## Required update triggers

Update `DESPICABLE_SPEC.md` in the same change set when a patch, feature, or refactor does any of the following:
1. adds a new subsystem or major folder with clear ownership
2. moves ownership from one module to another
3. introduces a new runtime reset owner or shared mutable cache
4. changes a key entrypoint, bootstrap path, or preview/render contract
5. adds a new reusable UI pattern to UIFramework
6. changes Core/NSFW dependency assumptions
7. changes the recommended edit map for a common bug or feature class

## Usually optional

A spec update is usually not required for:
- local bug fixes that do not change ownership or flow
- small internal refactors within an already documented owner
- purely cosmetic UI polish that does not change framework patterns
- tuning-only number changes
- localization-only content additions without architectural impact

## Definition of done

A feature or architecture-affecting change is not fully done until all three are true:
1. code is updated
2. relevant smaller docs/checklists are updated if their contract changed
3. `DESPICABLE_SPEC.md` still accurately describes the owner, flow, and edit-start path

## PR / change checklist snippet

```text
Spec impact check:
- [ ] Does this change alter ownership, flow, bootstrap, preview/render behavior, UI framework patterns, or Core/NSFW boundaries?
- [ ] If yes, did I update DESPICABLE_SPEC.md?
- [ ] Did I update any smaller docs that this spec points to?
- [ ] Does the Edit Map still point to the right starting files?
- [ ] Did I note any new danger zone or testing requirement?
```

## Practical rule

Prefer small frequent updates over rare giant rewrites. If a change affects the map, update the map while the change is still fresh.
