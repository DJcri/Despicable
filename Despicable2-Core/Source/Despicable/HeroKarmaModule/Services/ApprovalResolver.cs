using System;
using Despicable.HeroKarma.Patches.HeroKarma;
using RimWorld;
using Verse;

// Guardrail-Reason: Approval resolution stays centralized so karma, standing, and local-reputation routing preserve one decision order.
namespace Despicable.HeroKarma;

/// <summary>
/// Resolves a <see cref="KarmaEvent"/> into a 3-layer outcome:
/// - Cosmic karma delta: universal (drives perk tiers)
/// - Ideology standing delta: relative to the hero's ideoligion (drives standing effects)
///
/// Local reputation deltas are handled separately via tokens.
/// </summary>
public struct HKOutcomeResult
{
    public int karmaDelta;
    public string karmaReason;

    public int standingDelta;
    public string standingReason;

    // Legacy/debug: the raw ideology approval score we inferred (-2..+2-ish).
    public int approvalScore;

    public static HKOutcomeResult Neutral(string reason = null)
    {
        return new HKOutcomeResult
        {
            karmaDelta = 0,
            karmaReason = reason ?? "Neutral",
            standingDelta = 0,
            standingReason = null,
            approvalScore = 0
        };
    }
}

public static partial class ApprovalResolver
{
    /// <summary>
    /// Resolve event into cosmic karma + ideology standing.
    /// </summary>
    public static HKOutcomeResult Resolve(KarmaEvent ev)
    {
        if (ev == null || ev.eventKey.NullOrEmpty())
            return HKOutcomeResult.Neutral("No event key");

        try
        {
            int karmaDelta = ResolveCosmicKarma(ev, out string karmaReason);

            int standingDelta = 0;
            string standingReason = null;
            int approvalScore = 0;

            // Reuse existing toggle: this now represents "Enable Ideology Standing".
            if (IsIdeologyApprovalEnabled())
            {
                standingDelta = ResolveIdeologyStanding(ev, out standingReason, out approvalScore);
            }

            return new HKOutcomeResult
            {
                karmaDelta = karmaDelta,
                karmaReason = karmaReason,
                standingDelta = standingDelta,
                standingReason = standingReason,
                approvalScore = approvalScore
            };
        }
        catch (Exception ex)
        {
            return HKOutcomeResult.Neutral("Resolver error (neutral): " + ex.GetType().Name);
        }
    }

    // --- Cosmic karma (universal) -------------------------------------------------------------

    private static int ResolveCosmicKarma(KarmaEvent ev, out string reason)
    {
        reason = "Unmapped (neutral)";
        string eventKey = HKSettingsUtil.CanonicalizeEventKey(ev.eventKey);
        switch (eventKey)
        {
            case "ExecutePrisoner":
                reason = "Executed prisoner";
                return HKBalanceTuning.GetExecutePrisonerPenalty(HKBalanceTuning.KarmaEvents.ExecutePrisoner, HKHookUtil.IsCurrentlyGuilty(ev.targetPawn));

            case "EnslaveAttempt":
                reason = "Enslave attempt";
                return HKBalanceTuning.KarmaEvents.EnslaveAttempt;

            case HKSettingsUtil.EventSellCaptive:
                reason = "Sold captive";
                return HKBalanceTuning.KarmaEvents.SellCaptive;

            case "FreeSlave":
                reason = "Freed slave";
                return HKBalanceTuning.KarmaEvents.FreeSlave;

            case "OrganHarvest":
                reason = "Organ harvested";
                return HKBalanceTuning.KarmaEvents.OrganHarvest;

            case "CharityGift":
                return ResolveCosmicCharityGift(ev, out reason);

            case "DonateToBeggars":
                return ResolveCosmicDonateToBeggars(ev, out reason);

            case "AttackNeutral":
                return ResolveAttackNeutralCosmic(ev, out reason);

            case "TendOutsider":
                reason = "Helped outsider";
                return HKBalanceTuning.KarmaEvents.TendOutsider;

            case "ReleasePrisoner":
                reason = "Released prisoner";
                return HKBalanceTuning.KarmaEvents.ReleasePrisoner;

            case "ArrestNeutral":
                reason = "Arrested neutral";
                return HKBalanceTuning.KarmaEvents.ArrestNeutral;

            case "RescueOutsider":
                reason = "Rescued outsider";
                return HKBalanceTuning.KarmaEvents.RescueOutsider;

            case "StabilizeOutsider":
                reason = "Stabilized outsider";
                return HKBalanceTuning.KarmaEvents.StabilizeOutsider;

            case "KillDownedNeutral":
                reason = "Killed downed neutral";
                return HKBalanceTuning.GetKillDownedNeutralPenalty(HKBalanceTuning.KarmaEvents.KillDownedNeutral, HKHookUtil.IsCurrentlyGuilty(ev.targetPawn));

            case "HarmGuest":
                return ResolveHarmGuestCosmic(ev, out reason);

            default:
                return 0;
        }
    }

