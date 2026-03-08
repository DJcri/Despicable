using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class AnimationOffset_Single : BaseAnimationOffset
{
    public Vector3 offset;
    public int? rotation;
    public Vector3? scale = Vector3.one;

    public override Vector3? getOffset(Pawn pawn)
    {
        return offset;
    }

    public override int? getRotation(Pawn pawn)
    {
        return rotation;
    }

    public override Vector3? getScale(Pawn pawn)
    {
        return scale;
    }
}
