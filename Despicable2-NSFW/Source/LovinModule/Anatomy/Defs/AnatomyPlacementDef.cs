using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
public class AnatomyPlacementDef : Def
{
    public AnatomyPartDef part;
    public List<ThingDef> raceDefs;
    public List<PawnKindDef> pawnKindDefs;
    public List<BodyTypeDef> bodyTypes;
    public List<Gender> genders;
    public List<LifeStageDef> lifeStages;
    public Vector3 offset;
    public int priority;
}
