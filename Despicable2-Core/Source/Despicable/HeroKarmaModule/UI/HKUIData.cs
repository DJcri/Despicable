using System;
using System.Collections.Generic;
// Guardrail-Reason: Hero Karma UI data stays centralized because tier labels, effect cards, and reputation summaries share one presentation vocabulary.
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Despicable.UIFramework.Controls;

namespace Despicable.HeroKarma.UI;

internal enum HKRepFilter
{
    All,
    People,
    Factions
}

internal enum HKValueTint
{
    None,
    Positive,
    Negative
}

internal sealed class HKDisplayLine
{
    public string Label;
    public string Value;
    public HKValueTint ValueTint;
    public string Text;
    public string Tooltip;

    public bool IsPair => !Label.NullOrEmpty();

    public static HKDisplayLine Pair(string label, string value, HKValueTint valueTint = HKValueTint.None, string tooltip = null)
    {
        return new HKDisplayLine
        {
            Label = label,
            Value = value,
            ValueTint = valueTint,
            Tooltip = tooltip
        };
    }

    public static HKDisplayLine Note(string text, string tooltip = null)
    {
        return new HKDisplayLine
        {
            Text = text,
            Tooltip = tooltip
        };
    }
}

internal sealed class HKEffectCard
{
    public string Title;
    public string Description;
    public Texture2D Icon;
    public readonly List<HKDisplayLine> PrimaryLines = new();
    public readonly List<HKDisplayLine> SecondaryLines = new();
}

internal static class HKUIData
{
    public static readonly D2BandRuler.Band[] KarmaBands =
    {
        new("Dreaded", "Dreaded", HKRuntime.KarmaMin, HKBalanceTuning.TierDreadedMax),
        new("Notorious", "Notorious", HKBalanceTuning.TierDreadedMax + 1, HKBalanceTuning.TierNotoriousMax),
        new("Neutral", "Neutral", HKBalanceTuning.TierNotoriousMax + 1, HKBalanceTuning.TierTrustedMin - 1),
        new("Trusted", "Trusted", HKBalanceTuning.TierTrustedMin, HKBalanceTuning.TierParagonMin - 1),
        new("Paragon", "Paragon", HKBalanceTuning.TierParagonMin, HKRuntime.KarmaMax),
    };

    public static readonly D2BandRuler.Band[] StandingBands =
    {
        new("Heretical", "Heretical", HKRuntime.KarmaMin, HKBalanceTuning.StandingHereticalMax),
        new("Suspect", "Suspect", HKBalanceTuning.StandingHereticalMax + 1, HKBalanceTuning.StandingSuspectMax),
        new("Questioned", "Questioned", HKBalanceTuning.StandingSuspectMax + 1, HKBalanceTuning.StandingQuestionedMax),
        new("Neutral", "Neutral", HKBalanceTuning.StandingQuestionedMax + 1, HKBalanceTuning.StandingRespectedMin - 1),
        new("Respected", "Respected", HKBalanceTuning.StandingRespectedMin, HKBalanceTuning.StandingApprovedMin - 1),
        new("Approved", "Approved", HKBalanceTuning.StandingApprovedMin, HKBalanceTuning.StandingExemplaryMin - 1),
        new("Exemplary", "Exemplary", HKBalanceTuning.StandingExemplaryMin, HKRuntime.KarmaMax),
    };

    public static bool IsEligibleHero(Pawn pawn)
    {
        return HKSettingsUtil.ModuleEnabled
            && pawn != null
            && pawn.RaceProps?.Humanlike == true
            && pawn.IsColonistPlayerControlled;
    }

    public static string KarmaSummary(int karma)
    {
        return GetKarmaBandLabel(karma) + " (" + FormatSigned(karma) + ")";
    }

    public static string StandingSummary(int standing)
    {
        return GetStandingBandLabel(standing) + " (" + FormatSigned(standing) + ")";
    }

    public static string GetKarmaBandLabel(int karma)
    {
        return HKRuntime.GetTierFor(karma).ToString();
    }

