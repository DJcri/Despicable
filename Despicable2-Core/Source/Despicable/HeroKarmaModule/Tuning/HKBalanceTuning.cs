using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

// Guardrail-Reason: Hero Karma balance values and coordinated override application stay together so tuning changes do not drift across multiple owners.
namespace Despicable.HeroKarma;

/// <summary>
/// Internal-only single source of truth for Hero Karma balance.
///
/// Change values here to rebalance:
/// - global karma / standing thresholds
/// - karma and local-reputation gains/losses
/// - local rep gameplay coefficients
/// - perk passive stat offsets
/// - perk-specific bonus behavior numbers
///
/// This file is intentionally not player-facing. Existing runtime code reads from this hub,
/// and the perk HediffDefs are rewritten after defs load so the XML no longer acts as the
/// balance source of truth.
/// </summary>
public static class HKBalanceTuning
{
    public const int KarmaMin = -6000;
    public const int KarmaMax = 6000;

    public const int TierParagonMin = 4500;
    public const int TierTrustedMin = 2000;
    public const int TierNotoriousMax = -2000;
    public const int TierDreadedMax = -4500;

    public const int StandingExemplaryMin = 70;
    public const int StandingApprovedMin = 35;
    public const int StandingRespectedMin = 10;
    public const int StandingQuestionedMax = -10;
    public const int StandingSuspectMax = -35;
    public const int StandingHereticalMax = -70;

    public const float StandingCertaintySwing = 0.15f;
    public const float StandingCertaintyClampMin = 0.85f;
    public const float StandingCertaintyClampMax = 1.15f;
    public const int StandingOpinionMaxDelta = 8;
    public const int StandingCharityClamp = 12;

    public const int KarmaMagnitudeSmall = 2;
    public const int KarmaMagnitudeMedium = 5;
    public const int KarmaMagnitudeLarge = 10;

    public static class KarmaEvents
    {
        public const int ExecutePrisoner = -KarmaMagnitudeMedium;
        public const int EnslaveAttempt = -KarmaMagnitudeMedium;
        public const int SellCaptive = -KarmaMagnitudeLarge;
        public const int SellPrisoner = SellCaptive; // legacy alias
        public const int FreeSlave = +KarmaMagnitudeMedium;
        public const int OrganHarvest = -KarmaMagnitudeLarge;
        public const int TendOutsider = +KarmaMagnitudeSmall;
        public const int ReleasePrisoner = +KarmaMagnitudeMedium;
        public const int ArrestNeutral = -KarmaMagnitudeMedium;
        public const int AttackNeutralBase = -KarmaMagnitudeMedium;
        public const int RescueOutsider = 3;
        public const int StabilizeOutsider = +KarmaMagnitudeMedium;
        public const int KillDownedNeutral = -18;
        public const int HarmGuestHit = -7;
        public const int HarmGuestDown = -12;
        public const int HarmGuestKill = -18;
    }

    public static class LocalRepEvents
    {
        public const bool SuppressArrestNeutralIfTargetGuilty = true;
        public const bool SuppressAttackNeutralIfTargetGuilty = true;

        public const float GuiltyTargetHarmGuestMultiplier = 0.50f;
        public const float GuiltyTargetKillDownedNeutralMultiplier = 0.60f;
        public const float GuiltyTargetExecutePrisonerMultiplier = 0.70f;

        public const int TendOutsiderPawn = +2;
        public const int TendOutsiderFaction = +1;
        public const int TendOutsiderSettlement = +1;

        public const int ReleasePrisonerPawn = +4;
        public const int ReleasePrisonerFaction = +3;
        public const int ReleasePrisonerSettlement = +2;

        public const int ExecutePrisonerPawn = -6;
        public const int ExecutePrisonerFaction = -5;
        public const int ExecutePrisonerSettlement = -4;

        public const int EnslaveAttemptPawn = -3;
        public const int EnslaveAttemptFaction = -2;
        public const int EnslaveAttemptSettlement = -2;

