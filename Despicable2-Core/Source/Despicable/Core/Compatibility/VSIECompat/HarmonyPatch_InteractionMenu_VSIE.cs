using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Despicable.Core.Compatibility.VSIECompat;
/// <summary>
/// Optional VSIE additions to the directed social submenu.
/// We append player-facing manual entries only when VSIE is installed, while keeping
/// runtime checks authoritative so transient state changes cannot trigger brittle jobs.
/// </summary>
[HarmonyPatch]
internal static class HarmonyPatch_InteractionMenu_VSIE
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        var method = AccessTools.Method(typeof(InteractionMenu), "GenerateSocialOptionSpecs");
        if (method != null)
            yield return method;
    }

    private static void Postfix(Pawn pawn, LocalTargetInfo target, ref IEnumerable<ManualMenuOptionSpec> __result)
    {
        List<ManualMenuOptionSpec> list = __result?.ToList() ?? new List<ManualMenuOptionSpec>();
        Pawn targetPawn = target.Pawn;

        if (!VSIECompatUtility.CanAddManualOptionsFor(pawn, targetPawn))
        {
            __result = list;
            return;
        }

        AddActivityOption(list, pawn, targetPawn);
        AddVentOption(list, pawn, targetPawn);
        AddTeachingOption(list, pawn, targetPawn);

        __result = list;
    }

    private static void AddActivityOption(List<ManualMenuOptionSpec> list, Pawn pawn, Pawn targetPawn)
    {
        string targetName = targetPawn.Name?.ToStringShort ?? targetPawn.LabelShort;
        if (ContainsOption(list, "InteractionOption_VSIE_ActivityMenu", targetName))
            return;

        bool canOpen = VSIECompatUtility.CanOpenDirectedActivityMenu(pawn, targetPawn, out string disabledReason);

        list.Add(new ManualMenuOptionSpec
        {
            Label = canOpen
                ? "InteractionOption_VSIE_ActivityMenu".Translate(targetName)
                : "InteractionOption_VSIE_ActivityMenu_Disabled".Translate(targetName, disabledReason),
            Action = canOpen ? (Action)(() => OpenActivityMenu(pawn, targetPawn)) : null,
            Disabled = !canOpen,
            DisabledReason = canOpen ? null : disabledReason,
            Priority = MenuOptionPriority.High,
            RevalidateClickTarget = targetPawn,
            IconThing = targetPawn,
            IconColor = Color.white
        });
    }

    private static void AddVentOption(List<ManualMenuOptionSpec> list, Pawn pawn, Pawn targetPawn)
    {
        string targetName = targetPawn.Name?.ToStringShort ?? targetPawn.LabelShort;
        if (ContainsOption(list, "InteractionOption_VSIE_Vent", targetName))
            return;

        bool canVent = VSIECompatUtility.CanOrderVent(pawn, targetPawn, out string disabledReason);

        list.Add(new ManualMenuOptionSpec
        {
            Label = canVent
                ? "InteractionOption_VSIE_Vent".Translate(targetName)
                : "InteractionOption_VSIE_Vent_Disabled".Translate(targetName, disabledReason),
            Action = canVent ? (Action)(() => TryOrderVent(pawn, targetPawn)) : null,
            Disabled = !canVent,
            DisabledReason = canVent ? null : disabledReason,
            Priority = MenuOptionPriority.High,
            RevalidateClickTarget = targetPawn,
            IconThing = targetPawn,
            IconColor = Color.white
        });
    }

    private static void AddTeachingOption(List<ManualMenuOptionSpec> list, Pawn pawn, Pawn targetPawn)
    {
        string targetName = targetPawn.Name?.ToStringShort ?? targetPawn.LabelShort;
        if (ContainsOption(list, "InteractionOption_VSIE_Teach", targetName))
            return;

        bool canTeach = VSIECompatUtility.CanOrderTeaching(pawn, targetPawn, out string disabledReason);

        list.Add(new ManualMenuOptionSpec
        {
            Label = canTeach
                ? "InteractionOption_VSIE_Teach".Translate(targetName)
                : "InteractionOption_VSIE_Teach_Disabled".Translate(targetName, disabledReason),
            Action = canTeach ? (Action)(() => OpenTeachingSkillMenu(pawn, targetPawn)) : null,
            Disabled = !canTeach,
            DisabledReason = canTeach ? null : disabledReason,
            Priority = MenuOptionPriority.High,
            RevalidateClickTarget = targetPawn,
            IconThing = targetPawn,
            IconColor = Color.white
        });
    }

    private static void OpenActivityMenu(Pawn pawn, Pawn targetPawn)
    {
        if (!VSIECompatUtility.CanOpenDirectedActivityMenu(pawn, targetPawn, out _))
            return;

        List<GatheringDef> gatheringDefs = VSIECompatUtility.GetAvailableDirectedActivities(pawn, targetPawn);
        if (gatheringDefs.Count == 0)
            return;

        var options = new List<ManualMenuOptionSpec>();
        for (int i = 0; i < gatheringDefs.Count; i++)
        {
            GatheringDef localGatheringDef = gatheringDefs[i];
            options.Add(new ManualMenuOptionSpec
            {
                Label = localGatheringDef.LabelCap,
                Action = () => VSIECompatUtility.TryExecuteDirectedActivity(pawn, targetPawn, localGatheringDef),
                Priority = MenuOptionPriority.High,
                RevalidateClickTarget = targetPawn,
                IconThing = targetPawn,
                IconColor = Color.white
            });
        }

        ManualMenuHost.Open(new ManualMenuRequest("ManualInteraction/Social/VSIE/Activities", options)
        {
            GivesColonistOrders = false,
            VanishIfMouseDistant = false
        });
    }

    private static void TryOrderVent(Pawn pawn, Pawn targetPawn)
    {
        if (!VSIECompatUtility.CanOrderVent(pawn, targetPawn, out _))
            return;

        var resolution = Despicable.Core.InteractionEntry.ResolveManual(
            pawn,
            targetPawn,
            Despicable.Core.Channels.ManualSocial,
            req => req.RequestedCommand = VSIECompatUtility.VentCommandId);

        if (!resolution.Allowed || resolution.ChosenCommand != VSIECompatUtility.VentCommandId)
            return;

        JobDef jobDef = VSIECompatUtility.VentToFriendJobDef;
        if (jobDef == null || pawn.jobs == null)
            return;

        Job job = JobMaker.MakeJob(jobDef, targetPawn);
        pawn.jobs.TryTakeOrderedJob(job);
    }

    private static void OpenTeachingSkillMenu(Pawn pawn, Pawn targetPawn)
    {
        if (!VSIECompatUtility.CanOrderTeaching(pawn, targetPawn, out _))
            return;

        List<SkillDef> skillChoices = VSIECompatUtility.GetTeachingSkillChoices(pawn, targetPawn);
        if (skillChoices.NullOrEmpty())
            return;

        var options = new List<ManualMenuOptionSpec>();
        foreach (SkillDef skillDef in skillChoices)
        {
            SkillDef localSkillDef = skillDef;
            options.Add(new ManualMenuOptionSpec
            {
                Label = localSkillDef.LabelCap,
                Action = () => TryTeachSkill(pawn, targetPawn, localSkillDef),
                Priority = MenuOptionPriority.High,
                RevalidateClickTarget = targetPawn,
                IconThing = targetPawn,
                IconColor = Color.white,
                Tooltip = "InteractionOption_VSIE_TeachSkill_Tooltip".Translate(localSkillDef.LabelCap, targetPawn.Name?.ToStringShort ?? targetPawn.LabelShort)
            });
        }

        ManualMenuHost.Open(new ManualMenuRequest("ManualInteraction/Social/VSIE/TeachSkill", options)
        {
            GivesColonistOrders = false,
            VanishIfMouseDistant = false
        });
    }

    private static void TryTeachSkill(Pawn pawn, Pawn targetPawn, SkillDef skillDef)
    {
        InteractionDef teachingDef = VSIECompatUtility.TeachingInteractionDef;
        if (teachingDef == null || skillDef == null)
            return;

        if (!VSIECompatUtility.CanOrderTeaching(pawn, targetPawn, out _))
            return;

        if (!VSIECompatUtility.GetTeachingSkillChoices(pawn, targetPawn).Contains(skillDef))
            return;

        var resolution = Despicable.Core.InteractionEntry.ResolveManual(
            pawn,
            targetPawn,
            Despicable.Core.Channels.ManualSocial,
            req => req.RequestedInteractionDef = teachingDef);

        if (!resolution.Allowed || resolution.ChosenInteractionDef != teachingDef)
            return;

        if (!VSIECompatUtility.TryPrimeTeachingTopic(pawn, targetPawn, skillDef) || pawn.interactions == null)
            return;

        pawn.interactions.TryInteractWith(targetPawn, teachingDef);
    }

    private static bool ContainsOption(IEnumerable<ManualMenuOptionSpec> options, string translationKey, string targetName)
    {
        string labelPrefix = translationKey.Translate(targetName).RawText;
        return options.Any(option => option?.Label != null && option.Label.StartsWith(labelPrefix, StringComparison.OrdinalIgnoreCase));
    }
}
