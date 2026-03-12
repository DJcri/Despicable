using System.Text;
using Verse;

namespace Despicable.HeroKarma;

public sealed class HKIdeologyEvaluationTrace
{
    public string EventKey;
    public HKRepSemantic Semantic = HKRepSemantic.None;
    public bool? WasGuiltyContext;

    public bool ReputationEvaluated;
    public int ReputationBaseDelta;
    public int ReputationFinalDelta;
    public string ReputationMatchedPreceptDefName;
    public HKRepModifierMode ReputationModifierMode = HKRepModifierMode.None;
    public float ReputationModifierMultiplier = 1f;
    public string ReputationModifierReasonKey;

    public bool StandingEvaluated;
    public string StandingIssueKey;
    public string StandingMatchedPreceptDefName;
    public int StandingScore;
    public int StandingDelta;

    public int SettlementDelta;
    public string Notes;

    public bool HasData
    {
        get
        {
            return Semantic != HKRepSemantic.None
                || ReputationEvaluated
                || StandingEvaluated
                || SettlementDelta != 0
                || !Notes.NullOrEmpty();
        }
    }

    public static HKIdeologyEvaluationTrace GetOrCreate(KarmaEvent ev)
    {
        if (ev == null)
            return null;

        if (ev.ideologyTrace == null)
        {
            ev.ideologyTrace = new HKIdeologyEvaluationTrace
            {
                EventKey = ev.eventKey
            };
        }
        else if (ev.ideologyTrace.EventKey.NullOrEmpty())
        {
            ev.ideologyTrace.EventKey = ev.eventKey;
        }

        return ev.ideologyTrace;
    }

    public void RecordReputation(HKRepSemantic semantic, int baseDelta, int finalDelta, string matchedPreceptDefName, HKRepIdeologyModifier modifier)
    {
        ReputationEvaluated = true;
        Semantic = semantic;
        ReputationBaseDelta = baseDelta;
        ReputationFinalDelta = finalDelta;
        ReputationMatchedPreceptDefName = matchedPreceptDefName;

        bool usedExplicitRule = !matchedPreceptDefName.NullOrEmpty() || !modifier.ReasonKey.NullOrEmpty() || baseDelta != finalDelta;
        ReputationModifierMode = usedExplicitRule ? modifier.Mode : HKRepModifierMode.None;
        ReputationModifierMultiplier = modifier.Multiplier;
        ReputationModifierReasonKey = modifier.ReasonKey;
    }

    public void RecordStanding(string issueKey, string matchedPreceptDefName, int score, int delta)
    {
        StandingEvaluated = true;
        StandingIssueKey = issueKey;
        StandingMatchedPreceptDefName = matchedPreceptDefName;
        StandingScore = score;
        StandingDelta = delta;
    }

    public void AddSettlementDelta(int delta)
    {
        SettlementDelta += delta;
    }

    public void AddNote(string note)
    {
        if (note.NullOrEmpty())
            return;

        if (Notes.NullOrEmpty())
        {
            Notes = note;
            return;
        }

        if (Notes.Contains(note))
            return;

        Notes += "; " + note;
    }

    public string BuildCompactDetailLine()
    {
        if (!HasData)
            return null;

        StringBuilder sb = new StringBuilder();
        sb.Append("Ideology: ");

        if (Semantic != HKRepSemantic.None)
            sb.Append(FormatSemantic(Semantic));
        else if (!StandingIssueKey.NullOrEmpty())
            sb.Append(StandingIssueKey);
        else
            sb.Append("evaluated");

        if (WasGuiltyContext.HasValue)
            sb.Append("; guilt=").Append(WasGuiltyContext.Value ? "guilty" : "innocent");

        if (ReputationEvaluated)
        {
            sb.Append("; rep=");
            sb.Append(ReputationMatchedPreceptDefName.NullOrEmpty() ? "no exact rule" : ReputationMatchedPreceptDefName);
            sb.Append(" ").Append(FormatSigned(ReputationBaseDelta)).Append("→").Append(FormatSigned(ReputationFinalDelta));

            if (ReputationModifierMode != HKRepModifierMode.None)
            {
                sb.Append(" (").Append(ReputationModifierMode);
                if (ReputationModifierMode == HKRepModifierMode.Multiply)
                    sb.Append(" x").Append(ReputationModifierMultiplier.ToString("0.##"));
                if (!ReputationModifierReasonKey.NullOrEmpty())
                    sb.Append(", ").Append(ReputationModifierReasonKey);
                sb.Append(")");
            }
        }

        if (StandingEvaluated)
        {
            sb.Append("; standing=");
            sb.Append(StandingMatchedPreceptDefName.NullOrEmpty() ? "no exact rule" : StandingMatchedPreceptDefName);
            sb.Append(" ").Append(FormatSigned(StandingDelta));
            sb.Append(" (score ").Append(FormatSigned(StandingScore)).Append(")");
        }

        if (SettlementDelta != 0)
            sb.Append("; settlement=").Append(FormatSigned(SettlementDelta));

        if (!Notes.NullOrEmpty())
            sb.Append("; ").Append(Notes);

        return sb.ToString();
    }

    public string BuildDebugLine()
    {
        if (!HasData)
            return null;

        StringBuilder sb = new StringBuilder();
        sb.Append("IdeologyTrace event=").Append(EventKey ?? "none");
        sb.Append(" semantic=").Append(Semantic);
        sb.Append(" guilt=").Append(WasGuiltyContext.HasValue ? (WasGuiltyContext.Value ? "guilty" : "innocent") : "n/a");
        sb.Append(" repBase=").Append(FormatSigned(ReputationBaseDelta));
        sb.Append(" repFinal=").Append(FormatSigned(ReputationFinalDelta));
        sb.Append(" repPrecept=").Append(ReputationMatchedPreceptDefName ?? "none");
        sb.Append(" repMode=").Append(ReputationModifierMode);
        if (ReputationModifierMode == HKRepModifierMode.Multiply)
            sb.Append(" repMult=").Append(ReputationModifierMultiplier.ToString("0.##"));
        sb.Append(" repReason=").Append(ReputationModifierReasonKey ?? "none");
        sb.Append(" standingIssue=").Append(StandingIssueKey ?? "none");
        sb.Append(" standingPrecept=").Append(StandingMatchedPreceptDefName ?? "none");
        sb.Append(" standingScore=").Append(FormatSigned(StandingScore));
        sb.Append(" standingDelta=").Append(FormatSigned(StandingDelta));
        sb.Append(" settlement=").Append(FormatSigned(SettlementDelta));
        if (!Notes.NullOrEmpty())
            sb.Append(" notes=").Append(Notes);
        return sb.ToString();
    }

    private static string FormatSemantic(HKRepSemantic semantic)
    {
        switch (semantic)
        {
            case HKRepSemantic.MercyAid: return "mercy aid";
            case HKRepSemantic.Charity: return "charity";
            case HKRepSemantic.PublicOrder: return "public order";
            case HKRepSemantic.CaptivityMercy: return "captivity mercy";
            case HKRepSemantic.HarshPunishment: return "harsh punishment";
            case HKRepSemantic.CoercionSlavery: return "coercion/slavery";
            case HKRepSemantic.OrganUse: return "organ use";
            default: return semantic.ToString();
        }
    }

    private static string FormatSigned(int value)
    {
        return value > 0 ? "+" + value : value.ToString();
    }
}
