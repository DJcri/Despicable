using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public abstract class BaseAnimationOffset
{
    public List<ThingDef> races;

    public abstract Vector3? getOffset(Pawn pawn);

    public abstract int? getRotation(Pawn pawn);

    public abstract Vector3? getScale(Pawn pawn);

    public bool appliesToPawn(Pawn pawn)
    {
        return races.Contains(pawn.def);
    }
}
