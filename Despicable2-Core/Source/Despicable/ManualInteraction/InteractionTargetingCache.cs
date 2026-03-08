using RimWorld;
using Verse;

namespace Despicable;
/// <summary>
/// Shared targeting parameters for the manual interaction menu.
/// The instance is immutable after creation, so call sites do not need their own mutable cache.
/// </summary>
public static class InteractionTargetingCache
{
    private static readonly TargetingParameters HumanlikePawnTargets = new()
    {
        canTargetHumans = true,
        canTargetAnimals = false,
        canTargetItems = false,
        mapObjectTargetsMustBeAutoAttackable = false,
    };

    public static TargetingParameters ForHumanlikePawns => HumanlikePawnTargets;
}