        public const int OrganHarvestPawn = -10;
        public const int OrganHarvestFaction = -8;

        public const int ArrestNeutralPawn = -4;
        public const int ArrestNeutralFaction = -3;
        public const int ArrestNeutralSettlement = -2;

        public const int RescueOutsiderPawn = +3;
        public const int RescueOutsiderFaction = +2;
        public const int RescueOutsiderSettlement = +2;

        public const int StabilizeOutsiderPawn = +4;
        public const int StabilizeOutsiderFaction = +3;
        public const int StabilizeOutsiderSettlement = +2;

        public const int KillDownedNeutralPawn = -9;
        public const int KillDownedNeutralFaction = -10;
        public const int KillDownedNeutralSettlement = -3;

        public const int FreeSlavePawn = +6;
        public const int FreeSlaveFaction = +1;
        public const int FreeSlaveSettlement = +3;

        public const int SellCaptivePawn = -8;
        public const int SellPrisonerPawn = SellCaptivePawn; // legacy alias
        public const int SellCaptiveFaction = -4;
        public const int SellPrisonerFaction = SellCaptiveFaction; // legacy alias
        public const int SellCaptiveSettlement = -3;
        public const int SellPrisonerSettlement = SellCaptiveSettlement; // legacy alias
    }

    public static class ReputationIdeology
    {
        public const bool EnableTargetPreceptModifiers = true;
        public const bool AllowInversion = false;

        public const float MaxPositiveMultiplier = 1.50f;
        public const float MaxNegativeMultiplier = 1.50f;

        public const float Humanitarian_MercyAid = 1.25f;
        public const float Humanitarian_Charity = 1.20f;
        public const float Humanitarian_HarshPunishment = 1.25f;
        public const float Humanitarian_CaptivityMercy = 1.20f;

        public const float Charitable_Charity = 1.35f;

        public const float Authoritarian_PublicOrder_Negative = 0.50f;
        public const float Authoritarian_HarshPunishment_Negative = 0.75f;
        public const float Authoritarian_HarmGuest_Guilty_Negative = 0.85f;
        public const float Authoritarian_CoercionSlavery_Negative = 0.85f;

        public const float Punitive_HarshPunishment_Negative = 0.60f;
        public const float Guilty_HarmGuest_Guilty_Negative = 0.80f;
        public const float PainIsVirtue_HarshPunishment_Negative = 0.85f;
        public const float PainIsVirtue_HarmGuest_Guilty_Negative = 0.90f;

        public const float AntiOrganUse_OrganHarvest_Negative = 1.35f;
        public const float ProOrganUse_OrganHarvest_Negative = 0.75f;

        public const float AntiSlavery_CaptivityMercy = 1.35f;
        public const float AntiSlavery_CoercionSlavery_Negative = 1.50f;

        public const float ProSlavery_CaptivityMercy = 0.50f;
        public const float ProSlavery_CoercionSlavery_Negative = 0.50f;

        public const bool ProSlavery_CaptivityMercyCanNullify = true;
        public const bool ProSlavery_CoercionSlaveryCanNullify = false;
    }

    public static class PerkBehavior
    {
        public const float GoodwillTailwindPositiveBonus = 0.25f;
        public const float GoodwillTailwindNegativeLossMultiplier = 0.85f;
        public const float GoodwillFrictionPositiveMultiplier = 0.85f;
        public const float GoodwillFrictionNegativeLossBonus = 0.25f;

        public const float IntimidatingPresenceArrestSalvage = 0.08f;
        public const float TerrorEffectArrestSalvage = 0.15f;

        public const float CommunityBufferMoodRecovery = 0.015f;
        public const float SilverTongueRecruitResistanceOffset = -0.50f;
        public const float SilverTongueConvertCertaintyOffset = -0.02f;

