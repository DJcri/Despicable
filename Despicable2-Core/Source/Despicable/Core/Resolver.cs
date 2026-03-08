using RimWorld;
using Verse;

namespace Despicable.Core;
public static class Resolver
{
    // Core resolver is intentionally content-agnostic.
    // Modules are expected to complete job/animation selection via hooks.
    public static InteractionResolution Resolve(InteractionRequest req, InteractionContext ctx)
    {
        var res = new InteractionResolution
        {
            Allowed = true,
            Reason = null
        };

        if (req == null)
        {
            res.Allowed = false;
            res.Reason = "NullRequest";
            goto Done;
        }

        // Pre-resolve hooks (may mutate req/ctx or block)
        if (!Hooks.RunPreResolve(req, ctx, out var hookReason))
        {
            res.Allowed = false;
            res.Reason = hookReason ?? "BlockedByPreResolveHook";
            res.ChosenInteractionId = null;
            res.ChosenCommand = null;
            res.ChosenJobDef = null;
            res.ChosenInteractionDef = null;
            res.ChosenStageId = null;

            DebugLogger.Debug($"Resolve blocked by hook reason={res.Reason}");
            goto Done;
        }

        // Carry through opaque stage id by default. Hooks may override.
        res.ChosenStageId = req.RequestedStageId;

        // 1) Command path
        if (!req.RequestedCommand.NullOrEmpty())
        {
            res.Allowed = true;
            res.ChosenCommand = req.RequestedCommand;
            res.ChosenInteractionId = req.RequestedCommand;
            res.ChosenJobDef = null;
            res.ChosenInteractionDef = null;

            DebugLogger.Debug($"Resolve (command) allowed={res.Allowed} cmd={res.ChosenCommand}");
            goto Done;
        }

        // 2) Direct interaction path (vanilla social interaction)
        if (req.RequestedInteractionDef != null)
        {
            res.Allowed = true;
            res.ChosenInteractionDef = req.RequestedInteractionDef;
            res.ChosenInteractionId = req.RequestedInteractionDef.defName;
            res.ChosenCommand = null;
            res.ChosenJobDef = null;

            DebugLogger.Debug($"Resolve (direct) allowed={res.Allowed} interaction={res.ChosenInteractionDef.defName}");
            goto Done;
        }

        // 3) Generic ID path (modules resolve this into jobs/defs via hooks)
        if (!req.RequestedInteractionId.NullOrEmpty())
        {
            res.Allowed = true;
            res.ChosenInteractionId = req.RequestedInteractionId;
            res.ChosenCommand = null;
            res.ChosenJobDef = null;
            res.ChosenInteractionDef = null;

            DebugLogger.Debug($"Resolve (id) allowed={res.Allowed} id={res.ChosenInteractionId} stage={res.ChosenStageId}");
            goto Done;
        }

        // Nothing to do.
        res.Allowed = false;
        res.Reason = "Unresolved";

    Done:
        Hooks.RunPostResolve(req, ctx, res);

        // Final sanity check after PostResolve hooks had a chance to fill fields.
        if (res.Allowed)
        {
            bool hasAction =
                !res.ChosenCommand.NullOrEmpty()
                || res.ChosenInteractionDef != null
                || res.ChosenJobDef != null;

            if (!hasAction)
            {
                res.Allowed = false;
                res.Reason ??= "Unresolved";
            }
        }

        return res;
    }
}
