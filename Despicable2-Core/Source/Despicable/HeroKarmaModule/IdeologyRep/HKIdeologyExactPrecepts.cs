using System;
using Despicable.HeroKarma.Patches.HeroKarma;
using RimWorld;
using Verse;

namespace Despicable.HeroKarma;

/// <summary>
/// Shared exact-precept catalog for the narrow ideology issue families that Hero Karma
/// currently interprets. Keeps Reputation and Ideology Standing aligned on the same
/// exact def-name sets and the same guilt-aware execution scoring rules.
/// </summary>
public static class HKIdeologyExactPrecepts
{
    public static string[] GetSupportedPreceptDefs(string issueKey)
    {
        switch (issueKey)
        {
            case "Execution":
                return new[]
                {
                    "Execution_Abhorrent",
                    "Execution_Horrible",
                    "Execution_HorribleIfInnocent",
                    "Execution_DontCare",
                    "Execution_RespectedIfGuilty",
                    "Execution_Required",
                    "Execution_Classic"
                };

            case "Slavery":
                return new[]
                {
                    "Slavery_Abhorrent",
                    "Slavery_Horrible",
                    "Slavery_Disapproved",
                    "Slavery_Acceptable",
                    "Slavery_Honorable",
                    "Slavery_Classic"
                };

            case "Charity":
                return new[]
                {
                    "Charity_Essential",
                    "Charity_Important",
                    "Charity_Worthwhile"
                };

            case "OrganUse":
            case "Organ":
            case "Organs":
                return new[]
                {
                    "OrganUse_Abhorrent",
                    "OrganUse_HorribleNoSell",
                    "OrganUse_HorribleSellOK",
                    "OrganUse_Acceptable",
                    "OrganUse_Classic"
                };

            default:
                return null;
        }
    }

    public static bool TryFindMatchingMemeDefName(Ideo ideo, out string matchedDefName, params string[] defNames)
    {
        matchedDefName = null;
        if (ideo == null || defNames == null || defNames.Length == 0)
            return false;

        try
        {
            var list = ideo.memes;
            if (list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                string defName = list[i]?.defName;
                if (defName.NullOrEmpty())
                    continue;

                for (int j = 0; j < defNames.Length; j++)
                {
                    if (string.Equals(defName, defNames[j], StringComparison.OrdinalIgnoreCase))
                    {
                        matchedDefName = defName;
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HKIdeologyExactPrecepts.TryFindMatchingMemeDefName",
                "Hero Karma failed while scanning ideology memes for an exact supported def-name match.",
                ex);
        }

        return false;
    }

    public static bool HasAnyMemeDef(Ideo ideo, params string[] defNames)
    {
        return TryFindMatchingMemeDefName(ideo, out _, defNames);
    }

    public static bool TryFindMatchingPreceptDefName(Ideo ideo, out string matchedDefName, params string[] defNames)
    {
        matchedDefName = null;
        if (ideo == null || defNames == null || defNames.Length == 0)
            return false;

        try
        {
            var list = ideo.PreceptsListForReading;
            if (list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                string defName = list[i]?.def?.defName;
                if (defName.NullOrEmpty())
                    continue;

                for (int j = 0; j < defNames.Length; j++)
                {
                    if (string.Equals(defName, defNames[j], StringComparison.OrdinalIgnoreCase))
                    {
                        matchedDefName = defName;
                        return true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HKIdeologyExactPrecepts.TryFindMatchingPreceptDefName",
                "Hero Karma failed while scanning ideology precepts for an exact supported def-name match.",
                ex);
        }

        return false;
    }

    public static bool HasAnyPreceptDef(Ideo ideo, params string[] defNames)
    {
        if (ideo == null || defNames == null || defNames.Length == 0)
            return false;

        try
        {
            var list = ideo.PreceptsListForReading;
            if (list == null)
                return false;

            for (int i = 0; i < list.Count; i++)
            {
                string defName = list[i]?.def?.defName;
                if (defName.NullOrEmpty())
                    continue;

                for (int j = 0; j < defNames.Length; j++)
                {
                    if (string.Equals(defName, defNames[j], StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HKIdeologyExactPrecepts.HasAnyPreceptDef",
                "Hero Karma failed while scanning ideology precepts for exact supported issue defs.",
                ex);
        }

        return false;
    }

    public static Precept FindMatchingPrecept(Ideo ideo, string issueKey)
    {
        if (ideo == null)
            return null;

        try
        {
            string[] supported = GetSupportedPreceptDefs(issueKey);
            if (supported == null || supported.Length == 0)
                return null;

            var list = ideo.PreceptsListForReading;
            if (list == null)
                return null;

            for (int i = 0; i < list.Count; i++)
            {
                Precept precept = list[i];
                string defName = precept?.def?.defName;
                if (defName.NullOrEmpty())
                    continue;

                for (int j = 0; j < supported.Length; j++)
                {
                    if (string.Equals(defName, supported[j], StringComparison.OrdinalIgnoreCase))
                        return precept;
                }
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HKIdeologyExactPrecepts.FindMatchingPrecept",
                "Hero Karma failed while scanning ideology precepts for an exact supported issue match.",
                ex);
        }

        return null;
    }

    public static bool TryInferScore(string defName, KarmaEvent ev, out int score)
    {
        score = 0;
        if (defName.NullOrEmpty())
            return false;

        switch (defName)
        {
            case "Charity_Essential": score = +2; return true;
            case "Charity_Important": score = +1; return true;
            case "Charity_Worthwhile": score = +1; return true;

            case "Slavery_Abhorrent": score = -2; return true;
            case "Slavery_Horrible": score = -2; return true;
            case "Slavery_Disapproved": score = -1; return true;
            case "Slavery_Acceptable": score = 0; return true;
            case "Slavery_Honorable": score = +1; return true;
            case "Slavery_Classic": score = +1; return true;

            case "OrganUse_Abhorrent": score = -2; return true;
            case "OrganUse_HorribleNoSell": score = -2; return true;
            case "OrganUse_HorribleSellOK": score = -2; return true;
            case "OrganUse_Acceptable": score = 0; return true;
            case "OrganUse_Classic": score = -1; return true;

            case "Execution_Abhorrent": score = -2; return true;
            case "Execution_Horrible": score = -2; return true;
            case "Execution_DontCare": score = 0; return true;
            case "Execution_Required": score = +2; return true;
            case "Execution_Classic": score = -1; return true;

            case "Execution_HorribleIfInnocent":
            {
                bool guilty = HKHookUtil.IsCurrentlyGuilty(ev?.targetPawn);
                score = guilty ? 0 : -2;
                return true;
            }

            case "Execution_RespectedIfGuilty":
            {
                bool guilty = HKHookUtil.IsCurrentlyGuilty(ev?.targetPawn);
                score = guilty ? +1 : 0;
                return true;
            }

            default:
                return false;
        }
    }
}
