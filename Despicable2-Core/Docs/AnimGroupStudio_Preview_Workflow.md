# Anim Group Studio preview workflow

This note captures the current preview model for Anim Group Studio.

## Core idea

AGS preview is an **authoring-time sampling environment**, not normal gameplay playback.

That means the preview path is allowed to use synthetic pawns and workshop-scoped render state as long as it stays isolated from live save data.

## Ownership

Main files:

- `AnimModule/AnimGroupStudio/Preview/AgsPreviewPawnPool.cs`
- `AnimModule/AnimGroupStudio/Preview/IDetachedPreviewPawnInitializer.cs`
- `AnimModule/UI/PreviewPawnFactory.cs`
- `AnimModule/UI/WorkshopPreviewRenderer.cs`
- `AnimModule/AnimGroupStudio/Preview/AgsPreviewSession*.cs`
- `WorkshopRenderContext.cs`

## Preview pawn rule

AGS should use **detached generated preview pawns**.
It should not borrow live colony or world pawns for studio casting.

Current behavior:

- `AgsPreviewPawnPool` creates clean preview pawns through `PreviewPawnFactory`
- those pawns are owned by the preview pool and destroyed on cleanup
- preview-role assignments are reused only when the generated pawn still matches the requested role shape

## Why detached preview pawns need extra init

Detached preview pawns skip some normal gameplay lifecycle and tick paths.
That is why AGS now has narrow preview-specific interfaces:

- `IDetachedPreviewPawnInitializer`
- `IDetachedPreviewPawnMirrorFromSource`

Use them when a comp needs preview-only setup or source-state mirroring.
Do not expand them into a second general gameplay bootstrap.

## Face and anatomy preview alignment

Recent preview fixes depend on best-effort comp initialization for detached pawns.

Examples:

- face parts need style assignment plus face refresh before the first render
- anatomy can mirror resolved hidden anatomy from a source pawn, or seed a preview pawn from current gender when no source mirror is available

This is the current pattern to preserve:

1. create preview pawn
2. mirror preview-aware comp state from source when available
3. otherwise run narrow detached-preview initialization
4. dirty graphics if the comp owns visible render state
5. render inside workshop preview context

## Workshop preview renderer

`WorkshopPreviewRenderer` owns the persistent render texture used by scrub and play preview.

Important rules:

- resize the RT only when dimensions change
- render through workshop context-aware helpers
- keep preview rendering separate from normal portrait assumptions
- shared multi-pawn rendering may stack draws into one RT, so treat camera clear behavior as preview-sensitive

## Scrub and live parity

For a given stage and tick, scrub and live preview should agree.

If they diverge, check these seams first:

- stage-local tick reset
- workshop preview scope / tick state
- preview comp initialization not happening on detached pawns
- graphics not being dirtied after preview-only visible-state changes

## Cleanup rule

Preview-owned pawns are disposable editor resources.
The preview pool is responsible for cleaning them up.
Do not let AGS preview pawns drift into broader runtime ownership.