    public static string GetStandingBandLabel(int standing)
    {
        return HKRuntime.GetStandingBandLabel(standing);
    }

    public static List<D2BandRulerRow.Milestone> BuildKarmaMilestones(int karma)
    {
        var milestones = new List<D2BandRulerRow.Milestone>();
        HashSet<string> activeIds = HKRuntime.GetActivePerksFor(karma).Select(x => x.id).ToHashSet();

        foreach (HKTier tier in HKPerkCatalog.AllTiersInOrder())
        {
            List<HKPerkDef> perks = HKPerkCatalog.GetPerksFor(tier).ToList();
            if (perks.Count == 0)
                continue;

            int bandIndex = tier switch
            {
                HKTier.Paragon => 4,
                HKTier.Trusted => 3,
                HKTier.Notorious => 1,
                HKTier.Dreaded => 0,
                _ => 2,
            };

            for (int i = 0; i < perks.Count; i++)
            {
                HKPerkDef perk = perks[i];
                float offset = perks.Count switch
                {
                    <= 1 => 0f,
                    2 => i == 0 ? -0.18f : 0.18f,
                    _ => Mathf.Lerp(-0.22f, 0.22f, i / (float)(perks.Count - 1))
                };
                milestones.Add(new D2BandRulerRow.Milestone(
                    bandIndex,
                    offset,
                    HKUIConstants.GetPerkIcon(perk.iconKey),
                    activeIds.Contains(perk.id),
                    tooltip: perk.label,
                    id: perk.id));
            }
        }

        return milestones;
    }

    public static string GetActiveEffectNames(int karma)
    {
        string joined = string.Join(", ", HKRuntime.GetActivePerksFor(karma).Select(x => x.label));
        return joined.NullOrEmpty() ? "D2HK_UI_NoActiveEffects".Translate().ToString() : joined;
    }

    public static string FormatSigned(int value)
    {
        return value > 0 ? "+" + value : value.ToString();
    }

    public static string FormatSignedPercent(float value, int decimals = 0)
    {
        float pct = value * 100f;
        string fmt = decimals <= 0 ? "0" : ("0." + new string('0', decimals));
        string s = pct.ToString(fmt);
        if (pct > 0f)
            s = "+" + s;
        return s + "%";
    }

    public static string FormatSignedFixed(float value, int decimals = 2)
    {
        string fmt = decimals <= 0 ? "0" : ("0." + new string('0', decimals));
        string s = value.ToString(fmt);
        if (value > 0f)
            s = "+" + s;
        return s;
    }

    public static HKValueTint TintForSigned(float value, bool beneficialWhenPositive)
    {
        if (Mathf.Approximately(value, 0f))
            return HKValueTint.None;

        bool beneficial = beneficialWhenPositive ? value > 0f : value < 0f;
        return beneficial ? HKValueTint.Positive : HKValueTint.Negative;
    }

    public static HKValueTint TintForSigned(int value, bool beneficialWhenPositive)
    {
        if (value == 0)
            return HKValueTint.None;

        bool beneficial = beneficialWhenPositive ? value > 0 : value < 0;
        return beneficial ? HKValueTint.Positive : HKValueTint.Negative;
    }

    public static List<HKEffectCard> BuildActiveEffectCards(Pawn hero, int karma)
    {
        var cards = new List<HKEffectCard>();
        if (hero == null)
            return cards;

        foreach (HKPerkDef perk in HKRuntime.GetActivePerksFor(karma))
        {
            switch (perk.id)
            {
                case "HK_PERK_MERCY_MAGNET":
                    cards.Add(BuildMercyMagnetCard(perk));
                    break;
                case "HK_PERK_GOODWILL_TAILWIND":
                    cards.Add(BuildGoodwillTailwindCard(perk));
                    break;
                case "HK_PERK_SILVER_TONGUE":
                    cards.Add(BuildSilverTongueCard(perk));
                    break;
                case "HK_PERK_COMMUNITY_BUFFER":
                    cards.Add(BuildCommunityBufferCard(perk));
                    break;
                case "HK_PERK_INTIMIDATING_PRESENCE":
                    cards.Add(BuildIntimidatingPresenceCard(perk));
                    break;
                case "HK_PERK_GOODWILL_FRICTION":
                    cards.Add(BuildGoodwillFrictionCard(perk));
                    break;
                case "HK_PERK_TERROR_EFFECT":
                    cards.Add(BuildTerrorEffectCard(perk));
                    break;
                case "HK_PERK_REPUTATION_TAX":
                    cards.Add(BuildReputationTaxCard(perk));
                    break;
            }
        }

        return cards;
    }

