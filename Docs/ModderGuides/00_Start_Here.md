# Despicable 2 modder guides

These are the **easy-read** docs for other modders.

## Read this one if...

- You want to add a new right-click action  
  Read: `Adding_Custom_Manual_Interactions.md`

- You want your custom interaction to go through Despicable's resolver  
  Read: `Extending_Manual_Interaction_Resolver.md`

- You want to add staged animation content  
  Read: `Extending_Animation_Staging.md`

- You want to read or award HeroKarma from another mod  
  Read: `Integrating_With_HeroKarma.md`

## Very short version

### Manual interaction menu
- **C# only**
- easiest seam: patch `InteractionMenu.GenerateSocialOptionSpecs(...)`
- example: `Despicable2-NSFW/Source/LovinModule/Patches/HarmonyPatch_MI_Intimacy.cs`

### Manual interaction resolver
- **C# only**
- easiest seam: `InteractionEntry.TryPrepareManual(...)` + `Interactions.OrderedJob(...)`
- hook seam: `Hooks.RegisterPre(...)` / `Hooks.RegisterPost(...)`

### Animation staging
- **Defs first, C# optional**
- easiest path: add `StageClipDef`
- C# only if you need new pawn tags, new scoring rules, or a new playback backend

### HeroKarma
- **C# only**
- easiest path: `HeroKarmaBridge`
- advanced path: `IHKBackendBridge` + `HKBackendBridge.Register(...)`

## Best example code to copy

- `Despicable2-NSFW/Source/LovinModule/Patches/HarmonyPatch_MI_Intimacy.cs`
- `Despicable2-NSFW/Source/LovinModule/Hooks/HookBootstraps.cs`
- `Despicable2-NSFW/Source/LovinModule/Hooks/LovinResolveHooks.cs`
- `Despicable2-Core/Source/Despicable/DespicableBootstrap.cs`

## Practical rule

Start with the **smallest working path**.

Do not jump straight to hooks, backends, or bridge replacement unless the simple path is not enough.
