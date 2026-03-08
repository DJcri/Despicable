# Hero Karma balance hub

The authoritative internal balance file is now:

- `Source/Despicable/HeroKarmaModule/Tuning/HKBalanceTuning.cs`

Edit that file to rebalance:

- karma gain/loss values
- ideology standing thresholds and effect strength
- local reputation gain/loss values
- local reputation gameplay coefficients
- perk passive stat offsets
- perk-specific bonus behavior numbers

## Notes

- `LocalRepTuning.cs` now forwards to `HKBalanceTuning.cs` for compatibility.
- `HK_PerkHediffs.xml` is no longer the source of truth for perk stat offsets at runtime.
  The game applies the offsets from `HKBalanceTuning.cs` after defs load.
- Perk UI tooltips also pull their numbers from `HKBalanceTuning.cs`.

## Important

This package contains the source refactor, but the shipped `1.6/Assemblies/Despicable.dll`
was not rebuilt inside this environment.

To use the new balance hub in-game, rebuild the mod assembly from source and replace the DLL.

## Current local-reputation tuning intent

- **Faction echo** stays the strongest shared layer.
- **Settlement word-of-mouth** is live, but lighter.
- Settlement echo does **not** force a minimum ±1 contribution from tiny settlement scores.
  A place should usually need a few visible events before its word-of-mouth starts affecting everyone there.
- Passive **pawn -> settlement** accumulation remains off for now so the current checkpoint stays easy to read.
  Most local word-of-mouth is still authored by explicit public-order and civic events.