    public static List<HKDisplayLine> BuildStandingEffectLines(Pawn hero)
    {
        var lines = new List<HKDisplayLine>();
        if (!HKIdeologyCompat.IsStandingEnabled)
            return lines;

        if (!HKIdeologyCompat.IsStandingEffectsEnabled)
        {
            lines.Add(HKDisplayLine.Note("D2HK_UI_StandingEffectsDisabled".Translate()));
            return lines;
        }

        int standing = HKRuntime.GetGlobalStanding(hero);
        float influence = HKRuntime.GetInfluenceIndex(standing);
        float certaintyPct = Mathf.Clamp(HKBalanceTuning.StandingCertaintySwing * influence, -HKBalanceTuning.StandingCertaintySwing, HKBalanceTuning.StandingCertaintySwing);
        int opinion = Mathf.Clamp(Mathf.RoundToInt(HKBalanceTuning.StandingOpinionMaxDelta * influence), -HKBalanceTuning.StandingOpinionMaxDelta, HKBalanceTuning.StandingOpinionMaxDelta);

        lines.Add(HKDisplayLine.Pair("D2HK_UI_CertaintyGains".Translate(), FormatSignedPercent(certaintyPct), TintForSigned(certaintyPct, beneficialWhenPositive: true)));
        lines.Add(HKDisplayLine.Pair("D2HK_UI_CertaintyLosses".Translate(), FormatSignedPercent(-certaintyPct), TintForSigned(-certaintyPct, beneficialWhenPositive: true)));
        lines.Add(HKDisplayLine.Pair("D2HK_UI_SameIdeologyOpinion".Translate(), FormatSigned(opinion), TintForSigned(opinion, beneficialWhenPositive: true)));
        return lines;
    }

