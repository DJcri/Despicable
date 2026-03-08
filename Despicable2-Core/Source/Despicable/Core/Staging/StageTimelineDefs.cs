using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable.Core.Staging;
/// <summary>
/// One stage in a clip timeline.
/// Each stage lists per-slot track assignments, making role alignment explicit.
/// </summary>
public class StageTimelineStageDef
{
    public string stageId;

    /// <summary>
    /// Optional duration override. If omitted, backends may derive timing from AnimationDef.durationTicks.
    /// </summary>
    public int durationTicks;

    /// <summary>
    /// Optional per-stage loop specification.
    /// </summary>
    public StageLoopDef loop;

    /// <summary>
    /// Per-slot animation tracks for this stage.
    /// </summary>
    public List<StageTrackDef> tracks;

    public override string ToString() => $"Stage(stageId={stageId ?? "null"}, tracks={tracks?.Count ?? 0})";
}

/// <summary>
/// SlotId -> AnimationDef assignment for a given stage.
/// </summary>
public class StageTrackDef
{
    public string slotId;
    public AnimationDef animation;
}

public class StageLoopDef
{
    public StageLoopMode mode = StageLoopMode.None;

    // When mode == Count
    public int count = 1;

    // When mode == CountRange
    public IntRange countRange = new(1, 1);

    public int ResolveLoopCount()
    {
        switch (mode)
        {
            case StageLoopMode.None:
                return 1;
            case StageLoopMode.Count:
                return count <= 0 ? 1 : count;
            case StageLoopMode.CountRange:
                return countRange.TrueMax <= 0 ? 1 : countRange.RandomInRange;
            default:
                return 1;
        }
    }
}

// --------------------------------------------------------------------
// Variants (optional)
// --------------------------------------------------------------------

public class StageVariantDef
{
    public string variantId;
    public float weight = 1f;

    public StageVariantRequirements requirements;

    public List<StageOverrideDef> overrides;
}

public class StageVariantRequirements
{
    public List<StageSlotTagRule> slotTagRules;
}

public class StageSlotTagRule
{
    public string slotId;
    public List<string> requiredTags;
    public List<string> forbiddenTags;
}

public class StageOverrideDef
{
    public string stageId;
    public List<StageTrackDef> tracks;
}
