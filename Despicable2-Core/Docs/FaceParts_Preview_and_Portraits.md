# Face parts preview and portrait notes

This note exists because recent face work touched three preview surfaces at once:

- portrait warmup
- face customizer preview
- head blacklist preview tiles

They all look like "just UI," but they do not share the exact same render path.

## Ownership

Primary owners:

- `FacePartsModule/Comp/CompFaceParts.cs`
- `FacePartsModule/Comp/CompFaceParts.Refresh.cs`
- `FacePartsModule/UI/Dialog_D2FacePartsCustomizer.cs`
- `FacePartsModule/UI/FacePreviewCache.cs`
- `ModMain.cs` (`HarmonyPatch_PortraitsCache_Get_DespicableFaceWarmup`)
- `FacePartsModule/Compatibility/PawnEditorCompat/HarmonyPatch_PawnEditor_AppearanceEditor.cs`

## Portrait warmup

Portrait consumers can request a pawn render before normal face state has fully warmed.

Current contract:

1. `PortraitsCache.Get(...)` enters a face-aware prefix patch.
2. The patch skips out early unless face warmup is actually needed.
3. `CompFaceParts.TryWarmPortraitFastThisTick(...)` does the lightest safe warmup for that render tick.
4. Portrait rendering then continues inside a face-portrait render scope.

Practical rule:

- Do not replace this with broad always-dirty behavior.
- Warm the face state first, then let the portrait system render.

## Face customizer preview

`Dialog_D2FacePartsCustomizer` supports multiple preview modes, including a live workshop-style preview and an isolated composite fallback.

Recent behavior that matters:

- preview pawns are explicitly dirtied before portrait-style rendering
- texture selection for the isolated composite preview flows through `FacePreviewCache`
- the customizer now depends on preview-safe texture lookup rather than assuming every head or face-part texture is already in a portrait-ready state

When editing this area:

- treat preview rendering and face-state setup as one seam
- keep style assignment / face refresh ahead of the actual preview draw
- do not assume the fallback composite preview and the live render-tree preview share the same failure modes

## Head blacklist previews

The blacklist manager now relies on `FacePreviewCache` for head previews.

That cache does two important jobs:

- resolves head textures and face-part textures without forcing gameplay state changes
- crops preview textures to visible content so head tiles are readable instead of mostly empty transparent space

Practical rule:

- if blacklist previews look wrong, start in `FacePreviewCache` before changing the blacklist dialog layout

## Detached preview pawns

Some preview pawns are synthetic and unspawned.
That means normal per-tick face initialization may never fire on its own.

Current safety pattern:

- assign styles first
- refresh face state second
- only then render the preview pawn

This is especially important for eyes and mouth because null style state can silently prevent render-node setup.

## Pawn Editor compatibility

Pawn Editor integration should stay layout-safe.

Current rule from the recent fix:

- treat the postfix `inRect` as the remaining gap
- center the added button inside that remainder
- do not rebuild the left-section layout with hardcoded offsets

That keeps the button aligned with Pawn Editor's own layout instead of fighting it.

## Quick regression list

After touching these files, manually check:

- portraits render with face parts on first open
- face customizer preview shows eyes, brows, mouth, and eye detail layers
- blacklist preview tiles render cropped readable heads
- Pawn Editor still shows the injected button in the expected gap
