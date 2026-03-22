using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
public class D2GenitalTextureOverrideDef : Def
{
    public List<ThingDef> raceDefs;
    public List<PawnKindDef> pawnKindDefs;
    public List<D2GenitalTextureEntry> textures;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
            yield return error;

        bool hasRaceTargets = raceDefs != null && raceDefs.Count > 0;
        bool hasPawnKindTargets = pawnKindDefs != null && pawnKindDefs.Count > 0;
        if (!hasRaceTargets && !hasPawnKindTargets)
            yield return $"{defName} must define at least one raceDefs or pawnKindDefs target.";

        if (textures == null || textures.Count == 0)
        {
            yield return $"{defName} must define at least one texture entry.";
            yield break;
        }

        for (int i = 0; i < textures.Count; i++)
        {
            D2GenitalTextureEntry entry = textures[i];
            if (entry == null)
            {
                yield return $"{defName} has a null texture entry at index {i}.";
                continue;
            }

            if (entry.genital == null)
                yield return $"{defName} texture entry {i} is missing genital.";

            if (entry.texPath.NullOrEmpty() && entry.texPathAroused.NullOrEmpty())
                yield return $"{defName} texture entry {i} for {entry.genital?.defName ?? "<null>"} must define texPath, texPathAroused, or both.";
        }
    }
}

public class D2GenitalTextureEntry
{
    public GenitalDef genital;
    public string texPath;
    public string texPathAroused;
}
