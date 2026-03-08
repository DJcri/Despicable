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
    foreach (var m in typeof(TendUtility).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
    {
        if (m == null) continue;
        if (!string.Equals(m.Name, "DoTend", StringComparison.Ordinal)) continue;

        var p = m.GetParameters();
        if (p == null || p.Length < 2) continue;

        if (!typeof(Pawn).IsAssignableFrom(p[0].ParameterType)) continue;
        if (!typeof(Pawn).IsAssignableFrom(p[1].ParameterType)) continue;

        yield return m;
    }
}

public static void Postfix(Pawn doctor, Pawn patient)
    {
        try
        {
            if (doctor == null || patient == null) return;
            if (!HKHookUtilSafe.ActorIsHero(doctor)) return;

            // outsider = not player faction; (includes prisoners/guests/neutral)
            if (patient.Faction != null && patient.Faction.IsPlayer) return;

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
    public static void Prefix(Pawn doctor, Pawn patient, ref bool __state)
    {
        __state = false;
        try
        {
            if (doctor == null || patient == null) return;
            if (!HKSettingsUtil.HookEnabled("TendOutsider") && !HKSettingsUtil.HookEnabled("StabilizeOutsider")) return;
            if (!HKHookUtilSafe.ActorIsHero(doctor)) return;
            if (patient.Faction != null && patient.Faction.IsPlayer) return;

            HKGoodwillContext.Begin(doctor);
            __state = true;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_TendOutsider:2", "HarmonyPatch_TendOutsider suppressed an exception.", ex);
        }
    }

    public static void Finalizer(Exception __exception, bool __state)
    {
        if (!__state) return;
        try { HKGoodwillContext.End(); }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_TendOutsider:3", "HarmonyPatch_TendOutsider suppressed an exception.", ex);
        }
    }
}
