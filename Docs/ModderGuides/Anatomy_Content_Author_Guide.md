# Anatomy Content Author Guide

This guide shows how to add new anatomy content with defs only.
It covers:

- adding a brand-new part
- giving a part to a specific race
- giving a part to pawns with a specific gene
- swapping textures by race, gene, or installed bionic/variant
- changing size and fluid behavior with genes or installed variants

This guide reflects the current framework in source.

## Mental model

The anatomy system has five layers:

1. `AnatomyPartDef`
   - what the part is
2. `AnatomyProfileDef`
   - who gets the part
3. `AnatomyPartVariantDef`
   - what installed version that pawn has, such as a bionic version of the same part
4. `AnatomyAppearanceOverrideDef`
   - how the part looks for specific pawns
5. `AnatomyGeneModifierDef`
   - how genes modify generated size and fluids

If a part should exist but not render yet, set:

- `visibleByDefault` = `false`
- no render `properties`
- no texture paths required

## Quick recipe

For a new part, the minimum workflow is:

1. create an `AnatomyPartDef`
2. create one or more `AnatomyProfileDef` defs that grant it
3. optionally add `AnatomyPartVariantDef` defs for bionic or other installed variants
4. optionally add `AnatomyAppearanceOverrideDef` defs for race, gene, or variant-specific art
5. optionally add `AnatomyGeneModifierDef` defs for size / fluid changes

## Available selectors

### `AnatomyProfileDef`
Supports these selectors:

- `raceDefs`
- `geneDefs`
- `pawnKindDefs`
- `bodyTypes`
- `genders`
- `lifeStages`
- `humanlikeOnly`

Profiles are how you add a part to matching pawns.

### `AnatomyPartVariantDef`
Supports these selectors:

- `hediffDefs`
- `geneDefs`
- `raceDefs`
- `pawnKindDefs`
- `bodyTypes`
- `genders`
- `lifeStages`

Variants do **not** add the part. They select the installed version of a part that already exists, which is the clean path for bionics.

### `AnatomyAppearanceOverrideDef`
Supports these selectors:

- `variantDefs`
- `raceDefs`
- `geneDefs`
- `pawnKindDefs`
- `bodyTypes`
- `genders`
- `lifeStages`

Appearance overrides do **not** add the part. They only swap art. Variant-targeted overrides beat gene-only and race-only overrides.

### `AnatomyGeneModifierDef`
Supports these selectors:

- `geneDefs`
- `raceDefs`
- `pawnKindDefs`
- `bodyTypes`
- `genders`
- `lifeStages`

Gene modifiers do **not** add the part. They change generated size and fluids for parts the pawn already has.

## Example 1: Add a new internal-only part

This example adds an internal-only ovary part with a fluid template but no art.

```xml
<Despicable.AnatomyPartDef>
  <defName>AnatomyPart_Ovaries</defName>
  <slot>Chest</slot>
  <tags>
    <li>Ovaries</li>
  </tags>
  <capabilities>
    <li>InternalRepro</li>
  </capabilities>
  <visibleByDefault>false</visibleByDefault>
  <baseSize>1.0</baseSize>
  <minSize>0.9</minSize>
  <maxSize>1.1</maxSize>
</Despicable.AnatomyPartDef>
```

Notes:

- because it is internal-only, no `properties` block is required
- because it is not visible, no texture paths are required

## Example 2: Add a visible part

This example adds a visible tail-like part with base art.

```xml
<Despicable.AnatomyPartDef>
  <defName>AnatomyPart_FluffTail</defName>
  <slot>Chest</slot>
  <tags>
    <li>Tail</li>
  </tags>
  <capabilities>
    <li>Tail</li>
  </capabilities>
  <visibleByDefault>true</visibleByDefault>
  <showWhileAnimating>true</showWhileAnimating>
  <showOutsideAnimation>true</showOutsideAnimation>
  <properties Class="PawnRenderNodeProperties">
    <debugLabel>Fluff Tail</debugLabel>
    <nodeClass>Despicable.PawnRenderNode_Genitals</nodeClass>
    <workerClass>Despicable.PawnRenderNodeWorker_Genitals</workerClass>
    <texPath>MyMod/Anatomy/Tail/neutral</texPath>
    <tagDef>Genitals</tagDef>
    <rotDrawMode>Fresh, Rotting</rotDrawMode>
    <colorType>Skin</colorType>
    <baseLayer>64</baseLayer>
  </properties>
  <texPathAroused>MyMod/Anatomy/Tail/aroused</texPathAroused>
</Despicable.AnatomyPartDef>
```

Notes:

- today, visible parts need a `properties` block
- slot coverage is slot-aware in code:
  - `ExternalGenitals` checks pants coverage
  - `Chest` checks shirt coverage

## Example 3: Give a part to a specific race

Use an `AnatomyProfileDef`.

