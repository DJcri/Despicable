using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Despicable.Core;
using Despicable.NSFW.Integrations;
using Despicable.NSFW.Integrations.Intimacy;

namespace Despicable;
public class LovinInteractions
{
    public static List<ManualMenuOptionSpec> GenerateLovinOptionSpecs(Pawn pawn, LocalTargetInfo target)
    {
        List<ManualMenuOptionSpec> opts = new();
        Pawn targetPawn = target.Pawn;
        if (pawn == null || targetPawn == null)
            return opts;

        if (pawn.HostileTo(targetPawn))
            return opts;

        foreach (LovinTypeDef lovinTypeDef in DefDatabase<LovinTypeDef>.AllDefsListForReading.ToList())
        {
            if (!ReproCompatibilityUtil.PairSatisfiesLovinTypeRequirements(pawn, targetPawn, lovinTypeDef))
                continue;

            string optionLabel = lovinTypeDef.ResolvePlayerFacingLabel();
            Texture2D menuIcon = lovinTypeDef.ResolveMenuIcon();

            Action action = delegate ()
            {
                if (IntegrationGuards.ShouldUseIntimacyForLovinValidation()
                    && !IntimacyValidationBridge.PassesManualLovinCheck(pawn, targetPawn, out string blockedReason))
                {
                    Messages.Message(blockedReason ?? "D2N_LovinReason_Unknown".Translate(), targetPawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                if (!InteractionEntry.TryPrepareManual(
                    pawn,
                    targetPawn,
                    Despicable.Core.Channels.ManualLovin,
                    req =>
                    {
                        req.RequestedInteractionId = lovinTypeDef?.interaction?.defName ?? lovinTypeDef?.defName;
                        req.RequestedStageId = lovinTypeDef?.defName;
                    },
                    out var req,
                    out var ctx))
                {
                    return;
                }

                var res = Resolver.Resolve(req, ctx);

                Despicable.Core.DebugLogger.Debug(
                    $"ManualLovinClick req={req.RequestedInteractionId} " +
                    $"A={pawn.ThingID}:{pawn.LabelShort} B={targetPawn.ThingID}:{targetPawn.LabelShort} " +
                    $"tick={ctx.Tick} bedB={ctx.RecipientInBed} hostile={ctx.InitiatorHostileToRecipient}");

                JobDef fallbackJob = (ctx.InitiatorInBed || ctx.RecipientInBed)
                    ? LovinModule_JobDefOf.Job_GetBedLovin
                    : LovinModule_JobDefOf.Job_GetLovin;

                Interactions.OrderedJob(fallbackJob, pawn, target, req, ctx);
            };

            opts.Add(new ManualMenuOptionSpec
            {
                Label = optionLabel,
                Action = action,
                Priority = MenuOptionPriority.High,
                RevalidateClickTarget = targetPawn,
                IconTex = menuIcon,
                IconThing = menuIcon == null ? targetPawn : null,
                IconColor = Color.white
            });
        }

        return opts;
    }

    public static List<FloatMenuOption> GenerateLovinOptions(Pawn pawn, LocalTargetInfo target)
    {
        return ManualMenuBuilder.BuildOptions(GenerateLovinOptionSpecs(pawn, target));
    }
}
