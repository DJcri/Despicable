using RimWorld;
using System;
using Verse;

namespace Despicable.Core;
public static class InteractionEntry
{
    /// <summary>
    /// Canonical builder for manual interactions. Builds req/ctx in one place so callers do not drift.
    /// Caller supplies how to configure the request (RequestedInteractionDef, RequestedCommand, stage id, etc.)
    /// </summary>

    // NOTE: Manual interaction callers must use InteractionEntry to prepare or resolve requests.
    // Menu code must not construct InteractionRequest/Context directly.
    public static bool TryPrepareManual(
        Pawn initiator,
        Pawn recipient,
        string channel,
        Action<InteractionRequest> configureRequest,
        out InteractionRequest req,
        out InteractionContext ctx)
    {
        req = null;
        ctx = null;

        if (initiator == null || recipient == null)
            return false;

        req = new InteractionRequest(
            initiator,
            recipient,
            requestedInteractionId: null,
            isManual: true
        );

        req.Channel = channel;
        configureRequest?.Invoke(req);

        ctx = new InteractionContext
        {
            Map = initiator.Map,
            Tick = Find.TickManager.TicksGame,

            InitiatorDrafted = initiator.Drafted,
            RecipientDrafted = recipient.Drafted,

            InitiatorInBed = global::Despicable.PawnBedQuery.IsInBed(initiator),
            RecipientInBed = global::Despicable.PawnBedQuery.IsInBed(recipient),

            InitiatorHostileToRecipient = global::Despicable.PawnPairQuery.AreHostile(initiator, recipient)
        };

        return true;
    }


    public static bool TryPrepareManualSelf(
        Pawn initiator,
        string channel,
        Action<InteractionRequest> configureRequest,
        out InteractionRequest req,
        out InteractionContext ctx)
    {
        req = null;
        ctx = null;

        if (initiator == null)
            return false;

        req = new InteractionRequest(
            initiator,
            initiator,
            requestedInteractionId: null,
            isManual: true
        );

        req.Channel = channel;
        configureRequest?.Invoke(req);

        bool initiatorInBed = global::Despicable.PawnBedQuery.IsInBed(initiator);

        ctx = new InteractionContext
        {
            Map = initiator.Map,
            Tick = Find.TickManager.TicksGame,

            InitiatorDrafted = initiator.Drafted,
            RecipientDrafted = initiator.Drafted,

            InitiatorInBed = initiatorInBed,
            RecipientInBed = initiatorInBed,

            InitiatorHostileToRecipient = false
        };

        return true;
    }

    public static InteractionResolution ResolveManual(
        Pawn initiator,
        Pawn recipient,
        string channel,
        Action<InteractionRequest> configureRequest)
    {
        if (!TryPrepareManual(initiator, recipient, channel, configureRequest, out var req, out var ctx))
            return new InteractionResolution { Allowed = false, Reason = "NullPawn" };

        return Resolver.Resolve(req, ctx);
    }

    public static InteractionResolution ResolveManualSelf(
        Pawn initiator,
        string channel,
        Action<InteractionRequest> configureRequest)
    {
        if (!TryPrepareManualSelf(initiator, channel, configureRequest, out var req, out var ctx))
            return new InteractionResolution { Allowed = false, Reason = "NullPawn" };

        return Resolver.Resolve(req, ctx);
    }

}