```xml
<Despicable.AnatomyProfileDef>
  <defName>MyMod_CatfolkBreastsProfile</defName>
  <raceDefs>
    <li>CatfolkRaceDef</li>
  </raceDefs>
  <genders>
    <li>Female</li>
  </genders>
  <parts>
    <li>AnatomyPart_Breasts</li>
  </parts>
</Despicable.AnatomyProfileDef>
```

This adds breasts only to female pawns of `CatfolkRaceDef`.

## Example 4: Give a part to pawns with a specific gene

`AnatomyProfileDef` now supports `geneDefs`, so you can grant part presence by gene with defs only.

```xml
<Despicable.AnatomyProfileDef>
  <defName>MyMod_LactationGeneBreastsProfile</defName>
  <geneDefs>
    <li>MyMod_LactationGene</li>
  </geneDefs>
  <parts>
    <li>AnatomyPart_Breasts</li>
  </parts>
</Despicable.AnatomyProfileDef>
```

That profile grants `AnatomyPart_Breasts` to any pawn with the active gene.

You can combine selectors too:

```xml
<Despicable.AnatomyProfileDef>
  <defName>MyMod_InsectQueenOvipositorProfile</defName>
  <raceDefs>
    <li>InsectoidQueenRaceDef</li>
  </raceDefs>
  <geneDefs>
    <li>MyMod_OvipositionGene</li>
  </geneDefs>
  <parts>
    <li>AnatomyPart_Ovipositor</li>
  </parts>
</Despicable.AnatomyProfileDef>
```

In that example, the pawn must match both the race selector and the gene selector.

## Example 5: Race-specific textures

Use `AnatomyAppearanceOverrideDef`.

```xml
<Despicable.AnatomyAppearanceOverrideDef>
  <defName>MyMod_CatfolkBreastAppearance</defName>
  <raceDefs>
    <li>CatfolkRaceDef</li>
  </raceDefs>
  <parts>
    <li>
      <part>AnatomyPart_Breasts</part>
      <texPath>MyMod/Breasts/Catfolk/neutral</texPath>
      <texPathAroused>MyMod/Breasts/Catfolk/aroused</texPathAroused>
    </li>
  </parts>
</Despicable.AnatomyAppearanceOverrideDef>
```

This only changes appearance. The part must still be granted by a profile.

## Example 6: Gene-specific textures that override race

Gene appearance overrides are more specific than race-only ones, so they win when both match.

```xml
<Despicable.AnatomyAppearanceOverrideDef>
  <defName>MyMod_FelineGeneBreastAppearance</defName>
  <geneDefs>
    <li>MyMod_FelineGene</li>
  </geneDefs>
  <parts>
    <li>
      <part>AnatomyPart_Breasts</part>
      <texPath>MyMod/Breasts/Feline/neutral</texPath>
      <texPathAroused>MyMod/Breasts/Feline/aroused</texPathAroused>
    </li>
  </parts>
</Despicable.AnatomyAppearanceOverrideDef>
```

If the same pawn also matches a race appearance override, the gene override wins because the resolver scores `geneDefs` as more specific.


## Example 6b: Bionic or installed-variant textures

The clean path for bionic art is:

1. keep the same base part
2. define an `AnatomyPartVariantDef` that matches the implant or hediff
3. target that variant from `AnatomyAppearanceOverrideDef`

```xml
<Despicable.AnatomyPartVariantDef>
  <defName>MyMod_BionicPenisVariant</defName>
  <basePart>Genital_Penis</basePart>
  <hediffDefs>
    <li>MyMod_BionicPenisImplant</li>
  </hediffDefs>
  <sizeMultiplier>1.1</sizeMultiplier>
  <fluids>
    <li>
      <fluid>Fluid_Semen</fluid>
      <capacityMultiplier>1.5</capacityMultiplier>
      <refillRateMultiplier>1.25</refillRateMultiplier>
    </li>
  </fluids>
</Despicable.AnatomyPartVariantDef>

<Despicable.AnatomyAppearanceOverrideDef>
  <defName>MyMod_BionicPenisAppearance</defName>
  <variantDefs>
    <li>MyMod_BionicPenisVariant</li>
  </variantDefs>
  <parts>
    <li>
      <part>Genital_Penis</part>
      <texPath>MyMod/Anatomy/BionicPenis/neutral</texPath>
      <texPathAroused>MyMod/Anatomy/BionicPenis/aroused</texPathAroused>
    </li>
  </parts>
</Despicable.AnatomyAppearanceOverrideDef>
```

That keeps `Genital_Penis` as the anatomical identity while letting the installed bionic variant change appearance and static generation values.

## Variant precedence

For appearance, specificity now resolves in this order:

- `variantDefs`
- `geneDefs`
- `raceDefs`
- base part art

So a bionic or other installed variant can override both race art and gene art cleanly.

## Example 7: Size-based textures

A part def or an appearance override can use `sizeTextureVariants`.

