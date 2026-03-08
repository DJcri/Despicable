using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable.HeroKarma;
public static partial class HKPerkEffects
{
    private static readonly string[] allPerkHediffDefNames =
    {
        "HK_Hediff_MercyMagnet",
        "HK_Hediff_GoodwillTailwind",
        "HK_Hediff_SilverTongue",
        "HK_Hediff_CommunityBuffer",
        "HK_Hediff_IntimidatingPresence",
        "HK_Hediff_GoodwillFriction",
        "HK_Hediff_TerrorEffect",
        "HK_Hediff_ReputationTax"
    };

    private static void RemoveAllKnownPerkHediffs(Pawn pawn)
    {
        foreach (var hediffDefName in allPerkHediffDefNames)
        {
            RemoveHediff(pawn, hediffDefName);
        }
    }

    private static void TryApplyPerks(Pawn hero, List<string> perkIds)
    {
        if (hero == null || perkIds == null || perkIds.Count == 0) return;

        foreach (var perkId in perkIds)
        {
            TryApplyPerk(hero, perkId);
        }
    }

    private static void TryRemovePerks(Pawn hero, List<string> perkIds)
    {
        if (hero == null || perkIds == null || perkIds.Count == 0) return;

        foreach (var perkId in perkIds)
        {
            TryRemovePerk(hero, perkId);
        }
    }

    private static void TryApplyPerk(Pawn hero, string perkId)
    {
        var hediffDefName = GetHediffDefNameForPerk(perkId);
        if (hediffDefName.NullOrEmpty()) return;

        EnsureHediff(hero, hediffDefName);
    }

    private static void TryRemovePerk(Pawn hero, string perkId)
    {
        var hediffDefName = GetHediffDefNameForPerk(perkId);
        if (hediffDefName.NullOrEmpty()) return;

        RemoveHediff(hero, hediffDefName);
    }

    private static string GetHediffDefNameForPerk(string perkId)
    {
        if (perkId.NullOrEmpty()) return null;

        switch (perkId)
        {
            case "HK_PERK_MERCY_MAGNET":
                return "HK_Hediff_MercyMagnet";

            case "HK_PERK_GOODWILL_TAILWIND":
                return "HK_Hediff_GoodwillTailwind";

            case "HK_PERK_SILVER_TONGUE":
                return "HK_Hediff_SilverTongue";

            case "HK_PERK_COMMUNITY_BUFFER":
                return "HK_Hediff_CommunityBuffer";

            case "HK_PERK_INTIMIDATING_PRESENCE":
                return "HK_Hediff_IntimidatingPresence";

            case "HK_PERK_GOODWILL_FRICTION":
                return "HK_Hediff_GoodwillFriction";

            case "HK_PERK_TERROR_EFFECT":
                return "HK_Hediff_TerrorEffect";

            case "HK_PERK_REPUTATION_TAX":
                return "HK_Hediff_ReputationTax";

            default:
                return null;
        }
    }

    public static bool HasPerkHediff(Pawn pawn, string hediffDefName)
    {
        var def = HediffDefNamed(hediffDefName);
        if (def == null) return false;
        return pawn?.health?.hediffSet?.HasHediff(def) == true;
    }

    public static bool HasMercyMagnet(Pawn pawn) => HasPerkHediff(pawn, "HK_Hediff_MercyMagnet");
    public static bool HasSilverTongue(Pawn pawn) => HasPerkHediff(pawn, "HK_Hediff_SilverTongue");
    public static bool HasCommunityBuffer(Pawn pawn) => HasPerkHediff(pawn, "HK_Hediff_CommunityBuffer");
    public static bool HasIntimidatingPresence(Pawn pawn) => HasPerkHediff(pawn, "HK_Hediff_IntimidatingPresence");
    public static bool HasTerrorEffect(Pawn pawn) => HasPerkHediff(pawn, "HK_Hediff_TerrorEffect");

    /// <summary>
    /// Safe, soft dependency way to resolve hediff defs by name.
    /// If the Def doesn't exist (yet), we simply no-op.
    /// </summary>
    private static HediffDef HediffDefNamed(string defName)
    {
        if (defName.NullOrEmpty()) return null;
        return DefDatabase<HediffDef>.GetNamedSilentFail(defName);
    }

    private static void EnsureHediff(Pawn pawn, string hediffDefName)
    {
        var def = HediffDefNamed(hediffDefName);
        if (def == null) return;
        if (pawn?.health?.hediffSet == null) return;

        if (pawn.health.hediffSet.HasHediff(def)) return;
        pawn.health.AddHediff(def);
    }

    private static void RemoveHediff(Pawn pawn, string hediffDefName)
    {
        var def = HediffDefNamed(hediffDefName);
        if (def == null) return;
        if (pawn?.health?.hediffSet == null) return;

        var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
        if (hediff != null)
        {
            pawn.health.RemoveHediff(hediff);
        }
    }
}