    public static List<RepSnapshot> FilterReputationSnapshots(List<RepSnapshot> source, HKRepFilter filter, string search)
    {
        IEnumerable<RepSnapshot> query = source ?? Enumerable.Empty<RepSnapshot>();
        if (filter == HKRepFilter.People)
            query = query.Where(x => !x.isFaction);
        else if (filter == HKRepFilter.Factions)
            query = query.Where(x => x.isFaction);

        if (!search.NullOrEmpty())
        {
            string needle = search.Trim();
            query = query.Where(x => (!x.displayName.NullOrEmpty() && x.displayName.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                || (!x.label.NullOrEmpty() && x.label.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        return query.OrderByDescending(x => Math.Abs(x.score)).ThenBy(x => x.displayName).ToList();
    }

    public static List<HKDisplayLine> BuildReputationSummaryLines(RepSnapshot snapshot)
    {
        var lines = new List<HKDisplayLine>();
        if (!snapshot.valid)
            return lines;

        lines.Add(HKDisplayLine.Pair("D2HK_UI_Reputation".Translate(), snapshot.label + " (" + FormatSigned(snapshot.score) + ")", TintForSigned(snapshot.score, beneficialWhenPositive: true), "D2HK_UI_ReputationTooltip".Translate()));
        lines.Add(HKDisplayLine.Pair("D2HK_UI_DirectReputation".Translate(), FormatSigned(snapshot.directScore), TintForSigned(snapshot.directScore, beneficialWhenPositive: true)));

        if (!snapshot.isFaction && snapshot.factionEchoScore != 0)
            lines.Add(HKDisplayLine.Pair("D2HK_UI_FactionEffect".Translate(), FormatSigned(snapshot.factionEchoScore), TintForSigned(snapshot.factionEchoScore, beneficialWhenPositive: true)));

        if (!snapshot.isFaction && snapshot.settlementEchoScore != 0)
            lines.Add(HKDisplayLine.Pair("D2HK_UI_SettlementEffect".Translate(), FormatSigned(snapshot.settlementEchoScore), TintForSigned(snapshot.settlementEchoScore, beneficialWhenPositive: true)));

        return lines;
    }

    public static List<HKDisplayLine> BuildReputationEffectLines(RepSnapshot snapshot, Pawn hero)
    {
        var lines = new List<HKDisplayLine>();
        if (!snapshot.valid || hero == null)
            return lines;

        float influence = LocalReputationUtility.InfluenceIndexFromScore(snapshot.score);
        if (snapshot.isFaction)
        {
            if (HKSettingsUtil.LocalRepTradePricing)
            {
                float delta = LocalRepTuning.TradeDelta(influence);
                if (delta > 0f && HKPerkEffects.HasPerkHediff(hero, "HK_Hediff_ReputationTax"))
                {
                    lines.Add(HKDisplayLine.Note("D2HK_UI_TradeBlockedByTax".Translate()));
                }
                else if (!Mathf.Approximately(delta, 0f))
                {
                    lines.Add(HKDisplayLine.Pair("D2HK_UI_TradePriceImprovement".Translate(), FormatSignedPercent(delta), TintForSigned(delta, beneficialWhenPositive: true)));
                }
            }

            if (HKSettingsUtil.LocalRepGoodwillBias)
            {
                float gainDelta = LocalRepTuning.GoodwillMultiplier(+10, influence) - 1f;
                float lossDelta = LocalRepTuning.GoodwillMultiplier(-10, influence) - 1f;
                if (!Mathf.Approximately(gainDelta, 0f))
                    lines.Add(HKDisplayLine.Pair("D2HK_UI_GoodwillGains".Translate(), FormatSignedPercent(gainDelta, 1), TintForSigned(gainDelta, beneficialWhenPositive: true)));
                if (!Mathf.Approximately(lossDelta, 0f))
                    lines.Add(HKDisplayLine.Pair("D2HK_UI_GoodwillLosses".Translate(), FormatSignedPercent(lossDelta, 1), TintForSigned(lossDelta, beneficialWhenPositive: false)));
            }
        }
        else
        {
            Pawn target = HKResolve.TryResolvePawnById(snapshot.targetId);
            bool hasSilverTongue = HKPerkEffects.HasSilverTongue(hero);
            bool hasIntimidating = HKPerkEffects.HasIntimidatingPresence(hero);
            bool hasTerror = HKPerkEffects.HasTerrorEffect(hero);

            if (target != null && HKSettingsUtil.LocalRepInfluencePrisoners && target.IsPrisonerOfColony)
            {
                float recruit = LocalRepTuning.RecruitDeltaResistance(influence, hasSilverTongue);
                float convert = LocalRepTuning.ConvertDeltaCertainty(influence, hasSilverTongue);

                if (!Mathf.Approximately(recruit, 0f))
                    lines.Add(HKDisplayLine.Pair("D2HK_UI_RecruitResistance".Translate(), FormatSignedFixed(recruit, 2), TintForSigned(recruit, beneficialWhenPositive: false)));
                if (!Mathf.Approximately(convert, 0f))
                    lines.Add(HKDisplayLine.Pair("D2HK_UI_ConvertCertainty".Translate(), FormatSignedPercent(convert, 1), TintForSigned(convert, beneficialWhenPositive: false)));
            }

            if (target != null && HKSettingsUtil.LocalRepArrestCompliance && IsArrestRelevant(target))
            {
                float trust = LocalRepTuning.ArrestTrustChance(influence);
                float fear = LocalRepTuning.ArrestFearSynergyChance(influence, hasIntimidating || hasTerror);
                if (trust > 0f)
                    lines.Add(HKDisplayLine.Pair("D2HK_UI_ArrestCompliance".Translate(), FormatSignedPercent(trust, 1), TintForSigned(trust, beneficialWhenPositive: true)));
                if (fear > 0f)
                    lines.Add(HKDisplayLine.Pair("D2HK_UI_ArrestComplianceFear".Translate(), FormatSignedPercent(fear, 1), TintForSigned(fear, beneficialWhenPositive: true)));
            }
        }

        if (lines.Count == 0)
            lines.Add(HKDisplayLine.Note("D2HK_UI_NoDirectEffect".Translate()));

        return lines;
    }

    public static List<HKDisplayLine> BuildReputationLastChangeLines(RepSnapshot snapshot)
    {
        var lines = new List<HKDisplayLine>();
        if (!snapshot.valid)
            return lines;

        if (!snapshot.lastEventKey.NullOrEmpty())
        {
            EventDisplayInfo info = HKServices.EventCatalog.Get(snapshot.lastEventKey);
            lines.Add(HKDisplayLine.Pair("D2HK_UI_Event".Translate(), info.label));
        }

        if (snapshot.lastDelta != 0)
            lines.Add(HKDisplayLine.Pair("D2HK_UI_ReputationChange".Translate(), FormatSigned(snapshot.lastDelta), TintForSigned(snapshot.lastDelta, beneficialWhenPositive: true), "D2HK_UI_ReputationChangeTooltip".Translate()));

        if (snapshot.lastBaseDelta != 0)
            lines.Add(HKDisplayLine.Pair("D2HK_UI_BaseChange".Translate(), FormatSigned(snapshot.lastBaseDelta), TintForSigned(snapshot.lastBaseDelta, beneficialWhenPositive: true), "D2HK_UI_BaseChangeTooltip".Translate()));

        if (!snapshot.lastAffectedByLabel.NullOrEmpty())
            lines.Add(HKDisplayLine.Pair("D2HK_UI_AffectedBy".Translate(), snapshot.lastAffectedByLabel, HKValueTint.None, "D2HK_UI_AffectedByTooltip".Translate()));

        if (!snapshot.reasonSummary.NullOrEmpty())
            lines.Add(HKDisplayLine.Pair("D2HK_UI_Reason".Translate(), snapshot.reasonSummary));

        if (lines.Count == 0)
            lines.Add(HKDisplayLine.Note("D2HK_UI_NoRecentDeeds".Translate()));

        return lines;
    }

    public static string GetReputationDisplayName(RepSnapshot snapshot)
    {
        return !snapshot.displayName.NullOrEmpty() ? snapshot.displayName : snapshot.targetId ?? "Unknown";
    }

    public static string GetReputationContext(RepSnapshot snapshot)
    {
        if (snapshot.isFaction)
            return null;

        if (!snapshot.factionEchoSourceLabel.NullOrEmpty())
            return snapshot.factionEchoSourceLabel;

        if (!snapshot.settlementContextLabel.NullOrEmpty())
            return snapshot.settlementContextLabel;

        return null;
    }

    public static string GetReputationBand(RepSnapshot snapshot)
    {
        return snapshot.label.NullOrEmpty() ? "D2HK_UI_Reputation".Translate().ToString() : snapshot.label;
    }

    private static bool IsArrestRelevant(Pawn target)
    {
        if (target == null)
            return false;
        if (target.Dead || target.Destroyed)
            return false;
        if (target.IsColonistPlayerControlled || target.IsPrisonerOfColony)
            return false;

        try
        {
            return !target.HostileTo(Faction.OfPlayer);
        }
        catch
        {
            return false;
        }
    }

    private static HKEffectCard BuildMercyMagnetCard(HKPerkDef perk)
    {
        var card = NewCard(perk, "D2HK_UI_PerkDesc_MercyMagnet".Translate());
        AddPercentMetric(card, "D2HK_UI_SocialImpact".Translate(), HKBalanceTuning.PerkStats.MercyMagnetSocialImpact, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_InjuryHealingFactor".Translate(), HKBalanceTuning.PerkStats.MercyMagnetInjuryHealingFactor, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_TradePriceImprovement".Translate(), HKBalanceTuning.PerkStats.MercyMagnetTradePriceImprovement, beneficialWhenPositive: true);
        card.SecondaryLines.Add(HKDisplayLine.Note("D2HK_UI_PerkNote_MercyMagnet".Translate()));
        return card;
    }

    private static HKEffectCard BuildGoodwillTailwindCard(HKPerkDef perk)
    {
        var card = NewCard(perk, "D2HK_UI_PerkDesc_GoodwillTailwind".Translate());
        AddPercentMetric(card, "D2HK_UI_NegotiationAbility".Translate(), HKBalanceTuning.PerkStats.GoodwillTailwindNegotiationAbility, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_SocialImpact".Translate(), HKBalanceTuning.PerkStats.GoodwillTailwindSocialImpact, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_TradePriceImprovement".Translate(), HKBalanceTuning.PerkStats.GoodwillTailwindTradePriceImprovement, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_GoodwillGains".Translate(), HKBalanceTuning.PerkBehavior.GoodwillTailwindPositiveBonus, beneficialWhenPositive: true, decimals: 0);
        AddPercentMetric(card, "D2HK_UI_GoodwillLosses".Translate(), HKBalanceTuning.PerkBehavior.GoodwillTailwindNegativeLossMultiplier - 1f, beneficialWhenPositive: false, decimals: 0);
        return card;
    }

    private static HKEffectCard BuildSilverTongueCard(HKPerkDef perk)
    {
        var card = NewCard(perk, "D2HK_UI_PerkDesc_SilverTongue".Translate());
        AddPercentMetric(card, "D2HK_UI_NegotiationAbility".Translate(), HKBalanceTuning.PerkStats.SilverTongueNegotiationAbility, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_SocialImpact".Translate(), HKBalanceTuning.PerkStats.SilverTongueSocialImpact, beneficialWhenPositive: true);
        AddFixedMetric(card, "D2HK_UI_RecruitResistance".Translate(), HKBalanceTuning.PerkBehavior.SilverTongueRecruitResistanceOffset, beneficialWhenPositive: false, decimals: 2);
        AddPercentMetric(card, "D2HK_UI_ConvertCertainty".Translate(), HKBalanceTuning.PerkBehavior.SilverTongueConvertCertaintyOffset, beneficialWhenPositive: false, decimals: 0);
        return card;
    }

    private static HKEffectCard BuildCommunityBufferCard(HKPerkDef perk)
    {
        var card = NewCard(perk, "D2HK_UI_PerkDesc_CommunityBuffer".Translate());
        AddPercentMetric(card, "D2HK_UI_MentalBreakThreshold".Translate(), HKBalanceTuning.PerkStats.CommunityBufferMentalBreakThreshold, beneficialWhenPositive: false);
        AddPercentMetric(card, "D2HK_UI_SocialImpact".Translate(), HKBalanceTuning.PerkStats.CommunityBufferSocialImpact, beneficialWhenPositive: true);
        card.SecondaryLines.Add(HKDisplayLine.Note("D2HK_UI_PerkNote_CommunityBuffer".Translate()));
        return card;
    }

    private static HKEffectCard BuildIntimidatingPresenceCard(HKPerkDef perk)
    {
        var card = NewCard(perk, "D2HK_UI_PerkDesc_IntimidatingPresence".Translate());
        AddPercentMetric(card, "D2HK_UI_ArrestSuccessChance".Translate(), HKBalanceTuning.PerkStats.IntimidatingPresenceArrestSuccessChance, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_MeleeHitChance".Translate(), HKBalanceTuning.PerkStats.IntimidatingPresenceMeleeHitChance, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_MentalBreakThreshold".Translate(), HKBalanceTuning.PerkStats.IntimidatingPresenceMentalBreakThreshold, beneficialWhenPositive: false);
        AddPercentMetric(card, "D2HK_UI_SocialImpact".Translate(), HKBalanceTuning.PerkStats.IntimidatingPresenceSocialImpact, beneficialWhenPositive: true);
        card.SecondaryLines.Add(HKDisplayLine.Note("D2HK_UI_PerkNote_IntimidatingPresence".Translate(FormatSignedPercent(HKBalanceTuning.PerkBehavior.IntimidatingPresenceArrestSalvage))));
        return card;
    }

    private static HKEffectCard BuildGoodwillFrictionCard(HKPerkDef perk)
    {
        var card = NewCard(perk, "D2HK_UI_PerkDesc_GoodwillFriction".Translate());
        AddPercentMetric(card, "D2HK_UI_NegotiationAbility".Translate(), HKBalanceTuning.PerkStats.GoodwillFrictionNegotiationAbility, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_TradePriceImprovement".Translate(), HKBalanceTuning.PerkStats.GoodwillFrictionTradePriceImprovement, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_SocialImpact".Translate(), HKBalanceTuning.PerkStats.GoodwillFrictionSocialImpact, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_GoodwillGains".Translate(), HKBalanceTuning.PerkBehavior.GoodwillFrictionPositiveMultiplier - 1f, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_GoodwillLosses".Translate(), HKBalanceTuning.PerkBehavior.GoodwillFrictionNegativeLossBonus, beneficialWhenPositive: false);
        return card;
    }

    private static HKEffectCard BuildTerrorEffectCard(HKPerkDef perk)
    {
        var card = NewCard(perk, "D2HK_UI_PerkDesc_TerrorEffect".Translate());
        AddPercentMetric(card, "D2HK_UI_ArrestSuccessChance".Translate(), HKBalanceTuning.PerkStats.TerrorEffectArrestSuccessChance, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_MeleeHitChance".Translate(), HKBalanceTuning.PerkStats.TerrorEffectMeleeHitChance, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_MeleeDodgeChance".Translate(), HKBalanceTuning.PerkStats.TerrorEffectMeleeDodgeChance, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_SocialImpact".Translate(), HKBalanceTuning.PerkStats.TerrorEffectSocialImpact, beneficialWhenPositive: true);
        card.SecondaryLines.Add(HKDisplayLine.Note("D2HK_UI_PerkNote_TerrorEffect".Translate(FormatSignedPercent(HKBalanceTuning.PerkStats.TerrorEffectMentalBreakThreshold), FormatSignedPercent(HKBalanceTuning.PerkBehavior.TerrorEffectArrestSalvage))));
        return card;
    }

    private static HKEffectCard BuildReputationTaxCard(HKPerkDef perk)
    {
        var card = NewCard(perk, "D2HK_UI_PerkDesc_ReputationTax".Translate());
        AddPercentMetric(card, "D2HK_UI_TradePriceImprovement".Translate(), HKBalanceTuning.PerkStats.ReputationTaxTradePriceImprovement, beneficialWhenPositive: true);
        AddPercentMetric(card, "D2HK_UI_NegotiationAbility".Translate(), HKBalanceTuning.PerkStats.ReputationTaxNegotiationAbility, beneficialWhenPositive: true);
        return card;
    }

    private static HKEffectCard NewCard(HKPerkDef perk, string description)
    {
        return new HKEffectCard
        {
            Title = perk?.label,
            Description = description,
            Icon = HKUIConstants.GetPerkIcon(perk?.iconKey)
        };
    }

    private static void AddPercentMetric(HKEffectCard card, string label, float value, bool beneficialWhenPositive, int decimals = 0)
    {
        card.PrimaryLines.Add(HKDisplayLine.Pair(label, FormatSignedPercent(value, decimals), TintForSigned(value, beneficialWhenPositive)));
    }

    private static void AddFixedMetric(HKEffectCard card, string label, float value, bool beneficialWhenPositive, int decimals = 2)
    {
        card.PrimaryLines.Add(HKDisplayLine.Pair(label, FormatSignedFixed(value, decimals), TintForSigned(value, beneficialWhenPositive)));
    }
}
