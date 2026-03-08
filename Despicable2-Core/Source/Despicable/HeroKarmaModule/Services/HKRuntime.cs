using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// UI-facing runtime accessors.
///
/// Rule: HeroKarma is the real compiled system.
/// This class must not depend on HeroModule/legacy types.
/// </summary>
public static class HKRuntime
{
    public const int KarmaMin = HKBalanceTuning.KarmaMin;
    public const int KarmaMax = HKBalanceTuning.KarmaMax;

    public static Pawn GetHeroPawnSafe()
    {
        if (!HKSettingsUtil.ModuleEnabled)
            return null;

        var bridge = HKBackendBridge.Bridge;
        if (bridge != null)
        {
            try { return bridge.GetHeroPawnSafe(); }
            catch { }
        }

        var gc = Current.Game?.GetComponent<GameComponent_HeroKarma>();
        return gc?.ResolveHeroPawnSafe();
    }

    public static int GetGlobalKarma(Pawn hero)
    {
        if (!HKSettingsUtil.ModuleEnabled)
            return 0;

        var bridge = HKBackendBridge.Bridge;
        if (bridge != null)
        {
            try { return Mathf.Clamp(bridge.GetGlobalKarma(hero), KarmaMin, KarmaMax); }
            catch { }
        }

        var gc = Current.Game?.GetComponent<GameComponent_HeroKarma>();
        return gc != null ? Mathf.Clamp(gc.GlobalKarma, KarmaMin, KarmaMax) : 0;
    }

    public static int GetGlobalStanding(Pawn hero)
    {
        if (!HKSettingsUtil.ModuleEnabled)
            return 0;

        var bridge = HKBackendBridge.Bridge;
        if (bridge != null)
        {
            try { return Mathf.Clamp(bridge.GetGlobalStanding(hero), KarmaMin, KarmaMax); }
            catch { }
        }

        var gc = Current.Game?.GetComponent<GameComponent_HeroKarma>();
        return gc != null ? Mathf.Clamp(gc.GlobalStanding, KarmaMin, KarmaMax) : 0;
    }

    public static float GetInfluenceIndex(int score)
    {
        float s = Mathf.Clamp(score, KarmaMin, KarmaMax) / 100f;
        if (Mathf.Abs(s) < 0.0001f) return 0f;
        return Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.75f);
    }

    public static HKTier GetTierFor(int karma)
    {
        karma = Mathf.Clamp(karma, KarmaMin, KarmaMax);

        if (karma >= HKBalanceTuning.TierParagonMin) return HKTier.Paragon;
        if (karma >= HKBalanceTuning.TierTrustedMin) return HKTier.Trusted;
        if (karma <= HKBalanceTuning.TierDreadedMax) return HKTier.Dreaded;
        if (karma <= HKBalanceTuning.TierNotoriousMax) return HKTier.Notorious;
        return HKTier.Neutral;
    }

    public static string GetAxisLabel(int karma)
    {
        if (karma >= HKBalanceTuning.TierTrustedMin) return "Beloved";
        if (karma <= HKBalanceTuning.TierNotoriousMax) return "Feared";
        return "Balanced";
    }

    public static string GetStandingBandLabel(int standing)
    {
        if (standing >= HKBalanceTuning.StandingExemplaryMin) return "Exemplary";
        if (standing >= HKBalanceTuning.StandingApprovedMin) return "Approved";
        if (standing <= HKBalanceTuning.StandingHereticalMax) return "Heretical";
        if (standing <= HKBalanceTuning.StandingSuspectMax) return "Suspect";
        if (standing <= HKBalanceTuning.StandingQuestionedMax) return "Questioned";
        if (standing >= HKBalanceTuning.StandingRespectedMin) return "Respected";
        return "Neutral";
    }

    public static IEnumerable<HKLedgerRow> GetLedgerRows(Pawn hero, int cap = 40)
    {
        if (!HKSettingsUtil.ModuleEnabled)
            yield break;

        var bridge = HKBackendBridge.Bridge;
        if (bridge != null)
        {
            IEnumerable<HKLedgerRow> rows = null;
            try { rows = bridge.GetLedgerRows(hero, cap); }
            catch { rows = null; }

            if (rows != null)
            {
                foreach (var r in rows) yield return r;
                yield break;
            }
        }

        var gc = Current.Game?.GetComponent<GameComponent_HeroKarma>();
        if (gc == null) yield break;
        foreach (var r in gc.BuildLedgerRowsForUI(hero, cap)) yield return r;
    }

    public static HKLedgerRow GetLatestLedgerRow(Pawn hero)
    {
        HKLedgerRow latest = null;
        foreach (var r in GetLedgerRows(hero, cap: 1)) latest = r;
        return latest;
    }

    public static IEnumerable<HKPerkDef> GetActivePerksFor(int karma)
    {
        if (!HKSettingsUtil.ModuleEnabled)
            return System.Array.Empty<HKPerkDef>();

        var bridge = HKBackendBridge.Bridge;
        if (bridge != null)
        {
            try
            {
                var perks = bridge.GetActivePerksFor(karma);
                if (perks != null) return perks;
            }
            catch { }
        }

        return HKPerkCatalog.GetPerksFor(GetTierFor(karma));
    }
}

public enum HKTier
{
    Paragon,
    Trusted,
    Neutral,
    Notorious,
    Dreaded
}

public sealed class HKLedgerRow
{
    public string eventKey;
    public int delta; // karma delta
    public int standingDelta;
    public string label;
    public string detail;
    public string reason; // karma reason
    public string standingReason;
    public int ticks;
}
