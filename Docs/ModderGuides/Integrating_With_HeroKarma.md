# Integrating with HeroKarma

This is the short guide for mods that want to read or award HeroKarma.

## First: which API should I use?

Almost always use:

- `HeroKarmaBridge`

Only use:

- `IHKBackendBridge`
- `HKBackendBridge.Register(...)`

if you are replacing or supplying the backend itself.

Main files:

- `Despicable2-Core/Source/Despicable/HeroKarmaModule/Bridge/HeroKarmaBridge.cs`
- `Despicable2-Core/Source/Despicable/HeroKarmaModule/Services/IHKBackendBridge.cs`

## Easiest approach

Use `HeroKarmaBridge` to:

- get the current hero
- read karma / standing
- read ledger rows for UI
- apply outcomes

It is a thin facade and already does safe checks for disabled or missing runtime state.

## Minimal read example

```csharp
using Despicable.HeroKarma;
using Verse;

public static class MyHeroKarmaLogic
{
    public static void LogHeroState()
    {
        Pawn hero = HeroKarmaBridge.GetHeroPawnSafe();
        int karma = HeroKarmaBridge.GetGlobalKarma();
        int standing = HeroKarmaBridge.GetGlobalStanding();

        Log.Message($"Hero={hero?.LabelShort ?? "none"} karma={karma} standing={standing}");
    }
}
```

## Minimal award example

```csharp
using System;
using Despicable.HeroKarma;
using Verse;

public static class MyHeroKarmaLogic
{
    public static void AwardRumourKarma(Pawn targetPawn)
    {
        HeroKarmaBridge.ApplyOutcome(
            karmaDelta: 2,
            standingDelta: 0,
            eventKey: "MyRumourPraise",
            label: "Helpful gossip",
            detail: "Spread a useful rumour for the colony.",
            karmaReason: "Helped the colony",
            standingReason: null,
            tokens: Array.Empty<IHKEffectToken>(),
            targetPawnId: targetPawn?.ThingID,
            targetFactionId: 0);
    }
}
```

## Reading ledger rows for UI

If you want to show recent HeroKarma events in your own UI:

```csharp
using Despicable.HeroKarma;

foreach (var row in HeroKarmaBridge.GetLedgerRowsForUI(20))
{
    // row.label
    // row.detail
    // row.delta
    // row.standingDelta
}
```

## When to use `IHKBackendBridge`

Only use `IHKBackendBridge` if you are building or replacing the backend that HeroKarma UI reads from.

That is an advanced path.
It is not the normal “my mod wants to award karma” path.

## Practical recommendation

Start with:

- `GetHeroPawnSafe()`
- `GetGlobalKarma()`
- `ApplyOutcome(...)`

That is the stable, simple integration surface.

## In one sentence

**For normal mod integrations, use `HeroKarmaBridge` and ignore backend replacement unless you are doing deep HeroKarma internals work.**
