using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Despicable;
public class InteractionMenu
{
    private static TargetingParameters TargetParameters => InteractionTargetingCache.ForHumanlikePawns;

    public static void InitInteractionMenu(Pawn pawn, List<FloatMenuOption> opts, Vector3 clickPos)
    {
        IEnumerable<LocalTargetInfo> validTargets = GenUI.TargetsAt(clickPos, TargetParameters);

        // Defensive de-duplication: depending on selection state and target layers,
        // GenUI.TargetsAt can yield the same pawn more than once, and/or multiple
        // Harmony patches can call into this method.
        var addedTargets = new HashSet<int>();

            // The user has clicked on something and the loop is running.
            foreach (LocalTargetInfo target in validTargets)
            {
                // A quick way to get the pawn if it exists, otherwise it will be null.
                Pawn targetPawn = target.Pawn;

                // Only proceed if the target is a visible, spawned humanlike pawn.
                if (!PawnQuery.IsVisibleHumanlikeSpawned(targetPawn))
                    continue;

                // Check if the pawn can be reached.
                if (!pawn.CanReach(target, PathEndMode.ClosestTouch, Danger.Deadly))
                    continue;

                // Handle same-pawn case separately.
                if (pawn == targetPawn)
                {
                    // Actions for self
                    // ...
                }
                else
                {
                    CommonUtil.DebugLog("Creating interaction option");

                    // Prevent duplicates for the same target pawn.
                    if (!addedTargets.Add(targetPawn.thingIDNumber))
                        continue;

                    string label = "InteractionCategory".Translate(targetPawn.Name.ToStringShort);

                    // If something already added a matching option, don't add another.
                    // (We use StartsWith because DecoratePrioritizedTask can alter the label.)
                    if (opts.Any(o => o?.Label != null && o.Label.StartsWith(label)))
                        continue;

                    // The previous checks already ensured this is a humanlike pawn.
                    FloatMenuOption option = FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, delegate ()
                    {
                        ManualMenuRequest request = BuildSocialMenuRequest(pawn, target);
                        ManualMenuHost.Open(request);
                    }, targetPawn, Color.white, MenuOptionPriority.High), pawn, target);
                    opts.Add(option);
                }
            }
    }

    private static ManualMenuRequest BuildSocialMenuRequest(Pawn pawn, LocalTargetInfo target)
    {
        return new ManualMenuRequest("ManualInteraction/Social", GenerateSocialOptionSpecs(pawn, target))
        {
            GivesColonistOrders = false,
            VanishIfMouseDistant = false
        };
    }

    private static IEnumerable<ManualMenuOptionSpec> GenerateSocialOptionSpecs(Pawn pawn, LocalTargetInfo target)
    {
        var specs = new List<ManualMenuOptionSpec>();
        Pawn targetPawn = target.Pawn;

        if (pawn == null || targetPawn == null)
            return specs;

        if (PawnPairQuery.AreHostile(pawn, targetPawn))
            return specs;

        bool canInteract = pawn.interactions.CanInteractNowWith(targetPawn);
        if (!canInteract)
        {
            specs.Add(BuildDisabledSocialOption(
                IsTooFarAwayForSocialInteraction(pawn, targetPawn)
                    ? "No social interactions available (too far away)"
                    : "No social interactions available",
                targetPawn));
            return specs;
        }

        specs.Add(BuildSocialOption(
            "InteractionOption_SocialFight".Translate(targetPawn.Name.ToStringShort),
            () =>
            {
                var res = Despicable.Core.InteractionEntry.ResolveManual(
                    pawn,
                    targetPawn,
                    Despicable.Core.Channels.ManualSocial,
                    req => req.RequestedCommand = Despicable.Core.Commands.SocialFight
                );

                if (!res.Allowed || res.ChosenCommand != Despicable.Core.Commands.SocialFight)
                    return;

                pawn.interactions.StartSocialFight(targetPawn);
            },
            targetPawn));

        specs.Add(BuildSocialOption(
            "InteractionOption_Insult".Translate(targetPawn.Name.ToStringShort),
            () =>
            {
                var res = Despicable.Core.InteractionEntry.ResolveManual(
                    pawn,
                    targetPawn,
                    Despicable.Core.Channels.ManualSocial,
                    req => req.RequestedInteractionDef = InteractionDefOf.Insult
                );

                if (!res.Allowed || res.ChosenInteractionDef == null)
                    return;

                pawn.interactions.TryInteractWith(targetPawn, res.ChosenInteractionDef);
            },
            targetPawn));

        specs.Add(BuildSocialOption(
            "InteractionOption_Chat".Translate(targetPawn.Name.ToStringShort),
            () =>
            {
                var res = Despicable.Core.InteractionEntry.ResolveManual(
                    pawn,
                    targetPawn,
                    Despicable.Core.Channels.ManualSocial,
                    req => req.RequestedInteractionDef = InteractionDefOf.Chitchat
                );

                if (!res.Allowed || res.ChosenInteractionDef == null)
                    return;

                pawn.interactions.TryInteractWith(targetPawn, res.ChosenInteractionDef);
            },
            targetPawn));

        if (pawn.relations.DirectRelationExists(PawnRelationDefOf.Lover, targetPawn)
            || pawn.relations.DirectRelationExists(PawnRelationDefOf.Fiance, targetPawn))
        {
            specs.Add(BuildSocialOption(
                "InteractionOption_Marriage".Translate(targetPawn.Name.ToStringShort),
                () =>
                {
                    var res = Despicable.Core.InteractionEntry.ResolveManual(
                        pawn,
                        targetPawn,
                        Despicable.Core.Channels.ManualSocial,
                        req => req.RequestedInteractionDef = InteractionDefOf.MarriageProposal
                    );

                    if (!res.Allowed || res.ChosenInteractionDef == null)
                        return;

                    pawn.interactions.TryInteractWith(targetPawn, res.ChosenInteractionDef);
                },
                targetPawn));
        }

        // HeroModule legacy abilities removed from compile path (reference-only).
        return specs;
    }

    private static ManualMenuOptionSpec BuildSocialOption(string label, System.Action action, Pawn targetPawn)
    {
        return new ManualMenuOptionSpec
        {
            Label = label,
            Action = action,
            Priority = MenuOptionPriority.High,
            RevalidateClickTarget = targetPawn,
            IconThing = targetPawn,
            IconColor = Color.white
        };
    }

    private static ManualMenuOptionSpec BuildDisabledSocialOption(string label, Pawn targetPawn)
    {
        return new ManualMenuOptionSpec
        {
            Label = label,
            Action = null,
            Disabled = true,
            DisabledReason = label,
            Priority = MenuOptionPriority.High,
            RevalidateClickTarget = targetPawn,
            IconThing = targetPawn,
            IconColor = Color.white
        };
    }

    private static bool IsTooFarAwayForSocialInteraction(Pawn pawn, Pawn targetPawn)
    {
        if (pawn == null || targetPawn == null)
            return false;

        if (!pawn.Spawned || !targetPawn.Spawned || pawn.MapHeld != targetPawn.MapHeld)
            return false;

        if (pawn.PositionHeld == targetPawn.PositionHeld)
            return false;

        return !pawn.PositionHeld.AdjacentTo8WayOrInside(targetPawn.PositionHeld);
    }

    public static List<FloatMenuOption> GetHeroAbilityOptions(Pawn pawn, LocalTargetInfo target)
    {
        // HeroModule legacy abilities removed from compile path (reference-only).
        return new List<FloatMenuOption>();
    }

}
