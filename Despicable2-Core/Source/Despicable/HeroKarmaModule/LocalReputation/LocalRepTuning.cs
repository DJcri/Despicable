using System;
using UnityEngine;

namespace Despicable.HeroKarma;

/// <summary>
/// Legacy wrapper around the centralized Hero Karma balance hub.
///
/// Keep this file so existing callers stay stable, but edit HKBalanceTuning.cs for the real
/// local-reputation numbers.
/// </summary>
public static class LocalRepTuning
{
    // Prisoner influence (applied during recruit/convert interactions)
    public const float PrisonerSilverTongueMult = HKBalanceTuning.LocalRep.PrisonerSilverTongueMult;

    public const float RecruitCoeff = HKBalanceTuning.LocalRep.RecruitCoeff;
    public const float RecruitClampMin = HKBalanceTuning.LocalRep.RecruitClampMin;
    public const float RecruitClampMax = HKBalanceTuning.LocalRep.RecruitClampMax;

    public const float ConvertCoeff = HKBalanceTuning.LocalRep.ConvertCoeff;
    public const float ConvertClampMin = HKBalanceTuning.LocalRep.ConvertClampMin;
    public const float ConvertClampMax = HKBalanceTuning.LocalRep.ConvertClampMax;

    // Arrest compliance salvage (applied only after a failed arrest check)
    public const float ArrestTrustMax = HKBalanceTuning.LocalRep.ArrestTrustMax;
    public const float ArrestFearSynergyMax = HKBalanceTuning.LocalRep.ArrestFearSynergyMax;
    public const float ArrestChanceCap = HKBalanceTuning.LocalRep.ArrestChanceCap;

    // Goodwill bias (only for hero-instigated goodwill changes)
    public const float GoodwillStrength = HKBalanceTuning.LocalRep.GoodwillStrength;
    public const float GoodwillClampMin = HKBalanceTuning.LocalRep.GoodwillClampMin;
    public const float GoodwillClampMax = HKBalanceTuning.LocalRep.GoodwillClampMax;

    // Trade pricing bias (negotiator only when detectable)
    public const float TradeCoeff = HKBalanceTuning.LocalRep.TradeCoeff;
    public const float TradeClamp = HKBalanceTuning.LocalRep.TradeClamp;

    public static float RecruitDeltaResistance(float influenceIndex, bool silverTongue)
    {
        float mult = silverTongue ? PrisonerSilverTongueMult : 1f;
        return Mathf.Clamp((RecruitCoeff * influenceIndex) * mult, RecruitClampMin, RecruitClampMax);
    }

    public static float ConvertDeltaCertainty(float influenceIndex, bool silverTongue)
    {
        float mult = silverTongue ? PrisonerSilverTongueMult : 1f;
        return Mathf.Clamp((ConvertCoeff * influenceIndex) * mult, ConvertClampMin, ConvertClampMax);
    }

    public static float ArrestTrustChance(float influenceIndex)
    {
        return Mathf.Max(0f, influenceIndex) * ArrestTrustMax;
    }

    public static float ArrestFearSynergyChance(float influenceIndex, bool hasFearPerks)
    {
        if (!hasFearPerks) return 0f;
        if (influenceIndex >= 0f) return 0f;
        return Mathf.Max(0f, -influenceIndex) * ArrestFearSynergyMax;
    }

    public static float ClampArrestChance(float salvageChance)
    {
        return Mathf.Min(ArrestChanceCap, Mathf.Max(0f, salvageChance));
    }

    public static float GoodwillMultiplier(int goodwillChange, float influenceIndex)
    {
        if (goodwillChange == 0) return 1f;
        if (goodwillChange > 0)
            return Mathf.Clamp(1f + (GoodwillStrength * influenceIndex), GoodwillClampMin, GoodwillClampMax);
        return Mathf.Clamp(1f - (GoodwillStrength * influenceIndex), GoodwillClampMin, GoodwillClampMax);
    }

    public static float TradeDelta(float influenceIndex)
    {
        return Mathf.Clamp(TradeCoeff * influenceIndex, -TradeClamp, +TradeClamp);
    }

    public static string SignedPct(float value, int decimals = 0)
    {
        float pct = value * 100f;
        string fmt = decimals <= 0 ? "0" : ("0." + new string('0', decimals));
        string s = pct.ToString(fmt);
        if (pct > 0f) s = "+" + s;
        return s + "%";
    }
}
