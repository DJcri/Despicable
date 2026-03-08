using System;
using System.Collections.Generic;
using HarmonyLib;
using Despicable.HeroKarma;

using RimWorld;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

[HarmonyPatch]
public static partial class HarmonyPatch_AttackNeutral
{
    private const int InitiationMemoryTicks = 2500; // ~40s
    private const int MaxEntries = 4096;
    private const int StaleTicks = 10000; // > InitiationMemoryTicks; prune stale attacker/victim pairs

    private static readonly Dictionary<string, int> InitiatedTicksByInteractionKey = new(1024);
    private static readonly HKPruneTickTracker PruneTracker = new();

    private struct AttackState
    {
        public bool goodwillBegun;
        public bool victimWasDowned;
        public bool victimWasHostile;
        public bool victimWasGuestLike;
        public bool victimFactionWasHostile;
    }

    public static void ResetRuntimeState()
    {
        InitiatedTicksByInteractionKey.Clear();
        PruneTracker.Reset();
    }

    public static void ClearRuntimeState()
    {
        ResetRuntimeState();
    }

    // Prefix/Postfix signature binds by name/position on Pawn_HealthTracker.PostApplyDamage.
    private static void Prefix(object __instance, DamageInfo dinfo, ref AttackState __state)
    {
        __state = default;
        try
        {
            if (dinfo.Instigator is not Pawn instigatorPawn || !HKHookUtilSafe.ActorIsHero(instigatorPawn))
            {
                return;
            }

            Pawn victim = GetVictimPawn(__instance);
            __state.victimWasDowned = victim != null && victim.Downed;
            __state.victimWasGuestLike = HKHookUtil.IsGuestLike(victim);
            __state.victimWasHostile = victim != null && victim.HostileTo(instigatorPawn);

            __state.victimFactionWasHostile =
                victim?.Faction != null && instigatorPawn.Faction != null && victim.Faction.HostileTo(instigatorPawn.Faction);

            // Important: A neutral victim can become hostile immediately after the first hit.
            // Mark the interaction as "initiated by the hero" when factions are not hostile, so Postfix
            // doesn't incorrectly treat the exchange as pure self-defense and drop the event.
            if (victim != null && !__state.victimFactionWasHostile)
            {
                RememberInitiated(instigatorPawn, victim);
            }

            HKGoodwillContext.Begin(instigatorPawn);
            __state.goodwillBegun = true;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_AttackNeutral:1",
                "HarmonyPatch_AttackNeutral suppressed an exception.",
                ex);
        }
    }

    private static void Postfix(object __instance, DamageInfo dinfo, AttackState __state)
    {
        // Note: this patch can emit multiple keys (AttackNeutral / HarmGuest / KillDownedNeutral),
        // so we must not early-return on one HookEnabled flag.
        try
        {
            if (__instance == null)
            {
                return;
            }

            if (dinfo.Instigator is not Pawn instigatorPawn || !HKHookUtilSafe.ActorIsHero(instigatorPawn))
            {
                return;
            }

            Pawn victim = GetVictimPawn(__instance);
            if (!ShouldProcessAttack(instigatorPawn, victim))
            {
                return;
            }

            int stage = GetAttackStage(victim);
            int factionId = HKHookUtil.GetFactionIdSafe(victim);

            // Prefer guest harm classification if enabled.
            if (__state.victimWasGuestLike && HKSettingsUtil.HookEnabled("HarmGuest"))
            {
                var guestEvent = KarmaEvent.Create("HarmGuest", instigatorPawn, victim, factionId, stage);
                    HKKarmaProcessor.Process(guestEvent);

                RememberInitiated(instigatorPawn, victim);
                return;
            }

            // Finishing off a downed neutral is a separate deed from generic neutral aggression.
            if (stage >= 2 && __state.victimWasDowned && !__state.victimFactionWasHostile && HKSettingsUtil.HookEnabled("KillDownedNeutral"))
            {
                                    var finishOffEvent = KarmaEvent.Create("KillDownedNeutral", instigatorPawn, victim, factionId, stage);
                    HKKarmaProcessor.Process(finishOffEvent);
                

                RememberInitiated(instigatorPawn, victim);
                return;
            }

            if (!HKSettingsUtil.HookEnabled("AttackNeutral"))
            {
                return;
            }

            if (HKBalanceTuning.LocalRepEvents.SuppressAttackNeutralIfTargetGuilty && HKHookUtil.IsCurrentlyGuilty(victim))
            {
                return;
            }

            var karmaEvent = KarmaEvent.Create("AttackNeutral", instigatorPawn, victim, factionId, stage);
            HKKarmaProcessor.Process(karmaEvent);

            RememberInitiated(instigatorPawn, victim);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_AttackNeutral:2",
                "HarmonyPatch_AttackNeutral suppressed an exception.",
                ex);
        }
    }

    private static void Finalizer(Exception __exception, AttackState __state)
    {
        if (!__state.goodwillBegun)
        {
            return;
        }

        try
        {
            HKGoodwillContext.End();
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_AttackNeutral:3",
                "HarmonyPatch_AttackNeutral suppressed an exception.",
                ex);
        }
    }

    private static bool ShouldProcessAttack(Pawn instigatorPawn, Pawn victim)
    {
        if (victim == null)
        {
            return false;
        }

        if (HKHookUtil.IsAnimal(victim) || HKHookUtil.IsPermanentManhunterOrBerserk(victim))
        {
            return false;
        }

        if (victim.Faction != null && victim.Faction.IsPlayer)
        {
            return false;
        }

        // If the victim is hostile *and* this interaction was not initiated by the hero recently,
        // treat it as self-defense and do not generate karma events.
        bool factionHostile =
            victim.Faction != null && instigatorPawn.Faction != null && victim.Faction.HostileTo(instigatorPawn.Faction);

        if (factionHostile && !WasInitiatedRecently(instigatorPawn, victim))
        {
            if (HKDiagnostics.Enabled)
            {
                string v = null;
                try { v = victim.GetUniqueLoadID(); } catch { /* ignore */ }
                HKDiagnostics.AddOnly("Drop AttackNeutral: self-defense (faction hostile, not initiated)" + (v != null ? (" victim=" + v) : ""));
            }
            return false;
        }

        return true;
    }

    private static int GetAttackStage(Pawn victim)
    {
        if (victim.Dead)
        {
            return 2;
        }

        if (victim.Downed)
        {
            return 1;
        }

        return 0;
    }
}
