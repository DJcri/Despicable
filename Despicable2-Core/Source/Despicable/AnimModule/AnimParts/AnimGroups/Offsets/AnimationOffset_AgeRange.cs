using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Despicable;
public class AnimationOffset_AgeRange : BaseAnimationOffset
{
    public List<BodyTypeOffset_AgeRange> offsetsMale;
    public List<BodyTypeOffset_AgeRange> offsetsFemale;

    public override Vector3? getOffset(Pawn pawn)
    {
        List<BodyTypeOffset_AgeRange> pawnOffsets = (PawnStateUtil.ComparePawnGenderToByte(pawn, (byte)Gender.Male) ? offsetsMale : offsetsFemale);
        return pawnOffsets.Find(x => x.bodyType == pawn.story.bodyType && x.ageRange.Includes(pawn.ageTracker.AgeBiologicalYears))?.offset ?? pawnOffsets.Last().offset;
    }

    public override int? getRotation(Pawn pawn)
    {
        List<BodyTypeOffset_AgeRange> pawnOffsets = (PawnStateUtil.ComparePawnGenderToByte(pawn, (byte)Gender.Male) ? offsetsMale : offsetsFemale);
        return pawnOffsets.Find(x => x.bodyType == pawn.story.bodyType && x.ageRange.Includes(pawn.ageTracker.AgeBiologicalYears))?.rotation ?? pawnOffsets.Last().rotation;
    }

    public override Vector3? getScale(Pawn pawn)
    {
        List<BodyTypeOffset_AgeRange> pawnOffsets = (PawnStateUtil.ComparePawnGenderToByte(pawn, (byte)Gender.Male) ? offsetsMale : offsetsFemale);
        return pawnOffsets.Find(x => x.bodyType == pawn.story.bodyType && x.ageRange.Includes(pawn.ageTracker.AgeBiologicalYears))?.scale ?? pawnOffsets.Last().scale;
    }
}

public class BodyTypeOffset_AgeRange : BodyTypeOffset
{
    public FloatRange ageRange;
}