    private static int ResolveCosmicCharityGift(KarmaEvent ev, out string reason)
    {
        int baseDelta = HKBalanceTuning.GetCharityGiftKarmaDelta(ev.amount);

        if (baseDelta == 0)
        {
            reason = "Gift too small (neutral)";
            return 0;
        }

        reason = "Charity gift";
        return baseDelta;
    }

    private static int ResolveCosmicDonateToBeggars(KarmaEvent ev, out string reason)
    {
        int baseDelta = HKBalanceTuning.GetDonateToBeggarsKarmaDelta(ev.amount);

        if (baseDelta == 0)
        {
            reason = "Donation too small (neutral)";
            return 0;
        }

        reason = "Helped beggars";
        return baseDelta;
    }

    private static int ResolveAttackNeutralCosmic(KarmaEvent ev, out string reason)
    {
        int w = HKBalanceTuning.GetAttackSeverityWeight(ev.stage);
        int delta = HKBalanceTuning.KarmaEvents.AttackNeutralBase * w;
        reason = ev.stage >= 2 ? "Killed neutral" : (ev.stage == 1 ? "Downed neutral" : "Hit neutral");
        return delta;
    }

    private static int ResolveHarmGuestCosmic(KarmaEvent ev, out string reason)
    {
        bool targetIsGuilty = HKHookUtil.IsCurrentlyGuilty(ev.targetPawn);

        if (ev.stage >= 2)
        {
            reason = "Killed guest";
            return HKBalanceTuning.GetHarmGuestPenalty(HKBalanceTuning.KarmaEvents.HarmGuestKill, targetIsGuilty);
        }

        if (ev.stage == 1)
        {
            reason = "Downed guest";
            return HKBalanceTuning.GetHarmGuestPenalty(HKBalanceTuning.KarmaEvents.HarmGuestDown, targetIsGuilty);
        }

        reason = "Hit guest";
        return HKBalanceTuning.GetHarmGuestPenalty(HKBalanceTuning.KarmaEvents.HarmGuestHit, targetIsGuilty);
    }

    // --- Ideology standing (relative) ---------------------------------------------------------

    private static int ResolveIdeologyStanding(KarmaEvent ev, out string reason, out int approvalScore)
    {
        reason = null;
        approvalScore = 0;

        string eventKey = HKSettingsUtil.CanonicalizeEventKey(ev.eventKey);
        switch (eventKey)
        {
            case "ExecutePrisoner":
                return ResolveStandingByKeyword(ev, "Execution", HKBalanceTuning.KarmaMagnitudeMedium, out reason, out approvalScore);

            case "EnslaveAttempt":
                return ResolveStandingByKeyword(ev, "Slavery", HKBalanceTuning.KarmaMagnitudeMedium, out reason, out approvalScore);

            case HKSettingsUtil.EventSellCaptive:
                return ResolveStandingByKeyword(ev, "Slavery", HKBalanceTuning.KarmaMagnitudeLarge, out reason, out approvalScore);

            case "OrganHarvest":
                return ResolveStandingByKeyword(ev, "OrganUse", HKBalanceTuning.KarmaMagnitudeLarge, out reason, out approvalScore);

            case "FreeSlave":
                return ResolveStandingFreeSlave(ev, out reason, out approvalScore);

            case "CharityGift":
                return ResolveStandingCharity(ev, giftMode: true, out reason, out approvalScore);

            case "DonateToBeggars":
                return ResolveStandingCharity(ev, giftMode: false, out reason, out approvalScore);

            default:
                // Standing is intentionally narrow in v1 so it doesn't become "karma 2".
                reason = null;
                approvalScore = 0;
                return 0;
        }
    }


    private static void RecordStandingTrace(KarmaEvent ev, string issueKey, PreceptScore score, int delta)
    {
        HKIdeologyEvaluationTrace trace = HKIdeologyEvaluationTrace.GetOrCreate(ev);
        if (trace == null)
            return;

        trace.RecordStanding(issueKey, score.matchedDefName, score.score, delta);
        SetGuiltContextIfRelevant(trace, ev, issueKey);

        if (!score.resolved)
            trace.AddNote(issueKey + " standing unresolved");
        else if (score.matchedDefName.NullOrEmpty())
            trace.AddNote(issueKey + " standing matched no exact precept");
    }

    private static void SetGuiltContextIfRelevant(HKIdeologyEvaluationTrace trace, KarmaEvent ev, string issueKey)
    {
        if (trace == null || ev == null || issueKey != "Execution")
            return;

        try
        {
            trace.WasGuiltyContext = HKHookUtil.IsCurrentlyGuilty(ev.targetPawn);
        }
        catch (Exception ex)
        {
            trace.AddNote("guilt resolver error: " + ex.GetType().Name);
        }
    }

