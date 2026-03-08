using System;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

// Guardrail-Reason: Local reputation utility math stays centralized because callers share one calculation and propagation surface.
namespace Despicable.HeroKarma;
/// <summary>
/// Step 4: central entry point for applying local reputation deltas.
/// Includes anti-farm safeguards (cooldown + daily cap + diminishing returns).
///
/// Callers should pass primitive identifiers (heroId + targetId) and a short reason.
/// </summary>
public static class LocalReputationUtility
{
    private enum RepTargetKind
    {
        Pawn,
        Faction,
        Settlement
    }

    public const int RepMin = HKBalanceTuning.LocalRep.ScoreMin;
    public const int RepMax = HKBalanceTuning.LocalRep.ScoreMax;

    // Keep legacy call sites stable, but tune these values in HKBalanceTuning.LocalRep.
    public const int CooldownTicksSameEvent = HKBalanceTuning.LocalRep.CooldownTicksSameEvent;
    public const float CooldownFactor = HKBalanceTuning.LocalRep.CooldownFactor;
    public const int DailyAbsCapPerTarget = HKBalanceTuning.LocalRep.DailyAbsCapPerTarget;

    /// <summary>
    /// Apply local rep change Hero ↔ Pawn.
    /// Returns true if any delta was applied.
    /// </summary>
    public static bool TryApplyPawnDelta(Pawn hero, Pawn targetPawn, int delta, string eventKey, string reason, int baseDelta = 0, string affectedByLabel = null)
    {
        if (hero == null || targetPawn == null) return false;
        if (delta == 0) return false;
        string heroId = hero.GetUniqueLoadID();
        string pawnId = targetPawn.GetUniqueLoadID();
        bool changed = TryApplyDeltaInternal(heroId, pawnId, RepTargetKind.Pawn, factionId: -1, delta, eventKey, reason, recordRecent: true, out int appliedDelta, baseDelta, affectedByLabel);
        TryApplyPawnSettlementEcho(heroId, targetPawn, appliedDelta, eventKey, reason);
        return changed;
    }

    /// <summary>
    /// Apply local rep change Hero ↔ Faction.
    /// Returns true if any delta was applied.
    /// </summary>
    public static bool TryApplyFactionDelta(Pawn hero, Faction faction, int delta, string eventKey, string reason)
    {
        if (hero == null || faction == null) return false;
        if (delta == 0) return false;
        if (faction.IsPlayer) return false;
        string heroId = hero.GetUniqueLoadID();
        int fid = faction.loadID;
        return TryApplyDeltaInternal(heroId, HKLocalReputationComponent.FactionKey(fid), RepTargetKind.Faction, fid, delta, eventKey, reason, recordRecent: true, out _, baseDelta: 0, affectedByLabel: null);
    }

    public static bool TryApplySettlementDelta(Pawn hero, Settlement settlement, int delta, string eventKey, string reason)
    {
        return TryApplySettlementDelta(hero, settlement, delta, eventKey, reason, recordRecent: true);
    }

    public static bool TryApplySettlementDelta(Pawn hero, Settlement settlement, int delta, string eventKey, string reason, bool recordRecent)
    {
        if (hero == null || settlement == null) return false;
        if (delta == 0) return false;
        return TryApplySettlementDeltaByUniqueId(hero, settlement.GetUniqueLoadID(), delta, eventKey, reason, recordRecent);
    }

    public static bool TryApplySettlementDeltaByUniqueId(Pawn hero, string settlementUniqueId, int delta, string eventKey, string reason, bool recordRecent = false)
    {
        if (hero == null) return false;
        if (settlementUniqueId.NullOrEmpty()) return false;
        if (delta == 0) return false;

        string heroId = hero.GetUniqueLoadID();
        string settlementTargetId = HKLocalReputationComponent.SettlementKey(settlementUniqueId);
        if (settlementTargetId.NullOrEmpty()) return false;
        return TryApplyDeltaInternal(heroId, settlementTargetId, RepTargetKind.Settlement, factionId: -1, delta, eventKey, reason, recordRecent, out _, baseDelta: 0, affectedByLabel: null);
    }

