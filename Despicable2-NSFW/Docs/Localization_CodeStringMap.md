# Despicable 2 (NSFW) Localization: Code String Map

This file maps **auto-generated translation keys** to **player-facing strings currently hardcoded in C# source**.

Use it when converting UI/messages to localization via `.Translate()`.

> Note: changing C# source has no effect until the mod DLL is rebuilt and replaced in `1.6/Assemblies/`.

## `D2N_CODE_90731E3E`

**English:** Do lovin'

**Occurrences:**

- `LovinModule/Patches/HarmonyPatch_MI_Intimacy.cs:46`  
  `if (__result.Any(o => o != null && o.Label != null && o.Label.StartsWith("Do lovin'", StringComparison.OrdinalIgnoreCase)))`

## `D2N_CODE_C1CB4969`

**English:** Do lovin' with {targetPawn.Name?.ToStringShort ?? targetPawn.LabelShort}

**Occurrences:**

- `LovinModule/Patches/HarmonyPatch_MI_Intimacy.cs:49`  
  `var label = $"Do lovin' with {targetPawn.Name?.ToStringShort ?? targetPawn.LabelShort}";`