    private static int ResolveStandingByKeyword(KarmaEvent ev, string issueKey, int magnitude, out string reason, out int approvalScore)
    {
        approvalScore = 0;
        reason = issueKey + ": unresolved";

        PreceptScore score = TryGetPreceptScore(ev.actor, issueKey, ev, out string preceptReason);
        if (!score.resolved)
        {
            RecordStandingTrace(ev, issueKey, score, 0);
            return 0;
        }

        approvalScore = score.score;
        reason = issueKey + ": " + preceptReason;

        int delta = score.score * magnitude;
        // Treat "acceptable/indifferent" as neutral standing change.
        RecordStandingTrace(ev, issueKey, score, delta);
        return delta;
    }

    private static int ResolveStandingFreeSlave(KarmaEvent ev, out string reason, out int approvalScore)
    {
        approvalScore = 0;
        reason = "Free slave: unresolved";

        PreceptScore preceptScore = TryGetPreceptScore(ev.actor, "Slavery", ev, out string preceptReason);
        if (!preceptScore.resolved)
        {
            RecordStandingTrace(ev, "Slavery", preceptScore, 0);
            return 0;
        }

        // If slavery is approved, freeing is disapproved, and vice versa.
        int inverted = -preceptScore.score;
        approvalScore = inverted;
        reason = "Free slave: " + preceptReason;

        int delta = inverted * HKBalanceTuning.KarmaMagnitudeMedium;
        RecordStandingTrace(ev, "Slavery", new PreceptScore
        {
            resolved = true,
            score = inverted,
            matchedDefName = preceptScore.matchedDefName,
            matchedLabel = preceptScore.matchedLabel
        }, delta);
        return delta;
    }

    private static int ResolveStandingCharity(KarmaEvent ev, bool giftMode, out string reason, out int approvalScore)
    {
        approvalScore = 0;
        reason = "Charity: unresolved";

        int baseMag = giftMode
            ? HKBalanceTuning.GetCharityGiftKarmaDelta(ev.amount)
            : HKBalanceTuning.GetDonateToBeggarsKarmaDelta(ev.amount);

        if (baseMag == 0)
        {
            reason = giftMode ? "Gift too small (neutral)" : "Donation too small (neutral)";
            HKIdeologyEvaluationTrace trace = HKIdeologyEvaluationTrace.GetOrCreate(ev);
            trace?.RecordStanding("Charity", null, 0, 0);
            trace?.AddNote(reason);
            return 0;
        }

        PreceptScore preceptScore = TryGetPreceptScore(ev.actor, "Charity", ev, out string preceptReason);
        if (!preceptScore.resolved)
        {
            // If we can't infer a doctrine stance, we do not move standing.
            reason = "Charity: unresolved";
            RecordStandingTrace(ev, "Charity", preceptScore, 0);
            return 0;
        }

        approvalScore = preceptScore.score;
        reason = (giftMode ? "Charity gift: " : "Beggars: ") + preceptReason;

        // Scale by gift magnitude; ideology stance chooses direction.
        int delta = HKBalanceTuning.ClampStandingCharityDelta(preceptScore.score * baseMag);
        RecordStandingTrace(ev, "Charity", preceptScore, delta);
        return delta;
    }

    // --- Shared string scoring ---------------------------------------------------------------

    private static bool InferScoreFromDefName(string defName, out int score)
    {
        score = 0;
        if (defName.NullOrEmpty()) return false;

        if (EndsWith(defName, "Required") || EndsWith(defName, "Mandatory") || EndsWith(defName, "Essential"))
        { score = +2; return true; }

        if (EndsWith(defName, "Honorable") || EndsWith(defName, "Venerated") || EndsWith(defName, "Approved"))
        { score = +1; return true; }

        if (EndsWith(defName, "Acceptable") || EndsWith(defName, "Indifferent") || EndsWith(defName, "Okay"))
        { score = 0; return true; }

        if (EndsWith(defName, "Disapproved") || EndsWith(defName, "Dishonorable") || EndsWith(defName, "Disallowed"))
        { score = -1; return true; }

        if (EndsWith(defName, "Horrible") || EndsWith(defName, "Abhorrent") || EndsWith(defName, "Forbidden"))
        { score = -2; return true; }

        return false;
    }

    private static bool InferScoreFromLabel(string label, out int score)
    {
        score = 0;
        if (label.NullOrEmpty()) return false;

        string l = label.ToLowerInvariant();

        if (l.Contains("required") || l.Contains("essential") || l.Contains("must")) { score = +2; return true; }
        if (l.Contains("honorable") || l.Contains("approved") || l.Contains("venerated")) { score = +1; return true; }
        if (l.Contains("acceptable") || l.Contains("indifferent") || l.Contains("okay")) { score = 0; return true; }
        if (l.Contains("disapproved") || l.Contains("dishonorable") || l.Contains("disallowed")) { score = -1; return true; }
        if (l.Contains("horrible") || l.Contains("abhorrent") || l.Contains("forbidden")) { score = -2; return true; }

        return false;
    }

    private static bool EndsWith(string s, string suffix)
    {
        return s != null && suffix != null && s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }
}