    private static bool TryApplyDeltaInternal(string heroId, string targetId, RepTargetKind targetKind, int factionId, int delta, string eventKey, string reason, bool recordRecent, out int appliedDelta, int baseDelta = 0, string affectedByLabel = null)
    {
        appliedDelta = 0;
        if (heroId.NullOrEmpty() || targetId.NullOrEmpty()) return false;
        if (Current.Game == null) return false;

        var comp = Current.Game.GetComponent<HKLocalReputationComponent>();
        if (comp == null) return false;

        int nowTick = Find.TickManager?.TicksGame ?? 0;
        int day = GenDate.DaysPassed;

        RepRecord record = targetKind switch
        {
            RepTargetKind.Faction => comp.GetOrCreateFactionRecord(heroId, factionId),
            RepTargetKind.Settlement => comp.GetOrCreateSettlementRecord(heroId, targetId),
            _ => comp.GetOrCreatePawnRecord(heroId, targetId)
        };

        if (record == null) return false;

        // Reset daily counters on day change.
        if (record.lastDay != day)
        {
            record.lastDay = day;
            record.dayAbsApplied = 0;
            record.dayCount = 0;
        }

        // Daily cap check.
        int remaining = DailyAbsCapPerTarget - record.dayAbsApplied;
        if (remaining <= 0) return false;

        int applied = delta;

        // Cooldown: repeating the same eventKey against the same target gets heavily reduced.
        if (!eventKey.NullOrEmpty() && record.lastEventKey == eventKey && (nowTick - record.lastAppliedTick) < CooldownTicksSameEvent)
        {
            applied = ScaleTowardsZero(applied, CooldownFactor);
        }

        // Diminishing returns per day per target.
        float dim = 1f / (1f + record.dayCount * 0.75f);
        applied = Mathf.RoundToInt(applied * dim);
        if (applied == 0) applied = Math.Sign(delta); // keep the direction if something would have happened

        // Respect remaining daily cap.
        if (Math.Abs(applied) > remaining)
            applied = Math.Sign(applied) * remaining;

        // Clamp overall score.
        int before = record.score;
        int after = Mathf.Clamp(before + applied, RepMin, RepMax);
        applied = after - before;
        if (applied == 0) return false;

        appliedDelta = applied;

        record.score = after;
        record.lastChangedTick = nowTick;
        record.lastAppliedTick = nowTick;
        record.lastReason = reason;
        record.lastEventKey = eventKey;
        record.lastDelta = applied;
        record.lastBaseDelta = baseDelta;
        record.lastAffectedByLabel = affectedByLabel;
        record.dayAbsApplied += Math.Abs(applied);
        record.dayCount += 1;

        if (recordRecent)
        {
            comp.AddRecent(new LocalRepChangeEntry
            {
                heroId = heroId,
                isFaction = targetKind == RepTargetKind.Faction,
                targetId = targetId,
                delta = applied,
                tick = nowTick,
                reason = reason,
                eventKey = eventKey,
                baseDelta = baseDelta,
                affectedByLabel = affectedByLabel
            });
        }

        switch (targetKind)
        {
            case RepTargetKind.Faction:
                comp.NotifyFactionRecordTouched(heroId, nowTick);
                break;
            case RepTargetKind.Settlement:
                comp.NotifySettlementRecordTouched(heroId, nowTick);
                break;
            default:
                comp.NotifyPawnRecordTouched(heroId, nowTick);
                break;
        }

        return true;
    }

    private static int ScaleTowardsZero(int value, float factor)
    {
        int scaled = Mathf.RoundToInt(value * factor);
        if (scaled == 0) scaled = Math.Sign(value);
        // But never exceed original magnitude.
        if (Math.Abs(scaled) > Math.Abs(value)) return value;
        return scaled;
    }

    private static void TryApplyPawnSettlementEcho(string heroId, Pawn targetPawn, int appliedPawnDelta, string eventKey, string reason)
    {
        if (appliedPawnDelta == 0) return;
        if (Mathf.Approximately(HKBalanceTuning.LocalRep.PawnToSettlementEchoFactor, 0f)) return;
        if (UsesExplicitSettlementDelta(eventKey)) return;
        if (!TryResolveSettlementForPawn(targetPawn, out string settlementTargetId, out _)) return;

        int settlementDelta = GetPawnToSettlementEchoScore(appliedPawnDelta);
        if (settlementDelta == 0) return;

        TryApplyDeltaInternal(heroId, settlementTargetId, RepTargetKind.Settlement, factionId: -1, settlementDelta, eventKey, reason, recordRecent: false, out _, baseDelta: 0, affectedByLabel: null);
    }

