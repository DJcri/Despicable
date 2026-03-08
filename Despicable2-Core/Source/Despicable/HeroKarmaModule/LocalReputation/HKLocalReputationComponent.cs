using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Despicable.HeroKarma;
// Guardrail-Reason: Local reputation storage keeps save, prune, and cap policy in one owner so data lifecycle stays centralized.
/// <summary>
/// Step 4: Save-safe local reputation store.
/// Primitive-only keys/values (heroId + targetId -> record).
///
/// This intentionally does NOT touch pawn relations/goodwill directly.
/// It's your mod's internal "hero ↔ pawn/faction" layer.
/// </summary>
public class HKLocalReputationComponent : GameComponent
{
    private const int RecentCap = 60;
    private const int TicksPerDay = 60000;
    private const int PruneMinIntervalTicks = TicksPerDay;
    private const int PruneSoftEntryThreshold = 700;
    private const int StrongScoreKeepAbs = 25;
    private const int WeakScorePruneAbs = 5;

    private const int PawnPerHeroCap = 400;
    private const int SettlementPerHeroCap = 120;
    private const int FactionPerHeroCap = 80;

    private const int NeutralPawnStaleTicks = 7 * TicksPerDay;
    private const int NeutralSettlementStaleTicks = 10 * TicksPerDay;
    private const int NeutralFactionStaleTicks = 14 * TicksPerDay;

    private const int InvalidPawnZeroTicks = 2 * TicksPerDay;
    private const int InvalidPawnWeakTicks = 5 * TicksPerDay;
    private const int InvalidPawnMidTicks = 30 * TicksPerDay;

    private const int InvalidSettlementZeroTicks = 5 * TicksPerDay;
    private const int InvalidSettlementWeakTicks = 15 * TicksPerDay;
    private const int InvalidSettlementMidTicks = 45 * TicksPerDay;

    private const int InvalidFactionZeroTicks = 5 * TicksPerDay;
    private const int InvalidFactionWeakTicks = 20 * TicksPerDay;
    private const int InvalidFactionMidTicks = 60 * TicksPerDay;

    // Flat maps for simple scribing.
    // Key format: "<heroId>::<targetId>"
    private Dictionary<string, RepRecord> pawnRep = new();
    private Dictionary<string, RepRecord> factionRep = new();
    private Dictionary<string, RepRecord> settlementRep = new();

    // Recent changes feed for UI (cap).
    private List<LocalRepChangeEntry> recent = new();

    private readonly HKPruneTickTracker pruneTracker = new();

    private enum RepBucket
    {
        Pawn,
        Settlement,
        Faction
    }

    private sealed class EvictionCandidate
    {
        public string Key;
        public int Priority;
        public int ScoreAbs;
        public int AgeTicks;
    }

    public HKLocalReputationComponent(Game game) { }

    public override void StartedNewGame()
    {
        base.StartedNewGame();
        pruneTracker.Reset();
    }

