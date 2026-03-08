using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
/// <summary>
/// NSFW contribution point: inject "Do lovin' with X" into the Core interaction submenu.
/// This keeps Core SFW and lets NSFW extend via hooks/patches.
/// </summary>
[HarmonyPatch]
internal static class HarmonyPatch_InteractionMenu_GenerateSocialOptions
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        // Core evolved from a direct FloatMenu builder to a ManualMenuOptionSpec pipeline.
        // Target the current seam if present; yield none (no patch) if the method doesn't exist.
        var m = AccessTools.Method(typeof(InteractionMenu), "GenerateSocialOptionSpecs");
        if (m != null)
            yield return m;
    }

    private static void Postfix(Pawn pawn, LocalTargetInfo target, ref IEnumerable<ManualMenuOptionSpec> __result)
    {
        // Ensure we can safely append.
        var list = __result?.ToList() ?? new List<ManualMenuOptionSpec>();

        if (pawn == null)
        {
            __result = list;
            return;
        }

        var targetPawn = target.Pawn;
        if (targetPawn == null)
        {
            __result = list;
            return;
        }

        if (pawn == targetPawn) { __result = list; return; }
        if (pawn.HostileTo(targetPawn)) { __result = list; return; }
        // Visibility gate only. When the user disables the hide-setting, manual lovin should remain available.
        if (Despicable.NSFW.Integrations.IntegrationGuards.ShouldHideManualLovinOptionWithIntimacy())
        {
            __result = list;
            return;
        }

        // Avoid duplicates if multiple patches ever add the same thing.
        string dupPrefix = "D2N_CODE_90731E3E".Translate();
        if (list.Any(o => o != null && o.Label != null && o.Label.StartsWith(dupPrefix, StringComparison.OrdinalIgnoreCase)))
        {
            __result = list;
            return;
        }

        string targetName = targetPawn.Name?.ToStringShort ?? targetPawn.LabelShort;

        // Determine whether the general entry should be enabled.
        bool disabled = LovinUtil.TryGetManualLovinDisabledReason(pawn, targetPawn, out string reason);

        List<ManualMenuOptionSpec> lovinOptionSpecs = null;
        if (!disabled)
        {
            try
            {
                lovinOptionSpecs = LovinInteractions.GenerateLovinOptionSpecs(pawn, target);
            }
            catch (Exception e)
            {
                Log.Error($"[Despicable2.NSFW] Failed generating lovin options: {e}");
                disabled = true;
                reason = "D2N_LovinReason_Unknown".Translate();
            }

            // If nothing is anatomically compatible, present as disabled.
            if (!disabled && lovinOptionSpecs.NullOrEmpty())
            {
                disabled = true;
                reason = "D2N_LovinReason_NoCompatibleTypes".Translate();
            }
        }

        var label = disabled
            ? "D2N_Lovin_DoWithReason".Translate(targetName, reason)
            : "D2N_CODE_C1CB4969".Translate(targetName);

        Action action = null;
        if (!disabled)
        {
            action = () =>
            {
                var req = new ManualMenuRequest();
                foreach (ManualMenuOptionSpec option in lovinOptionSpecs.Where(o => o?.Action != null))
                {
                    req.Options.Add(option);
                }

                ManualMenuHost.Open(req);
            };
        }

        list.Add(new ManualMenuOptionSpec
        {
            Label = label,
            Priority = MenuOptionPriority.High,
            RevalidateClickTarget = targetPawn,
            IconThing = targetPawn,   // vanilla-like: show the target pawn as the icon
            Action = action           // null action => vanilla disabled entry
        });

        __result = list;
    }
}