        public const int MercyMagnetRescueBonusGoodwill = 2;
        public const int MercyMagnetTendBonusGoodwill = 1;
        public const int MercyMagnetEmergencyTendBonusGoodwill = 2;
        public const int MercyMagnetReleaseBonusGoodwill = 3;

        public const int MercyMagnetTendCooldownTicks = 60000;
        public const int MercyMagnetRescueCooldownTicks = 60000;
        public const int MercyMagnetReleaseCooldownTicks = 90000;
    }

    public static class LocalRep
    {
        public const int ScoreMin = -100;
        public const int ScoreMax = 100;

        public const int CooldownTicksSameEvent = 6000;
        public const float CooldownFactor = 0.15f;
        public const int DailyAbsCapPerTarget = 12;

        public const float FactionEchoToPawnFactor = 0.35f;
        public const int FactionEchoToPawnMaxAbs = 12;

        // Local word-of-mouth should be lighter and slower to surface than faction echo.
        // The settlement -> pawn lane is now enabled for live testing, but it does not force a
        // minimum +/-1 contribution from tiny settlement scores. Small local signals should need
        // to accumulate before they start moving every pawn in town.
        public const float SettlementEchoToPawnFactor = 0.15f;
        public const int SettlementEchoToPawnMaxAbs = 5;
        public const bool SettlementEchoForceMinimumOne = false;

        // Keep passive pawn -> settlement accumulation dormant for now. We already author the
        // most important public-order and civic events explicitly into settlement memory. This
        // keeps the local layer legible during the current tuning checkpoint.
        public const float PawnToSettlementEchoFactor = 0.00f;
        public const int PawnToSettlementEchoMaxAbs = 4;
        public const bool PawnToSettlementEchoForceMinimumOne = false;

        public const float PrisonerSilverTongueMult = 1.25f;

        public const float RecruitCoeff = -0.20f;
        public const float RecruitClampMin = -0.20f;
        public const float RecruitClampMax = +0.15f;

        public const float ConvertCoeff = -0.006f;
        public const float ConvertClampMin = -0.006f;
        public const float ConvertClampMax = +0.004f;

        public const float ArrestTrustMax = 0.15f;
        public const float ArrestFearSynergyMax = 0.08f;
        public const float ArrestChanceCap = 0.25f;

        public const float GoodwillStrength = 0.15f;
        public const float GoodwillClampMin = 0.90f;
        public const float GoodwillClampMax = 1.15f;

        public const float TradeCoeff = 0.02f;
        public const float TradeClamp = 0.02f;
    }

    public static class PerkStats
    {
        public const float MercyMagnetSocialImpact = 0.05f;
        public const float MercyMagnetInjuryHealingFactor = 0.03f;
        public const float MercyMagnetTradePriceImprovement = 0.01f;

        public const float GoodwillTailwindNegotiationAbility = 0.06f;
        public const float GoodwillTailwindSocialImpact = 0.03f;
        public const float GoodwillTailwindTradePriceImprovement = 0.02f;

        public const float SilverTongueNegotiationAbility = 0.10f;
        public const float SilverTongueSocialImpact = 0.06f;

        public const float CommunityBufferMentalBreakThreshold = -0.02f;
        public const float CommunityBufferSocialImpact = 0.02f;

        public const float IntimidatingPresenceSocialImpact = -0.03f;
        public const float IntimidatingPresenceArrestSuccessChance = 0.06f;
        public const float IntimidatingPresenceMentalBreakThreshold = -0.01f;
        public const float IntimidatingPresenceMeleeHitChance = 0.03f;

        public const float GoodwillFrictionNegotiationAbility = -0.06f;
        public const float GoodwillFrictionTradePriceImprovement = -0.02f;
        public const float GoodwillFrictionSocialImpact = -0.02f;

        public const float TerrorEffectMentalBreakThreshold = -0.01f;
        public const float TerrorEffectArrestSuccessChance = 0.10f;
        public const float TerrorEffectMeleeHitChance = 0.03f;
        public const float TerrorEffectMeleeDodgeChance = 0.02f;
        public const float TerrorEffectSocialImpact = 0.03f;

