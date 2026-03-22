# Legacy Save Bridge

This module exists only so old Despicable NSFW saves from the Steam Workshop build can load inside the new anatomy framework.

What it ships:
- `D2_Genital_Penis`
- `D2_Genital_Vagina`
- lazy lookup helpers and on-load migration code

What it does not do:
- no body-tree injection
- no rendering
- no new author-facing workflow

Load flow:
1. old save references deserialize because the legacy defs still exist inside NSFW
2. `GameComponent_LegacyAnatomyMigration` sweeps loaded pawns
3. legacy penis/vagina hediffs migrate into anatomy part instances
4. legacy hediffs are removed by def anywhere on the pawn

This module is intentionally isolated under `Source/LovinModule/LegacyBridge` and `Defs/LovinModule/LegacyBridge` so it can be removed cleanly after old saves have been migrated forward.
