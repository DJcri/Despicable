using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class AnimationOffset_BodyTypeGendered : BaseAnimationOffset
{
    public List<BodyTypeOffset> offsetsMale;
    public List<BodyTypeOffset> offsetsFemale;

    public override Vector3? getOffset(Pawn pawn)
    {
        if (PawnStateUtil.ComparePawnGenderToByte(pawn, (byte)Gender.Female))
        {
            return offsetsFemale.Find(x => x.bodyType == pawn.story.bodyType)?.offset;
        }
        else
        {
            return offsetsMale.Find(x => x.bodyType == pawn.story.bodyType)?.offset;
        }
    }

    public override int? getRotation(Pawn pawn)
    {
        if (PawnStateUtil.ComparePawnGenderToByte(pawn, (byte)Gender.Female))
        {
            return offsetsFemale.Find(x => x.bodyType == pawn.story.bodyType)?.rotation;
        }
        else
        {
            return offsetsMale.Find(x => x.bodyType == pawn.story.bodyType)?.rotation;
        }
    }

    public override Vector3? getScale(Pawn pawn)
    {
        if (PawnStateUtil.ComparePawnGenderToByte(pawn, (byte)Gender.Female))
        {
            return offsetsFemale.Find(x => x.bodyType == pawn.story.bodyType)?.scale;
        }
        else
        {
            return offsetsMale.Find(x => x.bodyType == pawn.story.bodyType)?.scale;
        }
    }
}
