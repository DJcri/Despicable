using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

[HarmonyPatch]
public static class HarmonyPatch_TendOutsider
{
    private const string PatchId = "HKPatch.TendOutsider";
    // Guardrail-Allow-Static: Cached Harmony target list for this patch class; populated during Prepare and reused for patch registration within the current load.
    private static List<MethodBase> _targets;

    [HarmonyPrepare]
private static bool Prepare()
{
    // RimWorld: TendUtility.DoTend(...) exists; signature/overloads shift by version.
    // Patch all overloads that begin with (Pawn doctor, Pawn patient, ...).
    return HKPatchGuard.PrepareMany(
        PatchId,
        "Tend outsider (DoTend)",
        featureKey: "CoreKarma",
        required: true,
        candidates: FindTargets(),
        cached: out _targets);
}

[HarmonyTargetMethods]
private static IEnumerable<MethodBase> TargetMethods()
{
    return _targets ?? (IEnumerable<MethodBase>)Array.Empty<MethodBase>();
}

private static IEnumerable<MethodBase> FindTargets()
{
    return HKPatchTargetUtil.FindMethods(
        typeof(TendUtility),
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
        method =>
        {
            if (!string.Equals(method.Name, "DoTend", StringComparison.Ordinal))
                return false;

            ParameterInfo[] parameters = method.GetParameters();
            return parameters != null
                && parameters.Length >= 2
                && typeof(Pawn).IsAssignableFrom(parameters[0].ParameterType)
                && typeof(Pawn).IsAssignableFrom(parameters[1].ParameterType);
        });
}

public static void Postfix(Pawn doctor, Pawn patient)
    {
        try
        {
            if (doctor == null || patient == null) return;
            if (!HKHookUtilSafe.ActorIsHero(doctor)) return;

            // Player-faction patients still affect Hero Karma; they simply have no non-player faction context.

            int factionId = HKHookUtil.GetFactionIdSafe(patient);
            string actorId = doctor.GetUniqueLoadID();
            string targetId = patient.GetUniqueLoadID();

            bool emergency = HKHookUtil.IsEmergencyTendTarget(patient);

            // Prefer StabilizeOutsider if it qualifies.
            if (emergency && HKSettingsUtil.HookEnabled("StabilizeOutsider"))
            {
                var sev = KarmaEvent.Create("StabilizeOutsider", doctor, patient, factionId);
                    HKKarmaProcessor.Process(sev);

                    if (HKPerkEffects.HasMercyMagnet(doctor))
                    {
                        HKHookUtil.TryAwardBonusGoodwill(doctor, patient, HKBalanceTuning.PerkBehavior.MercyMagnetEmergencyTendBonusGoodwill, "PerkMercyMagnet_Tend", HKBalanceTuning.PerkBehavior.MercyMagnetTendCooldownTicks);
                    }

                return;
            }

            if (!HKSettingsUtil.HookEnabled("TendOutsider")) return;

            
            var ev = KarmaEvent.Create("TendOutsider", doctor, patient, factionId);
            HKKarmaProcessor.Process(ev);

            if (HKPerkEffects.HasMercyMagnet(doctor))
            {
                int goodwill = emergency ? HKBalanceTuning.PerkBehavior.MercyMagnetEmergencyTendBonusGoodwill : HKBalanceTuning.PerkBehavior.MercyMagnetTendBonusGoodwill;
                HKHookUtil.TryAwardBonusGoodwill(doctor, patient, goodwill, "PerkMercyMagnet_Tend", HKBalanceTuning.PerkBehavior.MercyMagnetTendCooldownTicks);
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_TendOutsider:1", "HarmonyPatch_TendOutsider suppressed an exception.", ex);
        }
    }

    // Tending outsiders can trigger goodwill changes; attribute those to the hero when applicable.
    private static void Prefix(Pawn doctor, Pawn patient, ref HKGoodwillContext.Scope __state)
    {
        __state = default;
        try
        {
            if (doctor == null || patient == null) return;
            if (!HKSettingsUtil.HookEnabled("TendOutsider") && !HKSettingsUtil.HookEnabled("StabilizeOutsider")) return;
            if (!HKHookUtilSafe.ActorIsHero(doctor)) return;
            // Player-faction patients still affect Hero Karma; they simply have no non-player faction context.

            __state = HKGoodwillContext.Enter(doctor);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_TendOutsider:2", "HarmonyPatch_TendOutsider suppressed an exception.", ex);
        }
    }

    private static void Finalizer(Exception __exception, HKGoodwillContext.Scope __state)
    {
        try { __state.Dispose(); }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_TendOutsider:3", "HarmonyPatch_TendOutsider suppressed an exception.", ex);
        }
    }
}