```xml
<Despicable.AnatomyAppearanceOverrideDef>
  <defName>MyMod_BreastBuckets</defName>
  <parts>
    <li>
      <part>AnatomyPart_Breasts</part>
      <sizeTextureVariants>
        <li>
          <minSize>0.0</minSize>
          <maxSize>0.95</maxSize>
          <texPath>MyMod/Breasts/Small</texPath>
        </li>
        <li>
          <minSize>0.95</minSize>
          <maxSize>1.15</maxSize>
          <texPath>MyMod/Breasts/Medium</texPath>
        </li>
        <li>
          <minSize>1.15</minSize>
          <maxSize>999</maxSize>
          <texPath>MyMod/Breasts/Large</texPath>
        </li>
      </sizeTextureVariants>
    </li>
  </parts>
</Despicable.AnatomyAppearanceOverrideDef>
```

The renderer reads the pawn's saved part `size` and chooses the first matching bucket.

## Example 8: Gene-driven size and fluid changes

Use `AnatomyGeneModifierDef` to change generated values.

```xml
<Despicable.AnatomyGeneModifierDef>
  <defName>MyMod_LactationGeneModifier</defName>
  <geneDefs>
    <li>MyMod_LactationGene</li>
  </geneDefs>
  <parts>
    <li>
      <part>AnatomyPart_Breasts</part>
      <sizeMultiplier>1.15</sizeMultiplier>
      <sizeOffset>0.05</sizeOffset>
      <fluids>
        <li>
          <fluid>Fluid_Milk</fluid>
          <capacityMultiplier>1.5</capacityMultiplier>
          <initialAmountMultiplier>1.25</initialAmountMultiplier>
          <refillRateMultiplier>2.0</refillRateMultiplier>
        </li>
      </fluids>
    </li>
  </parts>
</Despicable.AnatomyGeneModifierDef>
```

This modifier does **not** add breasts.
It only changes generated size and milk values for pawns who already have the part.

## Example 9: Multiple fluids on one part

A part can define more than one fluid template.

```xml
<Despicable.AnatomyPartDef>
  <defName>AnatomyPart_ExampleReservoir</defName>
  <slot>Chest</slot>
  <visibleByDefault>false</visibleByDefault>
  <fluidTemplates>
    <li>
      <fluid>Fluid_Milk</fluid>
      <baseCapacity>40</baseCapacity>
      <minCapacity>20</minCapacity>
      <maxCapacity>80</maxCapacity>
      <initialFillPercent>0.2</initialFillPercent>
      <refillPerDay>12</refillPerDay>
    </li>
    <li>
      <fluid>Fluid_Semen</fluid>
      <baseCapacity>20</baseCapacity>
      <minCapacity>10</minCapacity>
      <maxCapacity>35</maxCapacity>
      <initialFillPercent>1.0</initialFillPercent>
      <refillPerDay>24</refillPerDay>
    </li>
  </fluidTemplates>
</Despicable.AnatomyPartDef>
```

Each fluid becomes its own saved `AnatomyFluidInstance` on the pawn's part instance.

## Race-specific placement

Use `AnatomyPlacementDef` for idle offset exceptions.

```xml
<Despicable.AnatomyPlacementDef>
  <defName>MyMod_CatfolkBreastPlacement</defName>
  <part>AnatomyPart_Breasts</part>
  <raceDefs>
    <li>CatfolkRaceDef</li>
  </raceDefs>
  <offset>(0, 0, 0.02)</offset>
</Despicable.AnatomyPlacementDef>
```

Notes:

- placement defs are for exceptions only
- do not put a default offset on every part just because you can
- base placement should come from the slot anchor and art alignment

## Suggested file layout

A tidy content patch usually wants:

- `Defs/LovinModule/Anatomy/MyNewPartDefs.xml`
- `Defs/LovinModule/Anatomy/MyNewProfiles.xml`
- `Defs/LovinModule/Anatomy/MyNewAppearanceOverrides.xml`
- `Defs/LovinModule/Anatomy/MyNewGeneModifiers.xml`
- textures under your own folder if the part is visible

## Common pitfalls

### 1. Visible part with no `properties`
`AnatomyPartDef.ConfigErrors()` requires `properties` when `visibleByDefault` is true.

### 2. Appearance override without a matching profile
Appearance swaps art only. It does not grant part presence.

### 3. Gene modifier expected to add a part
`AnatomyGeneModifierDef` modifies generated values. It does not grant part presence.
Use `AnatomyProfileDef` with `geneDefs` for that.

### 4. Assuming bodytypes exist for every pawn
Animal or non-standard pawns may not have a `BodyTypeDef`.
Prefer `raceDefs`, `pawnKindDefs`, and `lifeStages` when content should work broadly.

### 5. Expecting saved part sizes to reroll automatically
Generated size and capacity are saved per pawn. Changing defs later does not silently reroll old pawns.

## Good first debugging loop

In RimWorld dev mode:

1. spawn a pawn that should match your profile
2. use the **Anatomy Debug** gizmo
3. confirm:
   - the part exists
   - the part size looks right
   - the right fluids exist
   - the resolved textures match your race/gene rules
   - coverage and slot info look sane

That gizmo is the anatomy framework's flashlight.
