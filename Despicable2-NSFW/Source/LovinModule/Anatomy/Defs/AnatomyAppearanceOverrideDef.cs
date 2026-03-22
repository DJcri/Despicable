using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
public class AnatomyAppearanceOverrideDef : Def
{
    public List<ThingDef> raceDefs;
    public List<GeneDef> geneDefs;
    public List<AnatomyPartVariantDef> variantDefs;
    public List<PawnKindDef> pawnKindDefs;
    public List<BodyTypeDef> bodyTypes;
    public List<Gender> genders;
    public List<LifeStageDef> lifeStages;
    public List<AnatomyAppearanceEntry> parts;
    public int priority;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
            yield return error;

        if (parts == null || parts.Count == 0)
            yield return $"{defName} must define at least one anatomy appearance entry.";

        if (parts == null)
            yield break;

        for (int i = 0; i < parts.Count; i++)
        {
            AnatomyAppearanceEntry entry = parts[i];
            if (entry == null)
            {
                yield return $"{defName} has a null anatomy appearance entry.";
                continue;
            }

            if (entry.part == null)
                yield return $"{defName} anatomy appearance entry {i} must define a part.";

            if (entry.texPath.NullOrEmpty() && entry.texPathAroused.NullOrEmpty() && (entry.sizeTextureVariants == null || entry.sizeTextureVariants.Count == 0))
                yield return $"{defName} anatomy appearance entry {i} must define fixed texture paths or sizeTextureVariants.";

            if (entry.sizeTextureVariants == null)
                continue;

            for (int j = 0; j < entry.sizeTextureVariants.Count; j++)
            {
                AnatomySizeTextureVariant variant = entry.sizeTextureVariants[j];
                if (variant == null)
                {
                    yield return $"{defName} anatomy appearance entry {i} has a null sizeTextureVariants entry.";
                    continue;
                }

                if (variant.maxSize < variant.minSize)
                    yield return $"{defName} anatomy appearance entry {i} size texture variant {j} has maxSize < minSize.";

                if (variant.texPath.NullOrEmpty() && variant.texPathAroused.NullOrEmpty())
                    yield return $"{defName} anatomy appearance entry {i} size texture variant {j} must define a neutral or aroused texture path.";
            }
        }
    }
}

public class AnatomyAppearanceEntry
{
    public AnatomyPartDef part;
    public string texPath;
    public string texPathAroused;
    public List<AnatomySizeTextureVariant> sizeTextureVariants;
}
