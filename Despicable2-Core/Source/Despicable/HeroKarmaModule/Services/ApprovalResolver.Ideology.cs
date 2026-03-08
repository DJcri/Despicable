using System;
using RimWorld;
using Verse;

namespace Despicable.HeroKarma;
public static partial class ApprovalResolver
{
    private static bool IsIdeologyApprovalEnabled()
    {
        return HKIdeologyCompat.IsStandingEnabled;
    }

    private static Ideo TryGetActorIdeo(Pawn actor)
    {
        if (actor == null)
            return null;

        try
        {
            return actor.Ideo;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("ApprovalResolver.TryGetActorIdeo", "Hero Karma approval resolution failed to read the actor ideology.", ex);
            return null;
        }
    }

    private static Ideo TryGetPlayerPrimaryIdeo()
    {
        try
        {
            Faction ofPlayer = Faction.OfPlayer;
            if (ofPlayer != null && ofPlayer.ideos != null)
                return ofPlayer.ideos.PrimaryIdeo;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "ApprovalResolver.TryGetPlayerPrimaryIdeo",
                "Hero Karma approval resolution failed to read the player primary ideology.",
                ex);
        }

        return null;
    }

    private static string TryGetPreceptDefName(Precept precept)
    {
        try
        {
            return precept != null && precept.def != null ? precept.def.defName : null;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("ApprovalResolver.TryGetPreceptDefName", "Hero Karma approval resolution failed to read a precept def name.", ex);
            return null;
        }
    }

    private struct PreceptScore
    {
        public bool resolved;
        public int score;
        public string matchedDefName;
        public string matchedLabel;
    }

    private static PreceptScore TryGetPreceptScore(Pawn actor, string issueKey, KarmaEvent ev, out string reason)
    {
        reason = "unresolved";
        try
        {
            if (actor == null)
                return new PreceptScore { resolved = false, score = 0 };

            Ideo ideo = TryGetActorIdeo(actor);
            if (ideo == null)
                ideo = TryGetPlayerPrimaryIdeo();

            if (ideo == null)
                return new PreceptScore { resolved = false, score = 0 };

            // Use exact def-name matching for the known vanilla/DLC issue families we support.
            // If a clean exact mapping is unavailable, fail closed instead of fuzzy-matching.
            Precept precept = HKIdeologyExactPrecepts.FindMatchingPrecept(ideo, issueKey);
            if (precept == null)
                return new PreceptScore { resolved = false, score = 0 };

            string defName = TryGetPreceptDefName(precept);
            string label = SafeLabel(precept);

            int s;
            if (HKIdeologyExactPrecepts.TryInferScore(defName, ev, out s))
            {
                reason = label;
                return new PreceptScore { resolved = true, score = s, matchedDefName = defName, matchedLabel = label };
            }

            // Matched a supported precept family but do not have a clean semantic score for this exact def.
            reason = label;
            return new PreceptScore { resolved = true, score = 0, matchedDefName = defName, matchedLabel = label };
        }
        catch (Exception ex)
        {
            reason = "resolver error: " + ex.GetType().Name;
            return new PreceptScore { resolved = false, score = 0 };
        }
    }

    private static string SafeLabel(Precept p)
    {
        try
        {
            return p != null ? p.LabelCap : null;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("ApprovalResolver.SafeLabel", "D2C_CODE_2C24A061".Translate(), ex);
            return null;
        }
    }

}