    private static bool UsesExplicitSettlementDelta(string eventKey)
    {
        switch (HKSettingsUtil.CanonicalizeEventKey(eventKey))
        {
            case "TendOutsider":
            case "RescueOutsider":
            case "StabilizeOutsider":
            case "KillDownedNeutral":
            case "AttackNeutral":
            case "HarmGuest":
            case "ArrestNeutral":
            case "ExecutePrisoner":
            case "EnslaveAttempt":
            case "ReleasePrisoner":
            case "FreeSlave":
            case HKSettingsUtil.EventSellCaptive:
            case "CharityGift":
            case "DonateToBeggars":
                return true;
            default:
                return false;
        }
    }

    public static int GetFactionEchoToPawnScore(int factionScore)
    {
        if (factionScore == 0) return 0;

        int echoed = Mathf.RoundToInt(factionScore * HKBalanceTuning.LocalRep.FactionEchoToPawnFactor);
        if (echoed == 0)
            echoed = Math.Sign(factionScore);

        int cap = Math.Abs(HKBalanceTuning.LocalRep.FactionEchoToPawnMaxAbs);
        return Mathf.Clamp(echoed, -cap, cap);
    }

    public static int GetSettlementEchoToPawnScore(int settlementScore)
    {
        if (settlementScore == 0) return 0;
        if (Mathf.Approximately(HKBalanceTuning.LocalRep.SettlementEchoToPawnFactor, 0f)) return 0;

        int echoed = Mathf.RoundToInt(settlementScore * HKBalanceTuning.LocalRep.SettlementEchoToPawnFactor);
        if (echoed == 0 && HKBalanceTuning.LocalRep.SettlementEchoForceMinimumOne)
            echoed = Math.Sign(settlementScore);
        if (echoed == 0)
            return 0;

        int cap = Math.Abs(HKBalanceTuning.LocalRep.SettlementEchoToPawnMaxAbs);
        return Mathf.Clamp(echoed, -cap, cap);
    }

    public static int GetPawnToSettlementEchoScore(int pawnScore)
    {
        if (pawnScore == 0) return 0;
        if (Mathf.Approximately(HKBalanceTuning.LocalRep.PawnToSettlementEchoFactor, 0f)) return 0;

        int echoed = Mathf.RoundToInt(pawnScore * HKBalanceTuning.LocalRep.PawnToSettlementEchoFactor);
        if (echoed == 0 && HKBalanceTuning.LocalRep.PawnToSettlementEchoForceMinimumOne)
            echoed = Math.Sign(pawnScore);
        if (echoed == 0)
            return 0;

        int cap = Math.Abs(HKBalanceTuning.LocalRep.PawnToSettlementEchoMaxAbs);
        return Mathf.Clamp(echoed, -cap, cap);
    }

