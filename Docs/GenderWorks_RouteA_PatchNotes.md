# Gender Works Route A anatomy tracker patch

This source bundle migrates natural genital anatomy away from visible permanent hediffs and into hidden pawn state stored on `CompAnatomyBootstrap`.

## What changed
- Added hidden serialized anatomy state to `CompAnatomyBootstrap`
- `AnatomyQuery` is now tracker-first for logical anatomy
- `AnatomyBootstrapper` now seeds hidden state from:
  1. legacy D2 genital hediffs
  2. Gender Works signals
  3. visible gender fallback
- legacy normal-anatomy hediffs (`D2_Genital_Penis`, `D2_Genital_Vagina`) are removed after migration and are no longer used as the authoritative runtime source
- render, staging, and repro compatibility paths now read logical anatomy instead of the visible hediff layer
- existing D2 body-part anchor injection remains for structure / surgery targeting without treating normal anatomy as health UI content

## Expected result
- normal anatomy should stop showing as health-tab clutter once the rebuilt DLL is loaded and pawns are migrated
- Gender Works runtime animation and genital rendering should continue to work through hidden logical anatomy state
- old saves should migrate automatically on load/spawn via `CompAnatomyBootstrap`

## Important
This bundle updates source only. Rebuild `DespicableNSFW.dll` from `Despicable2-NSFW/Source/DespicableNSFW.csproj` before testing.
