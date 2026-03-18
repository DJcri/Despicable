# Extending the manual interaction resolver

Use this when you want your custom interaction to go through Despicable’s **request -> resolve -> job** pipeline.

## First: is this XML?

No.

This is a **C# extension seam**.

The main entry point is:

- `Despicable2-Core/Source/Despicable/Core/InteractionEntry.cs`

Useful support files:

- `Despicable2-Core/Source/Despicable/Core/Hooks/Hooks.cs`
- `Despicable2-Core/Source/Despicable/Core/Hooks/IPreResolveHook.cs`
- `Despicable2-Core/Source/Despicable/Core/Hooks/IPostResolveHook.cs`
- `Despicable2-Core/Source/Despicable/ManualInteractionModule/Interactions.cs`

Best example to copy:

- `Despicable2-NSFW/Source/LovinModule/Hooks/HookBootstraps.cs`
- `Despicable2-NSFW/Source/LovinModule/Hooks/LovinResolveHooks.cs`

## Easiest approach

If you want Despicable to carry your request context and run resolve hooks, do this:

1. call `InteractionEntry.TryPrepareManual(...)`
2. set `RequestedInteractionId` and anything else you need
3. start the ordered job with `Interactions.OrderedJob(...)`

That is the easiest “framework-native” path.

## Minimal example

```csharp
using Despicable;
using Despicable.Core;
using Verse;

public static class RumourInteractions
{
    public static void StartRumour(Pawn pawn, Pawn targetPawn)
    {
        if (!InteractionEntry.TryPrepareManual(
            pawn,
            targetPawn,
            Channels.ManualSocial,
            req =>
            {
                req.RequestedInteractionId = "SpreadRumours";
                req.RequestedStageId = "Rumour_Default";
            },
            out var req,
            out var ctx))
        {
            return;
        }

        Interactions.OrderedJob(
            MyRumourJobDefOf.Job_SpreadRumours,
            pawn,
            targetPawn,
            req,
            ctx);
    }
}
```

## What to put on the request

These are the fields most mods will care about:

- `RequestedInteractionId`  
  your own opaque ID, like `"SpreadRumours"`

- `RequestedStageId`  
  optional variant or subtype, like `"Rumour_Default"`

- `RequestedInteractionDef`  
  use this if you already have a real `InteractionDef`

- `RequestedCommand`  
  optional command string if that is how your content is modeled

- `Channel`  
  for manual social actions, use `Channels.ManualSocial`

## When to use hooks

Use hooks when you want to affect resolution globally instead of inside one menu click.

### Use a pre-resolve hook when you want to:
- block an interaction
- rewrite request data
- add extra checks before resolution

### Use a post-resolve hook when you want to:
- choose or replace the final `JobDef`
- fill in `ChosenInteractionId`
- fill in `ChosenStageId`

Do **not** start jobs from `IPostResolveHook`.
The interface comment explicitly says it should only adjust the resolution.

## Minimal hook example

```csharp
using Despicable.Core;

public sealed class RumourPreResolveHook : IPreResolveHook
{
    public bool PreResolve(InteractionRequest req, InteractionContext ctx, out string outReason)
    {
        outReason = null;

        if (req?.RequestedInteractionId != "SpreadRumours")
            return true;

        if (ctx == null || ctx.InitiatorHostileToRecipient)
        {
            outReason = "Cannot spread rumours about a hostile target.";
            return false;
        }

        return true;
    }
}

public sealed class RumourPostResolveHook : IPostResolveHook
{
    public void PostResolve(InteractionRequest req, InteractionContext ctx, InteractionResolution res)
    {
        if (req?.RequestedInteractionId != "SpreadRumours")
            return;

        if (res == null || !res.Allowed)
            return;

        res.ChosenInteractionId ??= "SpreadRumours";
        res.ChosenStageId ??= "Rumour_Default";
        res.ChosenJobDef ??= MyRumourJobDefOf.Job_SpreadRumours;
    }
}
```

## Registering hooks

Register them once during startup:

```csharp
using Despicable.Core;
using Verse;

[StaticConstructorOnStartup]
public static class RumourBootstrap
{
    static RumourBootstrap()
    {
        Hooks.RegisterPre(new RumourPreResolveHook());
        Hooks.RegisterPost(new RumourPostResolveHook());
    }
}
```

## Practical recommendation

Start with:

- `InteractionEntry.TryPrepareManual(...)`
- one `RequestedInteractionId`
- one fallback `JobDef`

Only add hooks if you want the interaction to behave like a first-class Despicable content path.

## In one sentence

**Use `InteractionEntry` when you want your custom interaction to move through Despicable’s resolver pipeline instead of bypassing it.**