    public static bool TryResolveSettlementForPawn(Pawn targetPawn, out string settlementTargetId, out string settlementLabel)
    {
        settlementTargetId = null;
        settlementLabel = null;
        if (targetPawn == null) return false;

        try
        {
            if (!global::Despicable.PawnContext.TryResolveSettlement(targetPawn, out string settlementUniqueId, out settlementLabel))
                return false;

            settlementTargetId = HKLocalReputationComponent.SettlementKey(settlementUniqueId);
            return !settlementTargetId.NullOrEmpty();
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "LocalReputationUtility.ResolveSettlementForPawn",
                "Hero Karma local reputation failed to resolve settlement context for a pawn. Local word-of-mouth will be skipped for that pawn.",
                ex);
            settlementTargetId = null;
            settlementLabel = null;
            return false;
        }
    }

    public static bool TryGetPawnScoreBreakdown(string heroId, string targetPawnId, out int effectiveScore, out int directScore, out int sharedScore, out string sharedSourceLabel)
    {
        bool valid = TryGetPawnScoreBreakdownDetailed(heroId, targetPawnId, out effectiveScore, out directScore, out int factionSharedScore, out string factionSharedSourceLabel, out int settlementSharedScore, out string settlementSharedSourceLabel, out _);
        sharedScore = factionSharedScore + settlementSharedScore;

        if (!factionSharedSourceLabel.NullOrEmpty() && !settlementSharedSourceLabel.NullOrEmpty())
            sharedSourceLabel = factionSharedSourceLabel + " + " + settlementSharedSourceLabel;
        else
            sharedSourceLabel = !factionSharedSourceLabel.NullOrEmpty() ? factionSharedSourceLabel : settlementSharedSourceLabel;

        return valid;
    }

    public static bool TryGetPawnScoreBreakdownDetailed(string heroId, string targetPawnId, out int effectiveScore, out int directScore, out int factionSharedScore, out string factionSharedSourceLabel, out int settlementSharedScore, out string settlementSharedSourceLabel, out string settlementContextLabel)
    {
        effectiveScore = 0;
        directScore = 0;
        factionSharedScore = 0;
        factionSharedSourceLabel = null;
        settlementSharedScore = 0;
        settlementSharedSourceLabel = null;
        settlementContextLabel = null;

        if (heroId.NullOrEmpty() || targetPawnId.NullOrEmpty()) return false;

        bool hasDirect = false;
        var comp = Current.Game?.GetComponent<HKLocalReputationComponent>();
        if (comp != null)
        {
            var direct = comp.GetPawnRecord(heroId, targetPawnId);
            if (direct != null)
            {
                directScore = Mathf.Clamp(direct.score, RepMin, RepMax);
                hasDirect = true;
            }
        }

        Pawn pawn = HKResolve.TryResolvePawnById(targetPawnId);
        Faction faction = pawn?.Faction;
        if (faction != null && !faction.IsPlayer && TryGetFactionScore(heroId, faction.loadID, out int factionScore))
        {
            factionSharedScore = GetFactionEchoToPawnScore(factionScore);
            factionSharedSourceLabel = faction.Name;
        }

        if (TryResolveSettlementForPawn(pawn, out string settlementTargetId, out string settlementLabel))
        {
            settlementContextLabel = settlementLabel;
            if (TryGetSettlementScore(heroId, settlementTargetId, out int settlementScore))
            {
                settlementSharedScore = GetSettlementEchoToPawnScore(settlementScore);
                settlementSharedSourceLabel = settlementLabel;
            }
        }

        effectiveScore = Mathf.Clamp(directScore + factionSharedScore + settlementSharedScore, RepMin, RepMax);
        return hasDirect || factionSharedScore != 0 || settlementSharedScore != 0 || !settlementContextLabel.NullOrEmpty();
    }

    /// <summary>
    /// Convert a rep score [-100..100] into an influence index in [-1..1]
    /// using a soft power curve.
    /// </summary>
    public static float InfluenceIndexFromScore(int score)
    {
        float s = Mathf.Clamp(score, RepMin, RepMax) / 100f;
        if (Mathf.Abs(s) < 0.0001f) return 0f;
        return Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.75f);
    }

    public static bool TryGetPawnScore(string heroId, string targetPawnId, out int score)
    {
        return TryGetPawnScoreBreakdown(heroId, targetPawnId, out score, out _, out _, out _);
    }

    public static bool TryGetFactionScore(string heroId, int factionId, out int score)
    {
        score = 0;
        if (heroId.NullOrEmpty() || factionId <= 0) return false;
        var comp = Current.Game?.GetComponent<HKLocalReputationComponent>();
        if (comp == null) return false;
        var r = comp.GetFactionRecord(heroId, factionId);
        if (r == null) return false;
        score = Mathf.Clamp(r.score, RepMin, RepMax);
        return true;
    }

    public static bool TryGetSettlementScore(string heroId, string settlementTargetId, out int score)
    {
        score = 0;
        if (heroId.NullOrEmpty() || settlementTargetId.NullOrEmpty()) return false;
        var comp = Current.Game?.GetComponent<HKLocalReputationComponent>();
        if (comp == null) return false;
        var r = comp.GetSettlementRecord(heroId, settlementTargetId);
        if (r == null) return false;
        score = Mathf.Clamp(r.score, RepMin, RepMax);
        return true;
    }

    public static bool TryGetPawnInfluenceIndex(string heroId, string targetPawnId, out float index)
    {
        index = 0f;
        if (!TryGetPawnScore(heroId, targetPawnId, out int score)) return false;
        index = InfluenceIndexFromScore(score);
        return true;
    }

    public static bool TryGetFactionInfluenceIndex(string heroId, int factionId, out float index)
    {
        index = 0f;
        if (!TryGetFactionScore(heroId, factionId, out int score)) return false;
        index = InfluenceIndexFromScore(score);
        return true;
    }

    public static string LabelForScore(int score)
    {
        if (score >= 70) return "Renowned";
        if (score >= 35) return "Well Regarded";
        if (score >= 10) return "Favorable";
        if (score > -10) return "Unremarked";
        if (score > -35) return "Strained";
        if (score > -70) return "Ill Regarded";
        return "Reviled";
    }
}
