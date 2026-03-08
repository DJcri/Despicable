namespace Despicable.Core.Staging;
public enum StageFacingRule
{
    FaceAnchor,
    FaceSlot,
    Fixed
}

public enum StageAnchorMode
{
    Any,
    BedOnly,
    StandingOnly
}

/// <summary>
/// Loop behavior for a single timeline stage.
/// This drives CompExtendedAnimator.loopIndex entries.
/// </summary>
public enum StageLoopMode
{
    None,
    Count,
    CountRange
}