        public const float ReputationTaxTradePriceImprovement = -0.05f;
        public const float ReputationTaxNegotiationAbility = -0.03f;
    }

    // Guardrail-Allow-Static: One-time perk-def override gate owned by HKBalanceTuning; shared across startup so balance overrides apply only once per load.
    private static bool _perkDefOverridesApplied;

    public static int GetAttackSeverityWeight(int stage)
    {
        if (stage >= 2) return 3;
        if (stage == 1) return 2;
        return 1;
    }

    public static int GetCharityGiftKarmaDelta(int amount)
    {
        amount = Math.Max(0, amount);
        int delta = 0;
        if (amount >= 50) delta = 1;
        if (amount >= 200) delta = 2;
        if (amount >= 500) delta = 4;
        if (amount >= 1000) delta = 6;
        if (amount >= 2000) delta = 8;
        return delta;
    }

    public static int GetDonateToBeggarsKarmaDelta(int amount)
    {
        amount = Math.Max(0, amount);
        int delta = 0;
        if (amount >= 50) delta = 2;
        if (amount >= 200) delta = 3;
        if (amount >= 500) delta = 5;
        if (amount >= 1000) delta = 7;
        if (amount >= 2000) delta = 9;
        return delta;
    }

    public static int GetCharityGiftLocalRepFactionDelta(int amount)
    {
        amount = Math.Max(0, amount);
        int delta = 0;
        if (amount >= 50) delta = 1;
        if (amount >= 200) delta = 2;
        if (amount >= 500) delta = 3;
        if (amount >= 1000) delta = 4;
        if (amount >= 2000) delta = 5;
        return delta;
    }

    public static int GetDonateToBeggarsLocalRepFactionDelta(int amount)
    {
        amount = Math.Max(0, amount);
        int delta = 0;
        if (amount >= 50) delta = 2;
        if (amount >= 200) delta = 3;
        if (amount >= 500) delta = 4;
        if (amount >= 1000) delta = 5;
        if (amount >= 2000) delta = 6;
        return delta;
    }

    public static int GetCharityGiftLocalRepSettlementDelta(int amount)
    {
        amount = Math.Max(0, amount);
        int delta = 0;
        if (amount >= 50) delta = 1;
        if (amount >= 500) delta = 2;
        if (amount >= 2000) delta = 3;
        return delta;
    }

    public static int GetDonateToBeggarsLocalRepSettlementDelta(int amount)
    {
        amount = Math.Max(0, amount);
        int delta = 0;
        if (amount >= 50) delta = 1;
        if (amount >= 200) delta = 2;
        if (amount >= 1000) delta = 3;
        if (amount >= 2000) delta = 4;
        return delta;
    }

    public static int ApplyPenaltyMultiplier(int delta, float multiplier)
    {
        if (delta == 0) return 0;

        int scaled = Mathf.RoundToInt(delta * multiplier);
        if (scaled == 0)
            scaled = Math.Sign(delta);

        if (Math.Abs(scaled) > Math.Abs(delta))
            return delta;

        return scaled;
    }

    public static int GetExecutePrisonerPenalty(int delta, bool targetIsGuilty)
    {
        return targetIsGuilty ? ApplyPenaltyMultiplier(delta, LocalRepEvents.GuiltyTargetExecutePrisonerMultiplier) : delta;
    }

    public static int GetKillDownedNeutralPenalty(int delta, bool targetIsGuilty)
    {
        return targetIsGuilty ? ApplyPenaltyMultiplier(delta, LocalRepEvents.GuiltyTargetKillDownedNeutralMultiplier) : delta;
    }

    public static int GetHarmGuestPenalty(int delta, bool targetIsGuilty)
    {
        return targetIsGuilty ? ApplyPenaltyMultiplier(delta, LocalRepEvents.GuiltyTargetHarmGuestMultiplier) : delta;
    }

