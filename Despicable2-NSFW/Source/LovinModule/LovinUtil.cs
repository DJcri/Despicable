using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Despicable.NSFW.Integrations;

namespace Despicable;
/// <summary>
/// Internal configuration for lovin' extensions and shared partner-selection helpers.
/// </summary>
public static partial class LovinUtil
{
    // Use duration ticks if no animations found
    private const int DefaultDurationTicksConstant = 10000;
    private const int SelfLovinDefaultDurationTicksConstant = 10000;
    private const float LovinMaxPainThresholdConstant = 0.6f;
    private const float LovinMinCompatibilityConstant = -1f;
    private const int LovinMinOpinionConstant = 20;
    private const int MaxLovinPartnersConstant = 3;
    private const int TicksBetweenHeartsConstant = 120;
    private const int RestDepletionIntervalConstant = 96;

    public static int DefaultDurationTicks => DefaultDurationTicksConstant;
    public static int SelfLovinDefaultDurationTicks => SelfLovinDefaultDurationTicksConstant;
    public static float LovinMaxPainThreshold => LovinMaxPainThresholdConstant;
    public static float LovinMinCompatibility => LovinMinCompatibilityConstant;
    public static int LovinMinOpinion => LovinMinOpinionConstant;
    public static int MaxLovinPartners => MaxLovinPartnersConstant;
    public static int TicksBetweenHearts => TicksBetweenHeartsConstant;
    public static int RestDepletionInterval => RestDepletionIntervalConstant;

    public static Pawn FindPartner(Pawn pawn, bool inBed = false)
    {
        if (IntegrationGuards.ShouldDeferLovinToIntimacy())
            return null;

        Pawn lover = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Lover);
        Pawn spouse = pawn.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Spouse);
        Pawn partner = null;
        Map map = pawn.Map;

        if (lover == null && spouse == null)
        {
            List<Pawn> targets = map.mapPawns.AllPawns
                .Where(target =>
                    PassesLovinCheck(pawn, target) &&
                    ((!inBed && !target.InBed()) ||
                     (inBed && (AloneInBed(target) || InSameBed(target, pawn)))))
                .ToList();

            foreach (Pawn potentialPartner in targets)
            {
                if (partner == null ||
                    pawn.relations.OpinionOf(potentialPartner) > pawn.relations.OpinionOf(partner))
                {
                    partner = potentialPartner;
                }
            }
        }
        else
        {
            partner = spouse == null ? lover : spouse;
            if (partner.Map != pawn.Map)
            {
                return null;
            }
        }

        return partner;
    }

    /// <summary>
    /// Checks whether or not the pawn would like to participate in lovin'.
    /// If the lovin was ordered, ignore checks for recreation or recent lovin'.
    /// </summary>
    public static bool CouldUseSomeLovin(Pawn pawn, bool orderedLovin = false)
    {
        return GetCouldUseSomeLovinFailureReason(pawn, orderedLovin) == null;
    }

    public static string GetShortLovinFailureReason(Pawn pawn, Pawn target, bool ordered = false)
    {
        if (ordered && TryGetManualLovinDisabledReason(pawn, target, out string manualReason))
        {
            return manualReason;
        }

        if (pawn == null || target == null)
        {
            return "Missing participants.";
        }

        if (target == pawn)
        {
            return "Same pawn.";
        }

        string reason = GetCouldUseSomeLovinFailureReason(pawn, ordered);
        if (reason != null)
        {
            return "Initiator " + reason;
        }

        reason = GetCouldUseSomeLovinFailureReason(target, ordered);
        if (reason != null)
        {
            return "Target " + reason;
        }

        reason = GetHealthCheckFailureReason(pawn);
        if (reason != null)
        {
            return "Initiator " + reason;
        }

        reason = GetHealthCheckFailureReason(target);
        if (reason != null)
        {
            return "Target " + reason;
        }

        if (!PassesOrientationCheck(pawn, target))
        {
            return "Orientation mismatch.";
        }

        if (!PassesRelationsCheck(pawn, target))
        {
            return "Attraction too low.";
        }

        if (!PassesIdeologyCheck(pawn, target))
        {
            return "Ideology forbids it.";
        }

        return null;
    }

    public static string GetReportForLovin(Pawn pawn, Pawn pawn2)
    {
        if (pawn == null)
        {
            return "Pawn doesn't exist";
        }

        return GetShortLovinFailureReason(pawn, pawn2);
    }


    internal static LovinTypeDef FindAutonomousLovinType(Pawn pawn, Pawn target)
    {
        if (pawn == null || target == null)
            return null;

        foreach (LovinTypeDef lovinType in DefDatabase<LovinTypeDef>.AllDefsListForReading)
        {
            if (lovinType == null || lovinType.isSolo)
                continue;

            if (ReproCompatibilityUtil.PairSatisfiesLovinTypeRequirements(pawn, target, lovinType))
                return lovinType;
        }

        return null;
    }

    internal static LovinTypeDef FindAutonomousSelfLovinType(Pawn pawn)
    {
        if (pawn == null)
            return null;

        foreach (LovinTypeDef lovinType in DefDatabase<LovinTypeDef>.AllDefsListForReading)
        {
            if (lovinType == null || !lovinType.isSolo)
                continue;

            if (!ReproCompatibilityUtil.PawnSatisfiesSoloLovinTypeRequirements(pawn, lovinType))
                continue;

            if (!TryGetAutonomousSelfLovinDisabledReason(pawn, lovinType, out _))
                return lovinType;
        }

        return null;
    }

    internal static void StampAutonomousLovinJob(Job job, Map map, LovinTypeDef lovinType)
    {
        if (job == null || map == null || lovinType == null)
            return;

        var store = Despicable.Core.InteractionInstanceStore.Get(map);
        if (store == null)
            return;

        string interactionId = lovinType.interaction?.defName ?? lovinType.defName;
        store.Set(job.loadID, interactionId);
        store.SetStage(job.loadID, lovinType.defName);
        store.SetChannel(job.loadID, Despicable.Core.Channels.Auto);
    }

}
