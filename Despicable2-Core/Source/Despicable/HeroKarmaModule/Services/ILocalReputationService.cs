using System.Collections.Generic;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Pass 3 safety rail: UI contract for local reputation.
/// Implement later; UI can already render placeholder panels using this.
/// </summary>
public interface ILocalReputationService
{
    RepSnapshot GetPawnRep(string heroPawnId, string targetPawnId);
    RepSnapshot GetFactionRep(string heroPawnId, int factionId);

    /// <summary>
    /// Returns the most relevant reputation entries for display (best/worst/recent).
    /// Keep this cheap; UI will call it often.
    /// </summary>
    List<RepSnapshot> GetTopReputationEntries(string heroPawnId, int limit);

    /// <summary>
    /// Optional but useful for UI: recent local rep changes for the Hero.
    /// </summary>
    List<RepSnapshot> GetRecentChanges(string heroPawnId, int limit);
}

/// <summary>
/// Simple primitive-only snapshot for UI consumption.
/// </summary>
public struct RepSnapshot
{
    public bool valid;
    public int score; // effective score used for gameplay/UI
    public int directScore; // direct reputation with this target
    public int echoScore; // total propagated echo contribution when applicable
    public string echoSourceLabel; // simple combined echo source label for legacy callers
    public int factionEchoScore;
    public string factionEchoSourceLabel;
    public int settlementEchoScore;
    public string settlementEchoSourceLabel;
    public string settlementContextLabel; // known local settlement context even if echo is currently disabled
    public string label;
    public string reasonSummary;
    public int lastChangedTick;
    public string lastEventKey;
    public int lastDelta;
    public int lastBaseDelta;
    public string lastAffectedByLabel;

    // UI helpers
    public bool isFaction;
    public string targetId;   // pawn unique load id or faction id as string
    public string displayName; // resolved name when available

    public static RepSnapshot Invalid => new RepSnapshot
    {
        valid = false,
        score = 0,
        directScore = 0,
        echoScore = 0,
        echoSourceLabel = null,
        factionEchoScore = 0,
        factionEchoSourceLabel = null,
        settlementEchoScore = 0,
        settlementEchoSourceLabel = null,
        settlementContextLabel = null,
        label = null,
        reasonSummary = null,
        lastChangedTick = 0,
        lastEventKey = null,
        lastDelta = 0,
        lastBaseDelta = 0,
        lastAffectedByLabel = null,
        isFaction = false,
        targetId = null,
        displayName = null
    };
}
