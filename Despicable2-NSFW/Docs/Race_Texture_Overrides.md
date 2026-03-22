# Race Texture Overrides

Despicable NSFW supports lightweight race and pawnkind-specific genital texture overrides through `D2GenitalTextureOverrideDef`.

This system is intentionally narrow. It only changes render texture paths. It does not change anatomy defaults, surgery anchors, or body trees.

## What patch authors can target

A texture override def may target either or both of:

- `raceDefs`: race `ThingDef` entries such as an AlienRace race def
- `pawnKindDefs`: specific pawn kinds for special variants

PawnKind matches are applied after race matches, so pawnkind-specific entries override race-level ones.

Multiple override defs can stack. Later matching entries overwrite earlier ones only for the fields they actually define.

## Texture entry format

Each texture entry targets one `GenitalDef`, such as `Genital_Penis` or `Genital_Vagina`.

Supported fields:

- `texPath`: neutral texture path
- `texPathAroused`: aroused texture path

Either field may be omitted.

## Fallback rules

When Despicable chooses a texture for a matching override entry, it uses this ladder:

- requesting aroused: `texPathAroused` -> `texPath`
- requesting neutral: `texPath` -> `texPathAroused`
- if neither override path exists, fall back to the base `GenitalDef` textures

This lets a patch provide only one texture path and still render in both states.

## Example

```xml
<Defs>
  <Despicable.D2GenitalTextureOverrideDef>
    <defName>D2_ReviaTextures</defName>

    <raceDefs>
      <li>ReviaRaceAlien</li>
    </raceDefs>

    <textures>
      <li>
        <genital>Genital_Penis</genital>
        <texPath>Anatomy/Revia/Penis/flaccid</texPath>
        <texPathAroused>Anatomy/Revia/Penis/erect</texPathAroused>
      </li>
      <li>
        <genital>Genital_Vagina</genital>
        <texPath>Anatomy/Revia/Vagina/neutral</texPath>
      </li>
    </textures>
  </Despicable.D2GenitalTextureOverrideDef>
</Defs>
```

In that example, the vagina override will use the neutral texture for both neutral and aroused rendering because no aroused override path was supplied.

## Practical guidance

- Start with race-level overrides first.
- Add pawnkind overrides only for true exceptions.
- Keep texture overrides separate from anatomy compatibility work.
- Do not patch foreign race defs just to change textures. Prefer standalone support defs in your own patch mod.
