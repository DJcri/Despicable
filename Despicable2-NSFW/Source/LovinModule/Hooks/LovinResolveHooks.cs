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

        if (!IsLovinRequest(req))
            return true;

        var a = req?.Initiator;
        var b = req?.Recipient;
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

        if (!req.RequestedStageId.NullOrEmpty())
        {
            var lovinType = DefDatabase<LovinTypeDef>.GetNamedSilentFail(req.RequestedStageId);
            if (lovinType != null && !ReproCompatibilityUtil.PairSatisfiesLovinTypeRequirements(a, b, lovinType))
            {
                outReason = "Selected lovin type anatomy requirements are not met.";
                return false;
            }
        }

        return true;
    }

    private static bool IsLovinRequest(InteractionRequest req)
    {
        if (req == null) return false;

        if (req.Channel == Channels.ManualLovin)
            return true;

        // If a stage id matches a LovinTypeDef, treat it as NSFW-lovin.
        if (!req.RequestedStageId.NullOrEmpty())
            return DefDatabase<LovinTypeDef>.GetNamedSilentFail(req.RequestedStageId) != null;

        return false;
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

        if (!IsLovinRequest(req))
            return;

        // If Core already blocked it, do not override.
        if (!res.Allowed)
            return;

        // Stage id is an opaque key in Core; for NSFW it's the LovinTypeDef.defName.
        if (!req.RequestedStageId.NullOrEmpty())
            res.ChosenStageId = req.RequestedStageId;

        LovinTypeDef lovinType = null;
        if (!res.ChosenStageId.NullOrEmpty())
            lovinType = DefDatabase<LovinTypeDef>.GetNamedSilentFail(res.ChosenStageId);

        // Ensure an interaction is chosen when the lovin type carries one.
        if (res.ChosenInteractionDef == null && lovinType?.interaction != null)
            res.ChosenInteractionDef = lovinType.interaction;

        if (res.ChosenInteractionId.NullOrEmpty())
            res.ChosenInteractionId = res.ChosenInteractionDef?.defName ?? req.RequestedInteractionId;

        // Choose the initiator job (partner will be assigned Job_GiveLovin by the driver).
        if (res.ChosenJobDef == null)
        {
            bool bedLovin = ctx.InitiatorInBed || ctx.RecipientInBed;

            res.ChosenJobDef = bedLovin
                ? LovinModule_JobDefOf.Job_GetBedLovin
                : LovinModule_JobDefOf.Job_GetLovin;
        }
    }

    private static bool IsLovinRequest(InteractionRequest req)
    {
        if (req == null) return false;

        if (req.Channel == Channels.ManualLovin)
            return true;

        if (!req.RequestedStageId.NullOrEmpty())
            return DefDatabase<LovinTypeDef>.GetNamedSilentFail(req.RequestedStageId) != null;

        return false;
    }
}
