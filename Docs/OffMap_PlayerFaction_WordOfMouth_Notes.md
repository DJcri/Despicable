# Off-map colony word-of-mouth fallback

## What changed
- Added a Hero Karma setting: **Allow off-map colony pawns to inherit colony word-of-mouth**
- Added a new shared resolver in `PawnContext` for **word-of-mouth settlement** resolution
- Off-map **player-faction** pawns can now fall back to a player settlement when they need settlement-based local reputation context
- The fallback only affects the **settlement / word-of-mouth lane**
- The **player faction remains blocked at the faction-reputation lane**

## Resolution order
1. Use the pawn's current settlement if they are physically on one
2. If the pawn is player-faction, off-map, and the setting is enabled, fall back to a player settlement
3. If no player settlement can be resolved, skip settlement echo

## Touched files
- `Despicable2-Core/Source/Despicable/Core/Pawns/PawnContext.cs`
- `Despicable2-Core/Source/Despicable/HeroKarmaModule/LocalReputation/LocalReputationUtility.cs`
- `Despicable2-Core/Source/Despicable/HeroKarmaModule/Pipeline/HKKarmaProcessor.Helpers.cs`
- `Despicable2-Core/Source/Despicable/HeroKarmaModule/Patches/HKSettlementContextUtil.cs`
- `Despicable2-Core/Source/Despicable/HeroKarmaModule/Dev/HKSettingsUtil.cs`
- `Despicable2-Core/Source/Despicable/Settings.cs`
- `Despicable2-Core/Source/Despicable/UIFramework/Settings/D2ModSettingsRenderer.cs`
- `Despicable2-Core/Languages/English/Keyed/CodeStrings_UI.xml`

## Important note
This environment patched the **source** only. It did not rebuild the RimWorld DLLs.
