using System.Collections.Generic;

namespace Despicable.HeroKarma;
public static partial class HKPerkCatalog
{
    private static readonly Dictionary<string, HKPerkDef> perks = new Dictionary<string, HKPerkDef>
    {
        // Paragon
                    {
            "HK_PERK_MERCY_MAGNET",
            new HKPerkDef(
                "HK_PERK_MERCY_MAGNET",
                "Mercy Magnet",
                HKBalanceTuning.DescribeMercyMagnetTooltip(),
                "MercyMagnet")
        },
                    {
            "HK_PERK_GOODWILL_TAILWIND",
            new HKPerkDef(
                "HK_PERK_GOODWILL_TAILWIND",
                "Goodwill Tailwind",
                HKBalanceTuning.DescribeGoodwillTailwindTooltip(),
                "GoodwillTailwind")
        },

        // Trusted
                    {
            "HK_PERK_SILVER_TONGUE",
            new HKPerkDef(
                "HK_PERK_SILVER_TONGUE",
                "Silver Tongue",
                HKBalanceTuning.DescribeSilverTongueTooltip(),
                "SilverTongue")
        },
                    {
            "HK_PERK_COMMUNITY_BUFFER",
            new HKPerkDef(
                "HK_PERK_COMMUNITY_BUFFER",
                "Community Buffer",
                HKBalanceTuning.DescribeCommunityBufferTooltip(),
                "CommunityBuffer")
        },

        // Notorious
                    {
            "HK_PERK_INTIMIDATING_PRESENCE",
            new HKPerkDef(
                "HK_PERK_INTIMIDATING_PRESENCE",
                "Intimidating Presence",
                HKBalanceTuning.DescribeIntimidatingPresenceTooltip(),
                "IntimidatingPresence")
        },
                    {
            "HK_PERK_GOODWILL_FRICTION",
            new HKPerkDef(
                "HK_PERK_GOODWILL_FRICTION",
                "Goodwill Friction",
                HKBalanceTuning.DescribeGoodwillFrictionTooltip(),
                "GoodwillFriction")
        },

        // Dreaded
                    {
            "HK_PERK_TERROR_EFFECT",
            new HKPerkDef(
                "HK_PERK_TERROR_EFFECT",
                "Terror Effect",
                HKBalanceTuning.DescribeTerrorEffectTooltip(),
                "TerrorEffect")
        },
                    {
            "HK_PERK_REPUTATION_TAX",
            new HKPerkDef(
                "HK_PERK_REPUTATION_TAX",
                "Reputation Tax",
                HKBalanceTuning.DescribeReputationTaxTooltip(),
                "ReputationTax")
        },
    };

    private static readonly Dictionary<HKTier, List<string>> tierToPerkIds = new Dictionary<HKTier, List<string>>
    {
                    {
            HKTier.Paragon,
            new List<string> { "HK_PERK_MERCY_MAGNET", "HK_PERK_GOODWILL_TAILWIND" }
        },
                    {
            HKTier.Trusted,
            new List<string> { "HK_PERK_SILVER_TONGUE", "HK_PERK_COMMUNITY_BUFFER" }
        },
        { HKTier.Neutral, new List<string>() },
                    {
            HKTier.Notorious,
            new List<string> { "HK_PERK_INTIMIDATING_PRESENCE", "HK_PERK_GOODWILL_FRICTION" }
        },
                    {
            HKTier.Dreaded,
            new List<string> { "HK_PERK_TERROR_EFFECT", "HK_PERK_REPUTATION_TAX" }
        },
    };

    private static readonly HKTier[] orderedTiers =
    {
        HKTier.Paragon,
        HKTier.Trusted,
        HKTier.Neutral,
        HKTier.Notorious,
        HKTier.Dreaded,
    };
}
