using System.Collections.Generic;
using Verse;

namespace Despicable;
public class AnatomyPartDef : Def
{
    public AnatomySlotDef slot;
    public List<string> tags;
    public List<string> capabilities;
    public string texPathAroused;
    public PawnRenderNodeProperties properties;
    public bool visibleByDefault = true;
    public bool showWhileAnimating = true;
    public bool showOutsideAnimation = true;

    public float baseSize = 1f;
    public float minSize = 1f;
    public float maxSize = 1f;
    // Legacy single-fluid capacity fields retained only for migration from earlier PoC saves/defs.
    public float baseFluidCapacity;
    public float minFluidCapacity;
    public float maxFluidCapacity;
    public List<AnatomyFluidTemplate> fluidTemplates;
    public List<AnatomySizeTextureVariant> sizeTextureVariants;

    public bool HasTag(string tag)
    {
        if (tag.NullOrEmpty() || tags == null)
            return false;

        for (int i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i], tag, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public bool HasCapability(string capability)
    {
        if (capability.NullOrEmpty() || capabilities == null)
            return false;

        for (int i = 0; i < capabilities.Count; i++)
        {
            if (string.Equals(capabilities[i], capability, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
            yield return error;

        if (visibleByDefault && properties == null)
            yield return $"{defName} must define properties when visibleByDefault is true.";

        if (maxSize < minSize)
            yield return $"{defName} has maxSize < minSize.";

        if (maxFluidCapacity < minFluidCapacity)
            yield return $"{defName} has maxFluidCapacity < minFluidCapacity.";

        if (fluidTemplates != null)
        {
            HashSet<string> seenFluidDefs = new HashSet<string>();
            for (int i = 0; i < fluidTemplates.Count; i++)
            {
                AnatomyFluidTemplate template = fluidTemplates[i];
                if (template == null)
                {
                    yield return $"{defName} has a null fluidTemplates entry.";
                    continue;
                }

                if (template.fluid == null)
                    yield return $"{defName} fluid template {i} must define a fluid.";

                if (template.maxCapacity < template.minCapacity)
                    yield return $"{defName} fluid template {i} has maxCapacity < minCapacity.";

                if (template.initialFillPercent < 0f || template.initialFillPercent > 1f)
                    yield return $"{defName} fluid template {i} has initialFillPercent outside 0..1.";

                if (template.refillPerDay < 0f)
                    yield return $"{defName} fluid template {i} has refillPerDay < 0.";

                string fluidDefName = template.fluid?.defName;
                if (!fluidDefName.NullOrEmpty() && !seenFluidDefs.Add(fluidDefName))
                    yield return $"{defName} defines duplicate fluid template entries for {fluidDefName}.";
            }
        }

        if (sizeTextureVariants != null)
        {
            for (int i = 0; i < sizeTextureVariants.Count; i++)
            {
                AnatomySizeTextureVariant variant = sizeTextureVariants[i];
                if (variant == null)
                {
                    yield return $"{defName} has a null sizeTextureVariants entry.";
                    continue;
                }

                if (variant.maxSize < variant.minSize)
                    yield return $"{defName} size texture variant {i} has maxSize < minSize.";

                if (variant.texPath.NullOrEmpty() && variant.texPathAroused.NullOrEmpty())
                    yield return $"{defName} size texture variant {i} must define a neutral or aroused texture path.";
            }
        }
    }
}

public class AnatomySizeTextureVariant
{
    public float minSize = 0f;
    public float maxSize = 999f;
    public string texPath;
    public string texPathAroused;

    public bool Matches(float size)
    {
        return size >= minSize && size <= maxSize;
    }
}