    public static int GetAttackNeutralLocalRepSettlementDelta(int stage)
    {
        int w = GetAttackSeverityWeight(stage);
        return -w;
    }

    public static int GetHarmGuestLocalRepSettlementDelta(int stage)
    {
        int w = GetAttackSeverityWeight(stage);
        return -(w + 1);
    }

    public static int ClampStandingCharityDelta(int delta)
    {
        return Mathf.Clamp(delta, -StandingCharityClamp, StandingCharityClamp);
    }

    public static string GetTierRangeLabel(HKTier tier)
    {
        switch (tier)
        {
            case HKTier.Paragon:
                return $"+{TierParagonMin} .. +{KarmaMax}";
            case HKTier.Trusted:
                return $"+{TierTrustedMin} .. +{TierParagonMin - 1}";
            case HKTier.Neutral:
                return $"{TierNotoriousMax + 1} .. +{TierTrustedMin - 1}";
            case HKTier.Notorious:
                return $"{TierDreadedMax + 1} .. {TierNotoriousMax}";
            case HKTier.Dreaded:
                return $"{KarmaMin} .. {TierDreadedMax}";
            default:
                return string.Empty;
        }
    }

    public static string DescribeMercyMagnetTooltip()
    {
        return "Passive: "
            + FormatStatOffset("Social Impact", PerkStats.MercyMagnetSocialImpact) + ", "
            + FormatStatOffset("Injury Healing Factor", PerkStats.MercyMagnetInjuryHealingFactor) + ", "
            + FormatStatOffset("Trade Price Improvement", PerkStats.MercyMagnetTradePriceImprovement)
            + ". Helping outsiders grants bonus goodwill.";
    }

    public static string DescribeGoodwillTailwindTooltip()
    {
        int pos = Mathf.RoundToInt(PerkBehavior.GoodwillTailwindPositiveBonus * 100f);
        int neg = Mathf.RoundToInt((1f - PerkBehavior.GoodwillTailwindNegativeLossMultiplier) * 100f);
        return "Passive: "
            + FormatStatOffset("Negotiation Ability", PerkStats.GoodwillTailwindNegotiationAbility) + ", "
            + FormatStatOffset("Social Impact", PerkStats.GoodwillTailwindSocialImpact) + ", "
            + FormatStatOffset("Trade Price Improvement", PerkStats.GoodwillTailwindTradePriceImprovement)
            + $". Hero-caused goodwill gains +{pos}%, losses -{neg}%.";
    }

    public static string DescribeSilverTongueTooltip()
    {
        return "Passive: "
            + FormatStatOffset("Negotiation Ability", PerkStats.SilverTongueNegotiationAbility) + ", "
            + FormatStatOffset("Social Impact", PerkStats.SilverTongueSocialImpact)
            + ". Recruit attempts reduce Resistance by an extra " + FormatAbsNumber(PerkBehavior.SilverTongueRecruitResistanceOffset, 2)
            + ", convert attempts reduce Certainty by " + FormatAbsPercent(PerkBehavior.SilverTongueConvertCertaintyOffset) + ".";
    }

    public static string DescribeCommunityBufferTooltip()
    {
        return "Passive: "
            + FormatStatOffset("Mental Break Threshold", PerkStats.CommunityBufferMentalBreakThreshold) + ", "
            + FormatStatOffset("Social Impact", PerkStats.CommunityBufferSocialImpact)
            + ". Nearby allies recover " + FormatAbsPercent(PerkBehavior.CommunityBufferMoodRecovery, 1) + " mood after insults or slights.";
    }

