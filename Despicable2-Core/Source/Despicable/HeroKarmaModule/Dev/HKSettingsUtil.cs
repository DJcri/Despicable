using System;
using Verse;

namespace Despicable.HeroKarma;
/// <summary>
/// Central settings access for HeroKarma (including DevMode-only hook gates).
/// Keep this in HeroKarma so patches can query without referencing legacy modules.
/// </summary>
internal static class HKSettingsUtil
{
    public static Despicable.Settings Settings
    {
        get
        {
            try { return Despicable.ModMain.Instance != null ? Despicable.ModMain.Instance.settings : null; }
            catch { return null; }
        }
    }

    public static bool ModuleEnabled
    {
        get { var s = Settings; return s == null ? true : s.heroModuleEnabled; }
    }

    public static bool EnableGlobalKarma
    {
        get { var s = Settings; return ModuleEnabled && (s == null ? true : s.heroKarmaEnableGlobalKarma); }
    }

    public static bool EnableLocalRep
    {
        get { var s = Settings; return ModuleEnabled && (s == null ? true : s.heroKarmaEnableLocalRep); }
    }

    public static bool EnableIdeologyApproval
    {
        get { var s = Settings; return ModuleEnabled && (s == null ? true : s.heroKarmaEnableIdeologyApproval); }
    }

    public static bool StandingEnableEffects
    {
        get { var s = Settings; return ModuleEnabled && (s == null ? true : s.heroKarmaStandingEnableEffects); }
    }

    public static bool IdeologyAvailable
    {
        get { return HKIdeologyCompat.IsAvailable; }
    }

    public static bool StandingFeatureEnabled
    {
        get { return HKIdeologyCompat.IsStandingEnabled; }
    }

    public static bool StandingEffectsUsable
    {
        get { return HKIdeologyCompat.IsStandingEffectsEnabled; }
    }

    public static bool LocalRepInfluencePrisoners
    {
        get { var s = Settings; return ModuleEnabled && (s == null ? true : s.heroKarmaLocalRepInfluencePrisoners); }
    }

    public static bool LocalRepArrestCompliance
    {
        get { var s = Settings; return ModuleEnabled && (s == null ? true : s.heroKarmaLocalRepArrestCompliance); }
    }

    public static bool LocalRepGoodwillBias
    {
        get { var s = Settings; return ModuleEnabled && (s == null ? true : s.heroKarmaLocalRepGoodwillBias); }
    }

    public static bool LocalRepTradePricing
    {
        get { var s = Settings; return ModuleEnabled && (s == null ? true : s.heroKarmaLocalRepTradePricing); }
    }

    public static bool DebugUI
    {
        get { var s = Settings; return ModuleEnabled && s != null && s.heroKarmaDebugUI; }
    }

    public static bool EchoDiagnosticsToLog
    {
        get { var s = Settings; return ModuleEnabled && s != null && s.heroKarmaEchoDiagnosticsToLog; }
    }

    // DevMode-only hook gates. If DevMode is off, hooks are always enabled.
    public const string EventSellCaptive = "SellCaptive";
    public const string LegacyEventSellPrisoner = "SellPrisoner";

    public static string CanonicalizeEventKey(string eventKey)
    {
        if (string.Equals(eventKey, LegacyEventSellPrisoner, StringComparison.Ordinal))
            return EventSellCaptive;

        return eventKey;
    }

    public static bool IsSellCaptiveEvent(string eventKey)
    {
        return string.Equals(CanonicalizeEventKey(eventKey), EventSellCaptive, StringComparison.Ordinal);
    }

    public static bool HookEnabled(string hookKey)
    {
        if (!ModuleEnabled) return false;
        if (!Prefs.DevMode) return true;

        var s = Settings;
        if (s == null) return true;

        switch (CanonicalizeEventKey(hookKey))
        {
            case "ExecutePrisoner": return s.hkDevHookExecutePrisoner;
            case "TendOutsider": return s.hkDevHookTendOutsider;
            case "ReleasePrisoner": return s.hkDevHookReleasePrisoner;
            case "EnslaveAttempt": return s.hkDevHookEnslaveAttempt;
            case "OrganHarvest": return s.hkDevHookOrganHarvest;
            case "CharityGift": return s.hkDevHookCharityGift;
            case "AttackNeutral": return s.hkDevHookAttackNeutral;
            case "ArrestNeutral": return s.hkDevHookArrestNeutral;
            case "RescueOutsider": return s.hkDevHookRescueOutsider;
            case "StabilizeOutsider": return s.hkDevHookStabilizeOutsider;
            case "KillDownedNeutral": return s.hkDevHookKillDownedNeutral;
            case "HarmGuest": return s.hkDevHookHarmGuest;
            case "FreeSlave": return s.hkDevHookFreeSlave;
            case "DonateToBeggars": return s.hkDevHookDonateToBeggars;
            case EventSellCaptive: return s.hkDevHookSellCaptive;
            default: return true;
        }
    }
}
