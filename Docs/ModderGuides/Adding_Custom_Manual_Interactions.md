# Adding a custom manual interaction to Despicable 2

This is the **simple, practical** way to add a custom right-click action like **“Spread rumours about X”**.

## First: is this XML?

No.

Right now the Despicable 2 social submenu is built in **C#**, not XML.

The seam to hook is:

- `Despicable2-Core/Source/Despicable/ManualInteractionModule/InteractionMenu.cs`
- method: `GenerateSocialOptionSpecs(...)`

The existing example to copy is:

- `Despicable2-NSFW/Source/LovinModule/Patches/HarmonyPatch_MI_Intimacy.cs`

## Easiest approach

If your mod already knows how to make pawns do the action autonomously, the easiest manual version is:

1. **Harmony patch** `InteractionMenu.GenerateSocialOptionSpecs(...)`
2. **Append** your own `ManualMenuOptionSpec`
3. In its `Action`, **start your own job / logic**

That is enough. You do **not** need XML support.

## Minimal example

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MyRumourMod;

[HarmonyPatch]
internal static class Patch_AddRumourInteraction
{
    private static System.Reflection.MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(InteractionMenu), "GenerateSocialOptionSpecs");
    }

    private static void Postfix(Pawn pawn, LocalTargetInfo target, ref IEnumerable<ManualMenuOptionSpec> __result)
    {
        var list = __result?.ToList() ?? new List<ManualMenuOptionSpec>();
        var targetPawn = target.Pawn;

        if (pawn == null || targetPawn == null)
        {
            __result = list;
            return;
        }

        if (pawn == targetPawn || pawn.HostileTo(targetPawn))
        {
            __result = list;
            return;
        }

        list.Add(new ManualMenuOptionSpec
        {
            Label = $"Spread rumours about {targetPawn.Name?.ToStringShort ?? targetPawn.LabelShort}",
            Priority = MenuOptionPriority.High,
            RevalidateClickTarget = targetPawn,
            IconThing = targetPawn,
            IconColor = Color.white,
            Action = () => StartRumourJob(pawn, targetPawn)
        });

        __result = list;
    }

    private static void StartRumourJob(Pawn pawn, Pawn targetPawn)
    {
        if (pawn == null || targetPawn == null)
            return;

        Job job = JobMaker.MakeJob(MyRumourJobDefOf.Job_SpreadRumours, targetPawn);
        pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        pawn.jobs.TryTakeOrderedJob(job);
    }
}
```

## When to use `InteractionEntry`

Use `Despicable.Core.InteractionEntry` if you want your interaction to go through Despicable’s manual interaction pipeline.

That is useful when you want:

- a real **manual interaction request/context**
- a custom `RequestedInteractionId`
- a custom `RequestedStageId`
- resolver / hook integration
- a fallback job through `Interactions.OrderedJob(...)`

Example shape:

```csharp
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

Interactions.OrderedJob(MyRumourJobDefOf.Job_SpreadRumours, pawn, targetPawn, req, ctx);
```

If your system already works with its own jobs, start with the **simple job version first**.

## Good defaults

Match the existing menu style:

- `Priority = MenuOptionPriority.High`
- `RevalidateClickTarget = targetPawn`
- `IconThing = targetPawn`
- `IconColor = Color.white`

## Optional polish

If the action is not always allowed, add a disabled entry instead of hiding it:

```csharp
list.Add(new ManualMenuOptionSpec
{
    Label = $"Spread rumours about {targetPawn.LabelShort}",
    Action = null,
    Disabled = true,
    DisabledReason = "Not available right now.",
    Tooltip = "Not available right now.",
    Priority = MenuOptionPriority.High,
    RevalidateClickTarget = targetPawn,
    IconThing = targetPawn,
    IconColor = Color.white
});
```

## In one sentence

**You can add custom manual interactions, but the working path today is a small C# Harmony patch, not XML.**
