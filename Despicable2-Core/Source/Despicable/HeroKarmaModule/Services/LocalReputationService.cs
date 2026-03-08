using System;
using System.Linq;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Step 4: real implementation backed by HKLocalReputationComponent.
/// </summary>
public class LocalReputationService : ILocalReputationService
{
    public RepSnapshot GetPawnRep(string heroPawnId, string targetPawnId)
    {
        var comp = Current.Game?.GetComponent<HKLocalReputationComponent>();
        if (comp == null) return RepSnapshot.Invalid;
        var r = comp.GetPawnRecord(heroPawnId, targetPawnId);
        if (r == null)
        {
            if (LocalReputationUtility.TryGetPawnScoreBreakdownDetailed(heroPawnId, targetPawnId, out int effectiveScore, out _, out int factionShared, out string factionSource, out int settlementShared, out string settlementSource, out string settlementContext))
            {
                return new RepSnapshot
                {
                    valid = true,
                    score = effectiveScore,
                    directScore = 0,
                    echoScore = factionShared + settlementShared,
                    echoSourceLabel = BuildSharedSourceLabel(factionSource, settlementSource),
                    factionEchoScore = factionShared,
                    factionEchoSourceLabel = factionSource,
                    settlementEchoScore = settlementShared,
                    settlementEchoSourceLabel = settlementSource,
                    settlementContextLabel = settlementContext,
                    label = LocalReputationUtility.LabelForScore(effectiveScore),
                    reasonSummary = null,
                    lastChangedTick = 0,
                    isFaction = false,
                    targetId = targetPawnId,
                    displayName = ResolvePawnName(targetPawnId)
                };
            }

            return RepSnapshot.Invalid;
        }

        return BuildPawnSnapshot(heroPawnId, r, targetId: targetPawnId, displayName: ResolvePawnName(targetPawnId));
    }

    public RepSnapshot GetFactionRep(string heroPawnId, int factionId)
    {
        var comp = Current.Game?.GetComponent<HKLocalReputationComponent>();
        if (comp == null) return RepSnapshot.Invalid;
        var r = comp.GetFactionRecord(heroPawnId, factionId);
        if (r == null) return RepSnapshot.Invalid;

        string tid = HKLocalReputationComponent.FactionKey(factionId);
        return BuildFactionSnapshot(r, targetId: tid, displayName: ResolveFactionName(factionId));
    }

    public List<RepSnapshot> GetTopReputationEntries(string heroPawnId, int limit)
    {
        var comp = Current.Game?.GetComponent<HKLocalReputationComponent>();
        if (comp == null) return new List<RepSnapshot>();

        var list = new List<RepSnapshot>();
        foreach (var kv in comp.AllPawnReps)
        {
            if (!HKLocalReputationComponent.TrySplitKey(kv.Key, out var heroId, out var targetId)) continue;
            if (heroId != heroPawnId) continue;
            var r = kv.Value;
            if (r == null) continue;
            list.Add(BuildPawnSnapshot(heroPawnId, r, targetId, displayName: ResolvePawnName(targetId)));
        }
        foreach (var kv in comp.AllFactionReps)
        {
            if (!HKLocalReputationComponent.TrySplitKey(kv.Key, out var heroId, out var targetId)) continue;
            if (heroId != heroPawnId) continue;
            var r = kv.Value;
            if (r == null) continue;
            int fid = ParseFactionId(targetId);
            list.Add(BuildFactionSnapshot(r, targetId, displayName: ResolveFactionName(fid)));
        }

        TryAddCurrentMapSharedPawnEntries(heroPawnId, list);

        list.Sort((a, b) => Math.Abs(b.score).CompareTo(Math.Abs(a.score)));
        if (list.Count > limit) list.RemoveRange(limit, list.Count - limit);
        return list;
    }

    public List<RepSnapshot> GetRecentChanges(string heroPawnId, int limit)
    {
        var comp = Current.Game?.GetComponent<HKLocalReputationComponent>();
        if (comp == null) return new List<RepSnapshot>();

        var list = new List<RepSnapshot>();
        var recent = comp.RecentChanges;
        for (int i = recent.Count - 1; i >= 0; i--) // newest first
        {
            var e = recent[i];
            if (e == null || e.heroId != heroPawnId) continue;
            string disp = e.isFaction ? ResolveFactionName(ParseFactionId(e.targetId)) : ResolvePawnName(e.targetId);
            list.Add(new RepSnapshot
            {
                valid = true,
                score = 0, // this list is "changes"; UI shows delta + reason
                label = e.delta >= 0 ? "+" + e.delta : e.delta.ToString(),
                reasonSummary = e.reason,
                lastChangedTick = e.tick,
                lastEventKey = e.eventKey,
                lastDelta = e.delta,
                lastBaseDelta = e.baseDelta,
                lastAffectedByLabel = e.affectedByLabel,
                isFaction = e.isFaction,
                targetId = e.targetId,
                displayName = disp
            });
            if (list.Count >= limit) break;
        }
        return list;
    }