    public override void LoadedGame()
    {
        base.LoadedGame();
        pruneTracker.Reset();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref pawnRep, "HK_pawnRep", LookMode.Value, LookMode.Deep);
        Scribe_Collections.Look(ref factionRep, "HK_factionRep", LookMode.Value, LookMode.Deep);
        Scribe_Collections.Look(ref settlementRep, "HK_settlementRep", LookMode.Value, LookMode.Deep);
        Scribe_Collections.Look(ref recent, "HK_recent", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (pawnRep == null) pawnRep = new Dictionary<string, RepRecord>();
            if (factionRep == null) factionRep = new Dictionary<string, RepRecord>();
            if (settlementRep == null) settlementRep = new Dictionary<string, RepRecord>();
            if (recent == null) recent = new List<LocalRepChangeEntry>();
            TrimRecentToCap();
            pruneTracker.Reset();
        }
    }

    public override void GameComponentTick()
    {
        base.GameComponentTick();

        int now = Find.TickManager?.TicksGame ?? 0;
        if (now <= 0) return;

        int totalEntries = pawnRep.Count + factionRep.Count + settlementRep.Count;
        if (pruneTracker.ShouldSkipPrune(totalEntries, PruneSoftEntryThreshold, now, PruneMinIntervalTicks))
            return;

        pruneTracker.MarkPruned(now);
        MaybePrune(now);
    }

    public RepRecord GetPawnRecord(string heroId, string pawnId)
    {
        if (heroId.NullOrEmpty() || pawnId.NullOrEmpty()) return null;
        pawnRep.TryGetValue(Key(heroId, pawnId), out var r);
        return r;
    }

    public RepRecord GetFactionRecord(string heroId, int factionId)
    {
        if (heroId.NullOrEmpty()) return null;
        factionRep.TryGetValue(Key(heroId, FactionKey(factionId)), out var r);
        return r;
    }

    public RepRecord GetSettlementRecord(string heroId, string settlementTargetId)
    {
        if (heroId.NullOrEmpty() || settlementTargetId.NullOrEmpty()) return null;
        settlementRep.TryGetValue(Key(heroId, settlementTargetId), out var r);
        return r;
    }

    public IEnumerable<KeyValuePair<string, RepRecord>> AllPawnReps => pawnRep;
    public IEnumerable<KeyValuePair<string, RepRecord>> AllFactionReps => factionRep;
    public IEnumerable<KeyValuePair<string, RepRecord>> AllSettlementReps => settlementRep;
    public IReadOnlyList<LocalRepChangeEntry> RecentChanges => recent;

    public RepRecord GetOrCreatePawnRecord(string heroId, string pawnId)
    {
        string k = Key(heroId, pawnId);
        if (!pawnRep.TryGetValue(k, out var r) || r == null)
        {
            r = new RepRecord();
            pawnRep[k] = r;
        }
        return r;
    }

    public RepRecord GetOrCreateFactionRecord(string heroId, int factionId)
    {
        string k = Key(heroId, FactionKey(factionId));
        if (!factionRep.TryGetValue(k, out var r) || r == null)
        {
            r = new RepRecord();
            factionRep[k] = r;
        }
        return r;
    }

    public RepRecord GetOrCreateSettlementRecord(string heroId, string settlementTargetId)
    {
        string k = Key(heroId, settlementTargetId);
        if (!settlementRep.TryGetValue(k, out var r) || r == null)
        {
            r = new RepRecord();
            settlementRep[k] = r;
        }
        return r;
    }

    public void AddRecent(LocalRepChangeEntry entry)
    {
        if (entry == null) return;
        recent.Add(entry);
        TrimRecentToCap();
    }

    public void NotifyPawnRecordTouched(string heroId, int now)
    {
        MaybePruneBucketAfterWrite(pawnRep, RepBucket.Pawn, heroId, PawnPerHeroCap, now, ShouldPrunePawnRecord);
    }

    public void NotifySettlementRecordTouched(string heroId, int now)
    {
        MaybePruneBucketAfterWrite(settlementRep, RepBucket.Settlement, heroId, SettlementPerHeroCap, now, ShouldPruneSettlementRecord);
    }

    public void NotifyFactionRecordTouched(string heroId, int now)
    {
        MaybePruneBucketAfterWrite(factionRep, RepBucket.Faction, heroId, FactionPerHeroCap, now, ShouldPruneFactionRecord);
    }

    public static string Key(string heroId, string targetId) => $"{heroId}::{targetId}";
    public static string FactionKey(int factionId) => $"F{factionId}";
    public static string SettlementKey(string settlementUniqueId) => settlementUniqueId.NullOrEmpty() ? null : ("S:" + settlementUniqueId);
    public static bool IsSettlementKey(string targetId) => !targetId.NullOrEmpty() && targetId.StartsWith("S:", StringComparison.Ordinal);

    public static bool TrySplitKey(string key, out string heroId, out string targetId)
    {
        heroId = null;
        targetId = null;
        if (key.NullOrEmpty()) return false;
        int idx = key.IndexOf("::", StringComparison.Ordinal);
        if (idx <= 0) return false;
        heroId = key.Substring(0, idx);
        targetId = key.Substring(idx + 2);
        return !heroId.NullOrEmpty() && !targetId.NullOrEmpty();
    }

    private void MaybePrune(int now)
    {
        try
        {
            PruneBrokenRecentEntries();
            PrunePawnRep(now);
            PruneSettlementRep(now);
            PruneFactionRep(now);
            TrimRecentToCap();
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HKLocalReputationComponent.MaybePrune",
                "Hero Karma local reputation pruning failed; keeping the existing reputation data.",
                ex);
        }
    }

    private void PruneBrokenRecentEntries()
    {
        if (recent == null || recent.Count == 0) return;

        for (int i = recent.Count - 1; i >= 0; i--)
        {
            if (recent[i] == null)
                recent.RemoveAt(i);
        }
    }

    private void PrunePawnRep(int now)
    {
        PruneDictionary(pawnRep, now, ShouldPrunePawnRecord);
        EnforcePerHeroCap(pawnRep, RepBucket.Pawn, PawnPerHeroCap, now);
    }

    private void PruneSettlementRep(int now)
    {
        PruneDictionary(settlementRep, now, ShouldPruneSettlementRecord);
        EnforcePerHeroCap(settlementRep, RepBucket.Settlement, SettlementPerHeroCap, now);
    }

    private void PruneFactionRep(int now)
    {
        PruneDictionary(factionRep, now, ShouldPruneFactionRecord);
        EnforcePerHeroCap(factionRep, RepBucket.Faction, FactionPerHeroCap, now);
    }

    private void MaybePruneBucketAfterWrite(Dictionary<string, RepRecord> map, RepBucket bucket, string heroId, int perHeroCap, int now, Func<string, RepRecord, int, bool> shouldRemove)
    {
        if (map == null || map.Count == 0) return;
        if (heroId.NullOrEmpty()) return;
        if (now <= 0) return;

        int heroEntryCount = CountEntriesForHero(map, heroId);
        bool overHeroCap = heroEntryCount > perHeroCap;
        bool overGlobalSoftThreshold = pawnRep.Count + factionRep.Count + settlementRep.Count > PruneSoftEntryThreshold;
        if (!overHeroCap && !overGlobalSoftThreshold)
            return;

        try
        {
            PruneDictionaryForHero(map, heroId, now, shouldRemove);
            if (CountEntriesForHero(map, heroId) > perHeroCap)
                EnforcePerHeroCapForHero(map, bucket, heroId, perHeroCap, now);

            if (overGlobalSoftThreshold)
            {
                PruneBrokenRecentEntries();
                TrimRecentToCap();
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                $"HKLocalReputationComponent.MaybePruneBucketAfterWrite.{bucket}",
                "Hero Karma immediate local reputation pruning failed; deferring cleanup to the scheduled sweep.",
                ex);
        }
    }

    private static void PruneDictionary(Dictionary<string, RepRecord> map, int now, Func<string, RepRecord, int, bool> shouldRemove)
    {
        if (map == null || map.Count == 0) return;

        List<string> removeKeys = null;
        foreach (var kv in map)
        {
            if (!shouldRemove(kv.Key, kv.Value, now))
                continue;

            removeKeys ??= new List<string>();
            removeKeys.Add(kv.Key);
        }

        if (removeKeys == null) return;
        for (int i = 0; i < removeKeys.Count; i++)
            map.Remove(removeKeys[i]);
    }

    private static int CountEntriesForHero(Dictionary<string, RepRecord> map, string heroId)
    {
        if (map == null || map.Count == 0) return 0;
        if (heroId.NullOrEmpty()) return 0;

        int count = 0;
        foreach (var kv in map)
        {
            if (TrySplitKey(kv.Key, out string keyHeroId, out _)
                && keyHeroId == heroId)
            {
                count++;
            }
        }

        return count;
    }

    private static void PruneDictionaryForHero(Dictionary<string, RepRecord> map, string heroId, int now, Func<string, RepRecord, int, bool> shouldRemove)
    {
        if (map == null || map.Count == 0) return;
        if (heroId.NullOrEmpty()) return;

        List<string> removeKeys = null;
        foreach (var kv in map)
        {
            if (!TrySplitKey(kv.Key, out string keyHeroId, out _)
                || keyHeroId != heroId
                || !shouldRemove(kv.Key, kv.Value, now))
            {
                continue;
            }

            removeKeys ??= new List<string>();
            removeKeys.Add(kv.Key);
        }

        if (removeKeys == null) return;
        for (int i = 0; i < removeKeys.Count; i++)
            map.Remove(removeKeys[i]);
    }

    private void EnforcePerHeroCap(Dictionary<string, RepRecord> map, RepBucket bucket, int perHeroCap, int now)
    {
        if (map == null || map.Count == 0) return;

        Dictionary<string, List<EvictionCandidate>> byHero = new();
        foreach (var kv in map)
        {
            string heroId;
            string targetId;
            if (!TrySplitKey(kv.Key, out heroId, out targetId))
            {
                heroId = string.Empty;
            }

            if (!byHero.TryGetValue(heroId, out var list) || list == null)
            {
                list = new List<EvictionCandidate>();
                byHero[heroId] = list;
            }

            list.Add(BuildEvictionCandidate(bucket, kv.Key, kv.Value, now));
        }

        foreach (var pair in byHero)
        {
            List<EvictionCandidate> list = pair.Value;
            if (list == null || list.Count <= perHeroCap)
                continue;

            list.Sort(CompareEvictionCandidates);
            int extra = list.Count - perHeroCap;
            for (int i = 0; i < extra; i++)
                map.Remove(list[i].Key);
        }
    }

    private void EnforcePerHeroCapForHero(Dictionary<string, RepRecord> map, RepBucket bucket, string heroId, int perHeroCap, int now)
    {
        if (map == null || map.Count == 0) return;
        if (heroId.NullOrEmpty()) return;

        List<EvictionCandidate> list = null;
        foreach (var kv in map)
        {
            if (!TrySplitKey(kv.Key, out string keyHeroId, out _)
                || keyHeroId != heroId)
            {
                continue;
            }

            list ??= new List<EvictionCandidate>();
            list.Add(BuildEvictionCandidate(bucket, kv.Key, kv.Value, now));
        }

        if (list == null || list.Count <= perHeroCap)
            return;

        list.Sort(CompareEvictionCandidates);
        int extra = list.Count - perHeroCap;
        for (int i = 0; i < extra; i++)
            map.Remove(list[i].Key);
    }

    private EvictionCandidate BuildEvictionCandidate(RepBucket bucket, string key, RepRecord record, int now)
    {
        int ageTicks = GetRecordAgeTicks(record, now);
        int scoreAbs = record != null ? Math.Abs(record.score) : int.MaxValue;
        int priority = GetEvictionPriority(bucket, key, record, now, ageTicks, scoreAbs);

        return new EvictionCandidate
        {
            Key = key,
            Priority = priority,
            ScoreAbs = scoreAbs,
            AgeTicks = ageTicks
        };
    }

    private int GetEvictionPriority(RepBucket bucket, string key, RepRecord record, int now, int ageTicks, int scoreAbs)
    {
        if (record == null || !TrySplitKey(key, out _, out string targetId))
            return 0;

        if (scoreAbs == 0)
        {
            if (ageTicks >= TicksPerDay)
                return 1;

            return 2;
        }

        bool isValid = bucket switch
        {
            RepBucket.Pawn => IsPawnTargetResolvable(targetId),
            RepBucket.Settlement => IsSettlementTargetResolvable(targetId),
            _ => IsFactionTargetResolvable(targetId)
        };

        if (!isValid && scoreAbs <= WeakScorePruneAbs)
            return 3;
        if (!isValid)
            return 4;
        if (scoreAbs < StrongScoreKeepAbs)
            return 5;
        return 6;
    }

    private static int CompareEvictionCandidates(EvictionCandidate a, EvictionCandidate b)
    {
        int byPriority = a.Priority.CompareTo(b.Priority);
        if (byPriority != 0) return byPriority;

        int byStrength = a.ScoreAbs.CompareTo(b.ScoreAbs);
        if (byStrength != 0) return byStrength;

        return b.AgeTicks.CompareTo(a.AgeTicks);
    }

    private bool ShouldPrunePawnRecord(string key, RepRecord record, int now)
    {
        if (record == null) return true;
        if (!TrySplitKey(key, out _, out string pawnId)) return true;

        if (record.score == 0 && IsOlderThan(record, now, NeutralPawnStaleTicks))
            return true;

        if (IsPawnTargetResolvable(pawnId))
            return false;

        int scoreAbs = Math.Abs(record.score);
        if (record.score == 0 && IsOlderThan(record, now, InvalidPawnZeroTicks))
            return true;
        if (scoreAbs <= WeakScorePruneAbs && IsOlderThan(record, now, InvalidPawnWeakTicks))
            return true;
        if (scoreAbs < StrongScoreKeepAbs && IsOlderThan(record, now, InvalidPawnMidTicks))
            return true;

        return false;
    }

    private bool ShouldPruneSettlementRecord(string key, RepRecord record, int now)
    {
        if (record == null) return true;
        if (!TrySplitKey(key, out _, out string settlementTargetId)) return true;
        if (!IsSettlementKey(settlementTargetId)) return true;

        if (record.score == 0 && IsOlderThan(record, now, NeutralSettlementStaleTicks))
            return true;

        if (IsSettlementTargetResolvable(settlementTargetId))
            return false;

        int scoreAbs = Math.Abs(record.score);
        if (record.score == 0 && IsOlderThan(record, now, InvalidSettlementZeroTicks))
            return true;
        if (scoreAbs <= WeakScorePruneAbs && IsOlderThan(record, now, InvalidSettlementWeakTicks))
            return true;
        if (scoreAbs < StrongScoreKeepAbs && IsOlderThan(record, now, InvalidSettlementMidTicks))
            return true;

        return false;
    }

    private bool ShouldPruneFactionRecord(string key, RepRecord record, int now)
    {
        if (record == null) return true;
        if (!TrySplitKey(key, out _, out string factionTargetId)) return true;
        if (!TryParseFactionTargetId(factionTargetId, out _)) return true;

        if (record.score == 0 && IsOlderThan(record, now, NeutralFactionStaleTicks))
            return true;

        if (IsFactionTargetResolvable(factionTargetId))
            return false;

        int scoreAbs = Math.Abs(record.score);
        if (record.score == 0 && IsOlderThan(record, now, InvalidFactionZeroTicks))
            return true;
        if (scoreAbs <= WeakScorePruneAbs && IsOlderThan(record, now, InvalidFactionWeakTicks))
            return true;
        if (scoreAbs < StrongScoreKeepAbs && IsOlderThan(record, now, InvalidFactionMidTicks))
            return true;

        return false;
    }

    private static bool IsOlderThan(RepRecord record, int now, int thresholdTicks)
    {
        return GetRecordAgeTicks(record, now) >= thresholdTicks;
    }

    private static int GetRecordAgeTicks(RepRecord record, int now)
    {
        if (record == null) return int.MaxValue;
        if (now <= 0) return 0;

        int anchorTick = Math.Max(record.lastChangedTick, record.lastAppliedTick);
        if (anchorTick <= 0) return int.MaxValue;
        if (anchorTick >= now) return 0;
        return now - anchorTick;
    }

    private static bool IsPawnTargetResolvable(string pawnId)
    {
        if (pawnId.NullOrEmpty()) return false;
        return HKResolve.TryResolvePawnById(pawnId) != null;
    }

    private static bool IsSettlementTargetResolvable(string settlementTargetId)
    {
        if (!TryGetSettlementUniqueId(settlementTargetId, out string settlementUniqueId))
            return false;

        var worldObjects = Find.WorldObjects?.AllWorldObjects;
        if (worldObjects == null) return false;

        for (int i = 0; i < worldObjects.Count; i++)
        {
            if (worldObjects[i] is Settlement settlement && settlement.GetUniqueLoadID() == settlementUniqueId)
                return true;
        }

        return false;
    }

    private static bool IsFactionTargetResolvable(string factionTargetId)
    {
        if (!TryParseFactionTargetId(factionTargetId, out int factionId))
            return false;

        Faction faction = HKResolve.TryResolveFactionById(factionId);
        return faction != null;
    }

    private static bool TryGetSettlementUniqueId(string settlementTargetId, out string settlementUniqueId)
    {
        settlementUniqueId = null;
        if (!IsSettlementKey(settlementTargetId)) return false;

        settlementUniqueId = settlementTargetId.Substring(2);
        return !settlementUniqueId.NullOrEmpty();
    }

    private static bool TryParseFactionTargetId(string factionTargetId, out int factionId)
    {
        factionId = 0;
        if (factionTargetId.NullOrEmpty()) return false;
        if (!factionTargetId.StartsWith("F", StringComparison.Ordinal)) return false;
        return int.TryParse(factionTargetId.Substring(1), out factionId) && factionId > 0;
    }

    private void TrimRecentToCap()
    {
        if (recent == null) return;
        if (recent.Count <= RecentCap) return;

        recent.RemoveRange(0, recent.Count - RecentCap);
    }
}
