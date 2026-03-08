using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class AnimationOffsetDef : Def
{
    public List<BaseAnimationOffset> offsets;

    public bool FindOffset(Pawn pawn, out BaseAnimationOffset offset)
    {
        foreach (BaseAnimationOffset animOffset in offsets)
        {
            if (animOffset.appliesToPawn(pawn)) {

                offset = animOffset;
                return true;
            }
        }

        offset = null;
        return false;
    }
}
