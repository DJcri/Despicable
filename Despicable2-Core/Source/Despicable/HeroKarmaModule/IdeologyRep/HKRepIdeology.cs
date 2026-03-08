using System;
using RimWorld;
using UnityEngine;
using Verse;
using Despicable.HeroKarma.Patches.HeroKarma;

// Guardrail-Reason: Ideology reputation semantics and resolution helpers stay together while standing behavior remains one feature seam.
namespace Despicable.HeroKarma;

public enum HKRepSemantic
{
    None = 0,
    MercyAid,
    Charity,
    PublicOrder,
    CaptivityMercy,
    HarshPunishment,
    CoercionSlavery,
    OrganUse
}

public enum HKRepModifierMode
{
    None = 0,
    Multiply,
    Nullify,
    Invert
}

public readonly struct HKRepIdeologyModifier
{
    public readonly HKRepModifierMode Mode;
    public readonly float Multiplier;
    public readonly string ReasonKey;

    public HKRepIdeologyModifier(HKRepModifierMode mode, float multiplier, string reasonKey = null)
    {
        Mode = mode;
        Multiplier = multiplier;
        ReasonKey = reasonKey;
    }

    public static HKRepIdeologyModifier Identity => new(HKRepModifierMode.Multiply, 1f, null);
}

public readonly struct HKIdeologyStanceProfile
{
    public readonly bool Humanitarian;
    public readonly bool Charitable;
    public readonly bool Authoritarian;
    public readonly bool Punitive;
    public readonly bool AntiExecution;
    public readonly bool AntiSlavery;
    public readonly bool ProSlavery;

    public HKIdeologyStanceProfile(
        bool humanitarian,
        bool charitable,
        bool authoritarian,
        bool punitive,
        bool antiExecution,
        bool antiSlavery,
        bool proSlavery)
    {
        Humanitarian = humanitarian;
        Charitable = charitable;
        Authoritarian = authoritarian;
        Punitive = punitive;
        AntiExecution = antiExecution;
        AntiSlavery = antiSlavery;
        ProSlavery = proSlavery;
    }
}

public static class HKRepSemanticResolver
{
    public static HKRepSemantic Classify(KarmaEvent ev)
    {
        if (ev == null)
            return HKRepSemantic.None;

        if (string.Equals(ev.eventKey, "HarmGuest", StringComparison.Ordinal))
        {
            try
            {
                return HKHookUtil.IsCurrentlyGuilty(ev.targetPawn)
                    ? HKRepSemantic.HarshPunishment
                    : HKRepSemantic.None;
            }
            catch
            {
                return HKRepSemantic.None;
            }
        }

        return Classify(ev.eventKey);
    }

    public static HKRepSemantic Classify(string eventKey)
    {
        eventKey = HKSettingsUtil.CanonicalizeEventKey(eventKey);

        switch (eventKey)
        {
            case "TendOutsider":
            case "RescueOutsider":
            case "StabilizeOutsider":
                return HKRepSemantic.MercyAid;

            case "CharityGift":
            case "DonateToBeggars":
                return HKRepSemantic.Charity;

            case "ArrestNeutral":
                return HKRepSemantic.PublicOrder;

            case "ReleasePrisoner":
            case "FreeSlave":
                return HKRepSemantic.CaptivityMercy;

            case "ExecutePrisoner":
            case "KillDownedNeutral":
                return HKRepSemantic.HarshPunishment;

            case "EnslaveAttempt":
            case HKSettingsUtil.EventSellCaptive:
                return HKRepSemantic.CoercionSlavery;

            case "OrganHarvest":
                return HKRepSemantic.OrganUse;

            default:
                return HKRepSemantic.None;
        }
    }
}

public static class HKIdeologyStanceResolver
{
    public static HKIdeologyStanceProfile Resolve(Pawn target)
    {
        Ideo ideo = TryGetIdeo(target);
        if (ideo == null)
            return default;

        bool humanitarian = HKIdeologyExactPrecepts.HasAnyPreceptDef(ideo,
            "Charity_Essential",
            "Charity_Important");

        bool charitable = HKIdeologyExactPrecepts.HasAnyPreceptDef(ideo,
            "Charity_Essential",
            "Charity_Important",
            "Charity_Worthwhile");

        bool authoritarian = HKIdeologyExactPrecepts.HasAnyMemeDef(ideo,
            "Loyalist");

        bool punitive = HKIdeologyExactPrecepts.HasAnyPreceptDef(ideo,
            "Execution_RespectedIfGuilty",
            "Execution_Required")
            || HKIdeologyExactPrecepts.HasAnyMemeDef(ideo,
                "Guilty",
                "PainIsVirtue");

        bool antiExecution = HKIdeologyExactPrecepts.HasAnyPreceptDef(ideo,
            "Execution_Abhorrent",
            "Execution_Horrible",
            "Execution_HorribleIfInnocent",
            "Execution_Classic");

        bool antiSlavery = HKIdeologyExactPrecepts.HasAnyPreceptDef(ideo,
            "Slavery_Abhorrent",
            "Slavery_Horrible",
            "Slavery_Disapproved");

        bool proSlavery = HKIdeologyExactPrecepts.HasAnyPreceptDef(ideo,
            "Slavery_Acceptable",
            "Slavery_Honorable",
            "Slavery_Classic");

        return new HKIdeologyStanceProfile(humanitarian, charitable, authoritarian, punitive, antiExecution, antiSlavery, proSlavery);
    }