    public static string DescribeIntimidatingPresenceTooltip()
    {
        return "Passive: "
            + FormatStatOffset("Social Impact", PerkStats.IntimidatingPresenceSocialImpact) + ", "
            + FormatStatOffset("Arrest Success Chance", PerkStats.IntimidatingPresenceArrestSuccessChance) + ", "
            + FormatStatOffset("Mental Break Threshold", PerkStats.IntimidatingPresenceMentalBreakThreshold) + ", "
            + FormatStatOffset("Melee Hit Chance", PerkStats.IntimidatingPresenceMeleeHitChance)
            + ". Failed arrests have an " + FormatAbsPercent(PerkBehavior.IntimidatingPresenceArrestSalvage) + " chance to turn into compliance.";
    }

    public static string DescribeGoodwillFrictionTooltip()
    {
        int neg = Mathf.RoundToInt(PerkBehavior.GoodwillFrictionNegativeLossBonus * 100f);
        int pos = Mathf.RoundToInt((1f - PerkBehavior.GoodwillFrictionPositiveMultiplier) * 100f);
        return "Passive: "
            + FormatStatOffset("Negotiation Ability", PerkStats.GoodwillFrictionNegotiationAbility) + ", "
            + FormatStatOffset("Trade Price Improvement", PerkStats.GoodwillFrictionTradePriceImprovement) + ", "
            + FormatStatOffset("Social Impact", PerkStats.GoodwillFrictionSocialImpact)
            + $". Hero-caused goodwill gains -{pos}%, losses +{neg}%.";
    }

    public static string DescribeTerrorEffectTooltip()
    {
        return "Passive: "
            + FormatStatOffset("Mental Break Threshold", PerkStats.TerrorEffectMentalBreakThreshold) + ", "
            + FormatStatOffset("Arrest Success Chance", PerkStats.TerrorEffectArrestSuccessChance) + ", "
            + FormatStatOffset("Melee Hit Chance", PerkStats.TerrorEffectMeleeHitChance) + ", "
            + FormatStatOffset("Melee Dodge Chance", PerkStats.TerrorEffectMeleeDodgeChance) + ", "
            + FormatStatOffset("Social Impact", PerkStats.TerrorEffectSocialImpact)
            + ". Failed arrests have a " + FormatAbsPercent(PerkBehavior.TerrorEffectArrestSalvage) + " chance to turn into compliance.";
    }

    public static string DescribeReputationTaxTooltip()
    {
        return "Passive: "
            + FormatStatOffset("Trade Price Improvement", PerkStats.ReputationTaxTradePriceImprovement) + ", "
            + FormatStatOffset("Negotiation Ability", PerkStats.ReputationTaxNegotiationAbility) + ".";
    }

