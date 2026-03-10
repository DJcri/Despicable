using RimWorld;
using Verse;
using Despicable.Core;
using Despicable.NSFW.Integrations;

namespace Despicable;
/// <summary>
/// Blocks lovin interactions when NSFW-specific requirements fail.
/// </summary>
public sealed class LovinPreResolveHook : IPreResolveHook
{
    public bool PreResolve(InteractionRequest req, InteractionContext ctx, out string outReason)
    {
        outReason = null;

        if (!TryGetRequestedLovinType(req, out LovinTypeDef lovinType))
            return true;

        bool selfLovin = IsSelfLovinRequest(req, lovinType);
        var a = req?.Initiator;
        var b = req?.Recipient;

        if (selfLovin)
        {
            if (a == null)
            {
                outReason = "D2N_LovinReason_IsMissing".Translate();
                return false;
            }

            if (!LovinUtil.PassesSelfLovinCheck(a, lovinType, out string selfReason))
            {
                outReason = selfReason ?? "D2N_LovinReason_Unknown".Translate();
                return false;
            }

            return true;
        }

        if (a == null || b == null)
        {
            outReason = "Missing participants.";
            return false;
        }

        bool manualLovin = req.IsManual || req.Channel == Channels.ManualLovin;

        if (!manualLovin && IntegrationGuards.ShouldDeferLovinToIntimacy())
        {
            outReason = "D2N_LovinReason_IntimacyInstalled".Translate();
            return false;
        }

        if (manualLovin)
        {
            if (!LovinUtil.PassesManualLovinCheck(a, b, out string manualReason))
            {
                outReason = manualReason ?? "D2N_LovinReason_Unknown".Translate();
                return false;
            }
        }
        else if (!LovinUtil.PassesLovinCheck(a, b, ordered: false))
        {
            outReason = LovinUtil.GetShortLovinFailureReason(a, b, ordered: false) ?? "Not a good time for lovin'.";
            return false;
        }

        if (lovinType != null && !lovinType.isSolo && !ReproCompatibilityUtil.PairSatisfiesLovinTypeRequirements(a, b, lovinType))
        {
            outReason = "Selected lovin type anatomy requirements are not met.";
            return false;
        }

        return true;
    }

    private static bool TryGetRequestedLovinType(InteractionRequest req, out LovinTypeDef lovinType)
    {
        lovinType = null;
        if (req == null)
            return false;

        if (!req.RequestedStageId.NullOrEmpty())
            lovinType = DefDatabase<LovinTypeDef>.GetNamedSilentFail(req.RequestedStageId);

        return req.Channel == Channels.ManualLovin
            || req.Channel == Channels.ManualSelfLovin
            || lovinType != null;
    }

    private static bool IsSelfLovinRequest(InteractionRequest req, LovinTypeDef lovinType)
    {
        return req?.Channel == Channels.ManualSelfLovin || lovinType?.isSolo == true;
    }
}

/// <summary>
/// Completes Core resolution for lovin by selecting job/interaction + tagging the chosen stage id.
/// </summary>
public sealed class LovinPostResolveHook : IPostResolveHook
{
    public void PostResolve(InteractionRequest req, InteractionContext ctx, InteractionResolution res)
    {
        if (req == null || ctx == null || res == null)
            return;

        if (!TryGetRequestedLovinType(req, out LovinTypeDef lovinType))
            return;

        if (!res.Allowed)
            return;

        if (!req.RequestedStageId.NullOrEmpty())
            res.ChosenStageId = req.RequestedStageId;

        if (lovinType == null && !res.ChosenStageId.NullOrEmpty())
            lovinType = DefDatabase<LovinTypeDef>.GetNamedSilentFail(res.ChosenStageId);

        if (res.ChosenInteractionDef == null && lovinType?.interaction != null)
            res.ChosenInteractionDef = lovinType.interaction;

        if (res.ChosenInteractionId.NullOrEmpty())
            res.ChosenInteractionId = res.ChosenInteractionDef?.defName ?? req.RequestedInteractionId;

        if (res.ChosenJobDef != null)
            return;

        if (IsSelfLovinRequest(req, lovinType))
        {
            res.ChosenJobDef = LovinModule_JobDefOf.Job_SelfLovin;
            return;
        }

        bool bedLovin = ctx.InitiatorInBed || ctx.RecipientInBed;
        res.ChosenJobDef = bedLovin
            ? LovinModule_JobDefOf.Job_GetBedLovin
            : LovinModule_JobDefOf.Job_GetLovin;
    }

    private static bool TryGetRequestedLovinType(InteractionRequest req, out LovinTypeDef lovinType)
    {
        lovinType = null;
        if (req == null)
            return false;

        if (!req.RequestedStageId.NullOrEmpty())
            lovinType = DefDatabase<LovinTypeDef>.GetNamedSilentFail(req.RequestedStageId);

        return req.Channel == Channels.ManualLovin
            || req.Channel == Channels.ManualSelfLovin
            || lovinType != null;
    }

    private static bool IsSelfLovinRequest(InteractionRequest req, LovinTypeDef lovinType)
    {
        return req?.Channel == Channels.ManualSelfLovin || lovinType?.isSolo == true;
    }
}