    private static Ideo TryGetIdeo(Pawn target)
    {
        try { return target?.Ideo; }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HKIdeologyStanceResolver.TryGetIdeo", "Hero Karma failed to read a target ideology while resolving Reputation precept modifiers.", ex);
            return null;
        }
    }
}

public static class HKRepIdeologyModifierResolver
{
    public static HKRepIdeologyModifier GetModifier(Pawn target, HKRepSemantic semantic, int baseDelta, string eventKey, out string matchedRuleDefName)
    {
        matchedRuleDefName = null;

        if (!HKIdeologyCompat.IsAvailable)
            return HKRepIdeologyModifier.Identity;

        if (!HKBalanceTuning.ReputationIdeology.EnableTargetPreceptModifiers)
            return HKRepIdeologyModifier.Identity;

        if (target == null || semantic == HKRepSemantic.None || baseDelta == 0)
            return HKRepIdeologyModifier.Identity;

        Ideo ideo = TryGetIdeo(target);
        if (ideo == null)
            return HKRepIdeologyModifier.Identity;

        bool positive = baseDelta > 0;
        bool negative = baseDelta < 0;
        bool guiltyContext = IsGuiltyContext(target);
        bool guiltyGuestDiscipline = guiltyContext && string.Equals(eventKey, "HarmGuest", StringComparison.Ordinal);

        switch (semantic)
        {
            case HKRepSemantic.MercyAid:
                if (positive && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "Charity_Essential",
                    "Charity_Important"))
                {
                    return MultPositive(HKBalanceTuning.ReputationIdeology.Humanitarian_MercyAid, "Humanitarian_MercyAid");
                }
                break;

            case HKRepSemantic.Charity:
                if (positive && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "Charity_Essential",
                    "Charity_Important",
                    "Charity_Worthwhile"))
                {
                    return MultPositive(HKBalanceTuning.ReputationIdeology.Charitable_Charity, "Charitable_Charity");
                }

