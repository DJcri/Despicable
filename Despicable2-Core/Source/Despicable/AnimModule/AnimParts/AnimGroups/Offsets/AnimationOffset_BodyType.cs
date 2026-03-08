using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class AnimationOffset_BodyType : BaseAnimationOffset
{
    public List<BodyTypeOffset> offsets;

    public override Vector3? getOffset(Pawn pawn)
    {
        return offsets.Find(x => x.bodyType == pawn.story.bodyType)?.offset;
    }

    public override int? getRotation(Pawn pawn)
    {
        return offsets.Find(x => x.bodyType == pawn.story.bodyType)?.rotation;
    }

    public override Vector3? getScale(Pawn pawn)
    {
        return offsets.Find(x => x.bodyType == pawn.story.bodyType)?.scale;
    }
}
