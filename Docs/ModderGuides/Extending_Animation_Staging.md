# Extending animation staging

This is the short guide for adding staged animation content.

## First: defs or C#?

Usually:

- **Defs first**
- **C# only if needed**

The easiest path is to add a `StageClipDef`.

You only need C# if you want one of these:

- new pawn tags for slot matching
- custom candidate scoring or filtering
- a new playback backend

Main code files:

- `Despicable2-Core/Source/Despicable/Core/Staging/StageClipDef.cs`
- `Despicable2-Core/Source/Despicable/Core/Staging/StagePlanner.cs`
- `Despicable2-Core/Source/Despicable/Core/Staging/StageTagProviders.cs`
- `Despicable2-Core/Source/Despicable/Core/Staging/StagePlanHooks.cs`
- `Despicable2-Core/Source/Despicable/Core/Staging/StagePlaybackBackends.cs`

Core bootstrap example:

- `Despicable2-Core/Source/Despicable/DespicableBootstrap.cs`

NSFW registration example:

- `Despicable2-NSFW/Source/LovinModule/Hooks/HookBootstraps.cs`

## Easiest approach

If your content can use the existing planner and playback rules, do this:

1. add a `StageClipDef`
2. give it the `stageTags` your interaction will ask for
3. add matching `slots`
4. let Despicable plan and play it normally

## Minimal `StageClipDef` example

```xml
<Defs>
  <Despicable.Core.Staging.StageClipDef>
    <defName>MyRumourStage</defName>
    <stageTags>
      <li>rumour_talk</li>
    </stageTags>
    <anchorMode>StandingOnly</anchorMode>
    <slots>
      <li>
        <slotId>speaker</slotId>
        <facing>FaceSlot</facing>
        <faceSlotId>listener</faceSlotId>
        <requiredPawnTags>
          <li>can_speak</li>
        </requiredPawnTags>
      </li>
      <li>
        <slotId>listener</slotId>
        <facing>FaceSlot</facing>
        <faceSlotId>speaker</faceSlotId>
      </li>
    </slots>
  </Despicable.Core.Staging.StageClipDef>
</Defs>
```

That is the simplest content-first path.

## When to add timeline stages

Add the `stages` list if you want an explicit multi-stage timeline.

If `stages` is present and non-empty, Despicable will treat the clip as timeline content.
If `playbackBackendKey` is empty, the planner defaults timeline clips to `stageClipTimeline`.

## When to add a pawn tag provider

Add a tag provider when the default tags are not enough for your slot rules.

Example:

```csharp
using System.Collections.Generic;
using Despicable.Core.Staging;
using RimWorld;
using Verse;

public sealed class RumourStageTags : IStagePawnTagProvider
{
    public int Priority => 100;

    public void AddTags(Pawn pawn, StageTagContext ctx, HashSet<string> into)
    {
        if (pawn == null || into == null)
            return;

        if (pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Talking) == true)
            into.Add("can_speak");
    }
}
```

Register it once at startup:

```csharp
using Despicable.Core.Staging;
using Verse;

[StaticConstructorOnStartup]
public static class RumourStageBootstrap
{
    static RumourStageBootstrap()
    {
        StageTagProviders.Register(new RumourStageTags());
    }
}
```

## When to add a stage plan hook

Use `IStagePlanHook` when you want to:

- reject some candidates
- prefer one valid candidate over another

That is the right seam for custom scoring and filtering.
Do not start playback there.

## When to add a playback backend

Use `IStagePlaybackBackend` only if your clip payload needs a custom player.

Most content mods should **not** start here.

## Practical recommendation

Start with:

- one `StageClipDef`
- existing stage tags if possible
- one custom pawn tag provider only if you truly need new slot tags

That gets you the most value with the least code.

## In one sentence

**Animation staging is defs-first, with optional C# registries when you need new tags, scoring, or playback behavior.**
