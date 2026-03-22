using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
public class AnatomyProfileDef : Def
{
    public List<ThingDef> raceDefs;
    public List<GeneDef> geneDefs;
    public List<PawnKindDef> pawnKindDefs;
    public List<BodyTypeDef> bodyTypes;
    public List<Gender> genders;
    public List<LifeStageDef> lifeStages;
    public List<AnatomyPartDef> parts;
    public bool humanlikeOnly;
    public int priority;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors())
            yield return error;

        if (parts == null || parts.Count == 0)
            yield return $"{defName} must define at least one anatomy part.";
    }
}
