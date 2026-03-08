using System.Collections.Generic;
using Verse;

namespace Despicable.Core.Staging;
public class StageSlotDef
{
    public string slotId;

    /// <summary>
    /// v1: raw per-slot offset vector. (Often unused once offsetDef is adopted.)
    /// </summary>
    public StageOffset offset;

    /// <summary>
    /// v2: optional AnimationOffsetDef reference, allowing per-pawn conditional offsets (bodytype, age, etc.).
    /// If set, playback backends should prefer this over the raw offset.
    /// </summary>
    public Despicable.AnimationOffsetDef offsetDef;

    public StageFacingRule facing = StageFacingRule.FaceAnchor;

    // Used when facing == FaceSlot
    public string faceSlotId;

    // Used when facing == Fixed (degrees)
    public float fixedRot;

    public List<string> requiredPawnTags;
    public List<string> forbiddenPawnTags;
}