    private static RepSnapshot BuildPawnSnapshot(string heroPawnId, RepRecord r, string targetId, string displayName)
    {
        int directScore = Mathf.Clamp(r.score, LocalReputationUtility.RepMin, LocalReputationUtility.RepMax);
        int effectiveScore = directScore;
        int factionEchoScore = 0;
        string factionEchoSourceLabel = null;
        int settlementEchoScore = 0;
        string settlementEchoSourceLabel = null;
        string settlementContextLabel = null;

        LocalReputationUtility.TryGetPawnScoreBreakdownDetailed(
            heroPawnId,
            targetId,
            out effectiveScore,
            out directScore,
            out factionEchoScore,
            out factionEchoSourceLabel,
            out settlementEchoScore,
            out settlementEchoSourceLabel,
            out settlementContextLabel);

        return new RepSnapshot
        {
            valid = true,
            score = effectiveScore,
            directScore = directScore,
            echoScore = factionEchoScore + settlementEchoScore,
            echoSourceLabel = BuildSharedSourceLabel(factionEchoSourceLabel, settlementEchoSourceLabel),
            factionEchoScore = factionEchoScore,
            factionEchoSourceLabel = factionEchoSourceLabel,
            settlementEchoScore = settlementEchoScore,
            settlementEchoSourceLabel = settlementEchoSourceLabel,
            settlementContextLabel = settlementContextLabel,
            label = LocalReputationUtility.LabelForScore(effectiveScore),
            reasonSummary = r.lastReason,
            lastChangedTick = r.lastChangedTick,
            lastEventKey = r.lastEventKey,
            lastDelta = r.lastDelta,
            lastBaseDelta = r.lastBaseDelta,
            lastAffectedByLabel = r.lastAffectedByLabel,
            isFaction = false,
            targetId = targetId,
            displayName = displayName
        };
    }

    private static RepSnapshot BuildFactionSnapshot(RepRecord r, string targetId, string displayName)
    {
        int directScore = Mathf.Clamp(r.score, LocalReputationUtility.RepMin, LocalReputationUtility.RepMax);
        return new RepSnapshot
        {
            valid = true,
            score = directScore,
            directScore = directScore,
            echoScore = 0,
            echoSourceLabel = null,
            factionEchoScore = 0,
            factionEchoSourceLabel = null,
            settlementEchoScore = 0,
            settlementEchoSourceLabel = null,
            settlementContextLabel = null,
            label = LocalReputationUtility.LabelForScore(directScore),
            reasonSummary = r.lastReason,
            lastChangedTick = r.lastChangedTick,
            lastEventKey = r.lastEventKey,
            lastDelta = r.lastDelta,
            lastBaseDelta = r.lastBaseDelta,
            lastAffectedByLabel = r.lastAffectedByLabel,
            isFaction = true,
            targetId = targetId,
            displayName = displayName
        };
    }

    private static void TryAddCurrentMapSharedPawnEntries(string heroPawnId, List<RepSnapshot> list)
    {
        if (heroPawnId.NullOrEmpty() || list == null)
            return;

        try
        {
            Pawn hero = HKResolve.TryResolvePawnById(heroPawnId);
            Map map = hero?.MapHeld ?? hero?.Map;
            if (map?.mapPawns == null)
                return;

            var pawns = map.mapPawns.AllPawnsSpawned;
            if (pawns == null)
                return;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn == hero)
                    continue;
                if (pawn.RaceProps == null || !pawn.RaceProps.Humanlike)
                    continue;

                string targetId = pawn.GetUniqueLoadID();
                if (targetId.NullOrEmpty())
                    continue;
                if (list.Any(x => !x.isFaction && x.targetId == targetId))
                    continue;

                RepSnapshot snap = HKServices.LocalRep.GetPawnRep(heroPawnId, targetId);
                if (!snap.valid)
                    continue;
                if (snap.score == 0 && snap.echoScore == 0)
                    continue;

                list.Add(snap);
            }
        }
        catch
        {
            // Best-effort only. The direct records remain the source of truth.
        }
    }


    private static string ResolvePawnName(string pawnId)
    {
        if (pawnId.NullOrEmpty()) return null;
        Pawn p = HKResolve.TryResolvePawnById(pawnId);
        return p?.LabelShortCap;
    }

    private static string BuildSharedSourceLabel(string factionSourceLabel, string settlementSourceLabel)
    {
        if (!factionSourceLabel.NullOrEmpty() && !settlementSourceLabel.NullOrEmpty())
            return factionSourceLabel + " + " + settlementSourceLabel;
        if (!factionSourceLabel.NullOrEmpty())
            return factionSourceLabel;
        if (!settlementSourceLabel.NullOrEmpty())
            return settlementSourceLabel;
        return null;
    }

    private static string ResolveFactionName(int factionId)
    {
        if (factionId <= 0) return null;
        try
        {
            var mgr = Find.FactionManager;
            if (mgr == null) return null;
            // RimWorld does not expose a stable GetById API in all versions; resolve by loadID.
            var f = mgr.AllFactionsListForReading?.FirstOrDefault(x => x != null && x.loadID == factionId);
            return f?.Name;
        }
        catch
        {
            return null;
        }
    }

    private static int ParseFactionId(string targetId)
    {
        if (targetId.NullOrEmpty()) return -1;
        if (targetId.Length >= 2 && targetId[0] == 'F')
        {
            if (int.TryParse(targetId.Substring(1), out int id)) return id;
        }
        return -1;
    }
}
