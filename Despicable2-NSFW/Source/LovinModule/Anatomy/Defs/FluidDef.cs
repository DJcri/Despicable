using System.Collections.Generic;
using Verse;

namespace Despicable;
public class FluidDef : Def
{
    public List<string> tags;

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
}

public class AnatomyFluidTemplate
{
    public FluidDef fluid;
    public float baseCapacity;
    public float minCapacity;
    public float maxCapacity;
    public float initialFillPercent;
    public float refillPerDay;
}