    public static void ApplyPerkDefOverrides()
    {
        if (_perkDefOverridesApplied)
            return;

        _perkDefOverridesApplied = true;

        try
        {
            ApplyPerkOffsets("HK_Hediff_MercyMagnet",
                new StatOffset("SocialImpact", PerkStats.MercyMagnetSocialImpact),
                new StatOffset("InjuryHealingFactor", PerkStats.MercyMagnetInjuryHealingFactor),
                new StatOffset("TradePriceImprovement", PerkStats.MercyMagnetTradePriceImprovement));

            ApplyPerkOffsets("HK_Hediff_GoodwillTailwind",
                new StatOffset("NegotiationAbility", PerkStats.GoodwillTailwindNegotiationAbility),
                new StatOffset("SocialImpact", PerkStats.GoodwillTailwindSocialImpact),
                new StatOffset("TradePriceImprovement", PerkStats.GoodwillTailwindTradePriceImprovement));

            ApplyPerkOffsets("HK_Hediff_SilverTongue",
                new StatOffset("NegotiationAbility", PerkStats.SilverTongueNegotiationAbility),
                new StatOffset("SocialImpact", PerkStats.SilverTongueSocialImpact));

            ApplyPerkOffsets("HK_Hediff_CommunityBuffer",
                new StatOffset("MentalBreakThreshold", PerkStats.CommunityBufferMentalBreakThreshold),
                new StatOffset("SocialImpact", PerkStats.CommunityBufferSocialImpact));

            ApplyPerkOffsets("HK_Hediff_IntimidatingPresence",
                new StatOffset("SocialImpact", PerkStats.IntimidatingPresenceSocialImpact),
                new StatOffset("ArrestSuccessChance", PerkStats.IntimidatingPresenceArrestSuccessChance),
                new StatOffset("MentalBreakThreshold", PerkStats.IntimidatingPresenceMentalBreakThreshold),
                new StatOffset("MeleeHitChance", PerkStats.IntimidatingPresenceMeleeHitChance));

            ApplyPerkOffsets("HK_Hediff_GoodwillFriction",
                new StatOffset("NegotiationAbility", PerkStats.GoodwillFrictionNegotiationAbility),
                new StatOffset("TradePriceImprovement", PerkStats.GoodwillFrictionTradePriceImprovement),
                new StatOffset("SocialImpact", PerkStats.GoodwillFrictionSocialImpact));

            ApplyPerkOffsets("HK_Hediff_TerrorEffect",
                new StatOffset("MentalBreakThreshold", PerkStats.TerrorEffectMentalBreakThreshold),
                new StatOffset("ArrestSuccessChance", PerkStats.TerrorEffectArrestSuccessChance),
                new StatOffset("MeleeHitChance", PerkStats.TerrorEffectMeleeHitChance),
                new StatOffset("MeleeDodgeChance", PerkStats.TerrorEffectMeleeDodgeChance),
                new StatOffset("SocialImpact", PerkStats.TerrorEffectSocialImpact));

            ApplyPerkOffsets("HK_Hediff_ReputationTax",
                new StatOffset("TradePriceImprovement", PerkStats.ReputationTaxTradePriceImprovement),
                new StatOffset("NegotiationAbility", PerkStats.ReputationTaxNegotiationAbility));
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HKBalanceTuning.ApplyPerkDefOverrides",
                "Hero Karma failed to apply perk Hediff balance overrides.",
                ex);
        }
    }

    private static void ApplyPerkOffsets(string hediffDefName, params StatOffset[] offsets)
    {
        HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(hediffDefName);
        if (def == null) return;

        if (def.stages == null)
            def.stages = new List<HediffStage>();

        HediffStage stage;
        if (def.stages.Count == 0)
        {
            stage = new HediffStage();
            def.stages.Add(stage);
        }
        else
        {
            stage = def.stages[0] ?? new HediffStage();
            def.stages[0] = stage;
        }

        if (stage.statOffsets == null)
            stage.statOffsets = new List<StatModifier>();
        else
            stage.statOffsets.Clear();

        for (int i = 0; i < offsets.Length; i++)
        {
            StatOffset offset = offsets[i];
            StatDef statDef = DefDatabase<StatDef>.GetNamedSilentFail(offset.StatDefName);
            if (statDef == null) continue;

            stage.statOffsets.Add(new StatModifier
            {
                stat = statDef,
                value = offset.Value
            });
        }
    }

    private readonly struct StatOffset
    {
        public readonly string StatDefName;
        public readonly float Value;

        public StatOffset(string statDefName, float value)
        {
            StatDefName = statDefName;
            Value = value;
        }
    }

    private static string FormatStatOffset(string label, float value)
    {
        return label + " " + FormatSignedPercent(value);
    }

    private static string FormatSignedPercent(float value, int decimals = 0)
    {
        float pct = value * 100f;
        string fmt = decimals <= 0 ? "0" : ("0." + new string('0', decimals));
        string s = pct.ToString(fmt);
        if (pct > 0f) s = "+" + s;
        return s + "%";
    }

    private static string FormatAbsPercent(float value, int decimals = 0)
    {
        return FormatSignedPercent(Mathf.Abs(value), decimals).TrimStart('+');
    }

    private static string FormatAbsNumber(float value, int decimals = 0)
    {
        string fmt = decimals <= 0 ? "0" : ("0." + new string('0', decimals));
        return Math.Abs(value).ToString(fmt);
    }
}