                if (positive && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "Charity_Essential",
                    "Charity_Important"))
                {
                    return MultPositive(HKBalanceTuning.ReputationIdeology.Humanitarian_Charity, "Humanitarian_Charity");
                }
                break;

            case HKRepSemantic.PublicOrder:
                if (negative && TryMatchMeme(ideo, out matchedRuleDefName,
                    "Loyalist"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.Authoritarian_PublicOrder_Negative, "Loyalist_PublicOrder");
                }
                break;

            case HKRepSemantic.CaptivityMercy:
                if (positive && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "Slavery_Abhorrent",
                    "Slavery_Horrible",
                    "Slavery_Disapproved"))
                {
                    return MultPositive(HKBalanceTuning.ReputationIdeology.AntiSlavery_CaptivityMercy, "AntiSlavery_CaptivityMercy");
                }

                if (positive && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "Slavery_Acceptable",
                    "Slavery_Honorable",
                    "Slavery_Classic"))
                {
                    if (HKBalanceTuning.ReputationIdeology.ProSlavery_CaptivityMercyCanNullify)
                        return new HKRepIdeologyModifier(HKRepModifierMode.Nullify, 0f, "ProSlavery_CaptivityMercy_Nullify");

                    return MultPositive(HKBalanceTuning.ReputationIdeology.ProSlavery_CaptivityMercy, "ProSlavery_CaptivityMercy");
                }
                break;

            case HKRepSemantic.HarshPunishment:
                if (negative && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "Execution_Abhorrent",
                    "Execution_Horrible",
                    "Execution_HorribleIfInnocent",
                    "Execution_Classic"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.Humanitarian_HarshPunishment,
                        matchedRuleDefName == "Execution_HorribleIfInnocent" ? "AntiExecution_HarshPunishment" : "Humanitarian_HarshPunishment");
                }

                if (negative && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "Charity_Essential",
                    "Charity_Important"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.Humanitarian_HarshPunishment, "Humanitarian_HarshPunishment");
                }

                if (negative && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "Execution_RespectedIfGuilty",
                    "Execution_Required"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.Punitive_HarshPunishment_Negative, "Punitive_HarshPunishment");
                }

                if (negative && guiltyGuestDiscipline && TryMatchMeme(ideo, out matchedRuleDefName,
                    "Guilty"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.Guilty_HarmGuest_Guilty_Negative, "Guilty_HarmGuest_Guilty");
                }

                if (negative && guiltyGuestDiscipline && TryMatchMeme(ideo, out matchedRuleDefName,
                    "PainIsVirtue"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.PainIsVirtue_HarmGuest_Guilty_Negative, "PainIsVirtue_HarmGuest_Guilty");
                }

                if (negative && guiltyGuestDiscipline && TryMatchMeme(ideo, out matchedRuleDefName,
                    "Loyalist"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.Authoritarian_HarmGuest_Guilty_Negative, "Loyalist_HarmGuest_Guilty");
                }

                if (negative && guiltyContext && TryMatchMeme(ideo, out matchedRuleDefName,
                    "Guilty"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.Punitive_HarshPunishment_Negative, "Guilty_HarshPunishment_Guilty");
                }

                if (negative && TryMatchMeme(ideo, out matchedRuleDefName,
                    "PainIsVirtue"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.PainIsVirtue_HarshPunishment_Negative, "PainIsVirtue_HarshPunishment");
                }

                if (negative && TryMatchMeme(ideo, out matchedRuleDefName,
                    "Loyalist"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.Authoritarian_HarshPunishment_Negative, "Loyalist_HarshPunishment");
                }
                break;

            case HKRepSemantic.CoercionSlavery:
                if (negative && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "Slavery_Abhorrent",
                    "Slavery_Horrible",
                    "Slavery_Disapproved"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.AntiSlavery_CoercionSlavery_Negative, "AntiSlavery_CoercionSlavery");
                }

                if (negative && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "Slavery_Acceptable",
                    "Slavery_Honorable",
                    "Slavery_Classic"))
                {
                    if (HKBalanceTuning.ReputationIdeology.ProSlavery_CoercionSlaveryCanNullify)
                        return new HKRepIdeologyModifier(HKRepModifierMode.Nullify, 0f, "ProSlavery_CoercionSlavery_Nullify");

                    return MultNegative(HKBalanceTuning.ReputationIdeology.ProSlavery_CoercionSlavery_Negative, "ProSlavery_CoercionSlavery");
                }

                if (negative && TryMatchMeme(ideo, out matchedRuleDefName,
                    "Loyalist"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.Authoritarian_CoercionSlavery_Negative, "Loyalist_CoercionSlavery");
                }
                break;

            case HKRepSemantic.OrganUse:
                if (negative && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "OrganUse_Abhorrent",
                    "OrganUse_HorribleNoSell",
                    "OrganUse_HorribleSellOK"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.AntiOrganUse_OrganHarvest_Negative, "AntiOrganUse_OrganHarvest");
                }

                if (negative && TryMatchPrecept(ideo, out matchedRuleDefName,
                    "OrganUse_Acceptable",
                    "OrganUse_Classic"))
                {
                    return MultNegative(HKBalanceTuning.ReputationIdeology.ProOrganUse_OrganHarvest_Negative, "ProOrganUse_OrganHarvest");
                }
                break;
        }

        matchedRuleDefName = null;
        return HKRepIdeologyModifier.Identity;
    }

    private static bool TryMatchPrecept(Ideo ideo, out string matchedRuleDefName, params string[] defNames)
    {
        return HKIdeologyExactPrecepts.TryFindMatchingPreceptDefName(ideo, out matchedRuleDefName, defNames);
    }

    private static bool TryMatchMeme(Ideo ideo, out string matchedRuleDefName, params string[] defNames)
    {
        if (HKIdeologyExactPrecepts.TryFindMatchingMemeDefName(ideo, out string matchedMemeDefName, defNames))
        {
            matchedRuleDefName = "meme:" + matchedMemeDefName;
            return true;
        }

        matchedRuleDefName = null;
        return false;
    }

    private static bool IsGuiltyContext(Pawn target)
    {
        try { return target != null && target.guilt != null && target.guilt.IsGuilty; }
        catch { return false; }
    }

    private static Ideo TryGetIdeo(Pawn target)
    {
        try { return target?.Ideo; }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HKRepIdeologyModifierResolver.TryGetIdeo", "Hero Karma failed to read a target ideology while resolving Reputation precept modifiers.", ex);
            return null;
        }
    }

    private static HKRepIdeologyModifier MultPositive(float raw, string reasonKey)
    {
        float mult = Mathf.Clamp(raw, 0f, HKBalanceTuning.ReputationIdeology.MaxPositiveMultiplier);
        return new HKRepIdeologyModifier(HKRepModifierMode.Multiply, mult, reasonKey);
    }

    private static HKRepIdeologyModifier MultNegative(float raw, string reasonKey)
    {
        float mult = Mathf.Clamp(raw, 0f, HKBalanceTuning.ReputationIdeology.MaxNegativeMultiplier);
        return new HKRepIdeologyModifier(HKRepModifierMode.Multiply, mult, reasonKey);
    }
}

public static class HKRepIdeologyMath
{
    public static int ApplyAndRound(int baseDelta, HKRepIdeologyModifier modifier)
    {
        if (baseDelta == 0)
            return 0;

        switch (modifier.Mode)
        {
            case HKRepModifierMode.Nullify:
                return 0;

            case HKRepModifierMode.Invert:
                return -baseDelta;

            case HKRepModifierMode.Multiply:
            default:
                return Mathf.RoundToInt(baseDelta * modifier.Multiplier);
        }
    }
}
