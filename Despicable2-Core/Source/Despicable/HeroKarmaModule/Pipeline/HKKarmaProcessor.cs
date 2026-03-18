using System;
using System.Collections.Generic;
using Verse;

namespace Despicable.HeroKarma;
public static partial class HKKarmaProcessor
{
    private const int CooldownVeryShort = 60;   // 1s
    private const int CooldownShort = 180;      // 3s
    private const int CooldownMed = 600;        // 10s
    private const int CooldownLong = 2000;      // ~33s
    private const int WindowShort = 2500;       // ~1 day (RimWorld day is 60000 ticks, so this is a short window)
    private const int WindowDay = 60000;

    public static void Process(KarmaEvent ev)
    {
        if (ev == null || ev.actor == null || ev.eventKey.NullOrEmpty())
            return;

        var gc = Current.Game != null ? Current.Game.GetComponent<GameComponent_HeroKarma>() : null;
        if (gc == null) return;

        if (!IsHeroActor(gc, ev.actor))
        {
            if (HKDiagnostics.Enabled)
                HKDiagnostics.AddOnly("Drop " + ev.eventKey + ": actor not hero");
            return;
        }

        string canonicalEventKey = HKSettingsUtil.CanonicalizeEventKey(ev.eventKey);
        int cooldown = GetCooldownTicks(canonicalEventKey);
        int window = GetWindowTicks(canonicalEventKey);
        int maxInWindow = GetMaxPerWindow(canonicalEventKey);

        string dropReason;
        if (!HKEventDebouncer.ShouldProcess(ev.eventKey, ev.actorPawnId, ev.targetPawnId, ev.targetFactionId,
                ev.stage, cooldown, window, maxInWindow, out dropReason))
        {
            if (HKDiagnostics.Enabled)
            {
                string tgt = !ev.targetPawnId.NullOrEmpty() ? (" pawn=" + ev.targetPawnId) : (ev.targetFactionId > 0 ? (" faction=" + ev.targetFactionId) : "");
                HKDiagnostics.AddOnly("Debounced " + ev.eventKey + " (" + (dropReason ?? "drop") + ") stage=" + ev.stage + tgt);
            }
            return;
        }

        var display = HKServices.EventCatalog.Get(ev.eventKey);
        var outcome = ApprovalResolver.Resolve(ev);

        var tokens = BuildTokens(ev, canonicalEventKey);

        string label = display.label;
        string detail = BuildDetail(ev, canonicalEventKey);
        string karmaReason = outcome.karmaReason;
        string standingReason = outcome.standingReason;

        gc.ApplyOutcome(ev.actor, outcome.karmaDelta, outcome.standingDelta, ev.eventKey, label, detail, karmaReason, standingReason, tokens, ev.targetPawnId, ev.targetFactionId);

        if (HKDiagnostics.Enabled)
        {
            // Keep this lightweight: ring buffer by default; will echo to log only when the debug setting is enabled.
            HKDiagnostics.AddOnly("Applied " + ev.eventKey + " karma=" + outcome.karmaDelta + " standing=" + outcome.standingDelta + " detail='" + detail + "'");
            string ideologyTrace = ev.ideologyTrace?.BuildDebugLine();
            if (!ideologyTrace.NullOrEmpty())
                HKDiagnostics.AddOnly(ideologyTrace);
        }
    }

    private static bool IsHeroActor(GameComponent_HeroKarma gc, Pawn actor)
    {
        if (gc == null || actor == null) return false;
        var heroId = gc.HeroPawnId;
        if (heroId.NullOrEmpty()) return false;

        string actorId;
        try
        {
            actorId = actor.GetUniqueLoadID();
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HKKarmaProcessor.IsHeroActor", "Hero Karma failed to resolve the actor load ID.", ex);
            return false;
        }

        return actorId == heroId;
    }

    private static int GetCooldownTicks(string canonicalEventKey)
    {
        switch (canonicalEventKey)
        {
            // Combat spam can be intense, but escalation should pass (handled by debouncer stage logic).
            case "AttackNeutral": return CooldownVeryShort;
            case "HarmGuest": return 300;
            case "HarmColonyAnimal": return 300;
            case "KillDownedNeutral": return 300;

            // Medical actions can tick repeatedly; rate-limit per target.
            case "TendOutsider": return 2500;
            case "StabilizeOutsider": return 5000;
            case "RescueOutsider": return 2500;

            // Social / custody.
            case "ArrestNeutral": return 2500;
            case "FreeSlave": return 2500;
            case HKSettingsUtil.EventSellCaptive: return 2500;

            // Charity can happen in bursts (multiple stacks in one action).
            case "DonateToBeggars": return 120;
            case "CharityGift": return CooldownMed;

            // Surgery actions are rare but keep a long cooldown just in case a recipe fires multiple times.
            case "OrganHarvest": return CooldownLong;

            default: return CooldownMed;
        }
    }

    private static int GetWindowTicks(string canonicalEventKey)
    {
        switch (canonicalEventKey)
        {
            case "CharityGift": return WindowDay;

            case "AttackNeutral": return 2500;
            case "HarmGuest": return 2500;
            case "HarmColonyAnimal": return 2500;
            case "KillDownedNeutral": return 2500;

            case "TendOutsider": return 10000;
            case "StabilizeOutsider": return 15000;
            case "RescueOutsider": return 10000;

            case "ArrestNeutral": return 10000;
            case "FreeSlave": return 10000;
            case HKSettingsUtil.EventSellCaptive: return 10000;

            case "DonateToBeggars": return 1200;

            default: return 0;
        }
    }

    private static int GetMaxPerWindow(string canonicalEventKey)
    {
        switch (canonicalEventKey)
        {
            case "CharityGift": return 4;      // per faction/target due to keying

            case "AttackNeutral": return 10;   // per target; escalation still passes
            case "HarmGuest": return 3;        // per target
            case "HarmColonyAnimal": return 1; // per target; hit -> down escalation still passes
            case "KillDownedNeutral": return 1;

            case "TendOutsider": return 1;     // per target
            case "StabilizeOutsider": return 1;
            case "RescueOutsider": return 1;

            case "ArrestNeutral": return 1;
            case "FreeSlave": return 1;

            case HKSettingsUtil.EventSellCaptive: return 8;     // per pawn target; mostly defensive
            case "DonateToBeggars": return 3;

            default: return 0;
        }
    }
}
