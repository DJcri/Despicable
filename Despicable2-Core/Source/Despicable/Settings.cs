using System.Collections.Generic;
using Verse;

namespace Despicable;
public class Settings : ModSettings
{
    // Player settings
    public bool animationExtensionEnabled = true;
    public bool facialPartsExtensionEnabled = true;
    public bool experimentalAutoEyePatchEnabled = true;

    // NSFW module settings are persisted here in Core so the player has one central settings source.
    public bool lovinExtensionEnabled = true;

    // Lovin (NSFW addon) behavior
    // When enabled, preference checks also apply from the target back to the initiator.
    public bool lovinMutualConsent = true;

    // When disabled, ideology precepts do not block lovin.
    public bool lovinRespectIdeology = true;

    public bool heroModuleEnabled = true;
    public bool hideManualLovinOptionWhenIntimacyInstalled = true;
    // Legacy compatibility-only flag. Related-pawn lovin is now automatically allowed
    // when Birds of a Feather is installed, so this is no longer player-facing or read by gameplay.
    public bool allowRelatedLovinWhenBirdsOfAFeatherInstalled = false;

    // HeroKarma (Step 4)
    public bool heroKarmaEnableGlobalKarma = true;
    public bool heroKarmaEnableLocalRep = true;
    public bool heroKarmaEnableIdeologyApproval = true;
    public bool heroKarmaEchoDiagnosticsToLog = false;
    public bool heroKarmaAllowOffMapPlayerFactionSettlementWordOfMouth = true;

    // Compatibility-only migration flag for older saves/configs.
    // This is no longer player-facing, but we still read the legacy key so prior setups remain compatible.
    public bool heroKarmaMigrateLegacyKarmaToStanding = false;

    // Ideology Standing (effects)
    public bool heroKarmaStandingEnableEffects = true;

    // Local Rep gameplay effects (Step 5)
    public bool heroKarmaLocalRepInfluencePrisoners = true;
    public bool heroKarmaLocalRepArrestCompliance = true;
    public bool heroKarmaLocalRepGoodwillBias = true;
    public bool heroKarmaLocalRepTradePricing = true;

    // Debug UI (exposes internal formulas + safeguard details)
    public bool heroKarmaDebugUI = false;

    // HeroKarma DevMode hook gates (Step 4). Only used when DevMode is on.
    public bool hkDevHookExecutePrisoner = true;
    public bool hkDevHookTendOutsider = true;
    public bool hkDevHookReleasePrisoner = true;
    public bool hkDevHookEnslaveAttempt = true;
    public bool hkDevHookOrganHarvest = true;
    public bool hkDevHookCharityGift = true;
    public bool hkDevHookAttackNeutral = true;
    public bool hkDevHookArrestNeutral = true;
    public bool hkDevHookRescueOutsider = true;
    public bool hkDevHookStabilizeOutsider = true;
    public bool hkDevHookKillDownedNeutral = true;
    public bool hkDevHookHarmGuest = true;
    public bool hkDevHookFreeSlave = true;
    public bool hkDevHookDonateToBeggars = true;
    public bool hkDevHookSellCaptive = true;

    public bool nudityEnabled = true;
    public bool renderGenitalsEnabled = true;
    public float soundVolume = 1f;

    // Animation Workshop export settings
    public string workshopExportRootPath = ""; // absolute path to a mod project folder (optional)

    // Face-parts blacklist persistence
    public List<string> headTypeBlacklistDefNames = new();
    public List<string> allowedDefaultDisabledHeadDefNames = new();

    public override void ExposeData()
    {
        Scribe_Values.Look(ref animationExtensionEnabled, "animationExtensionEnabled", true);
        Scribe_Values.Look(ref facialPartsExtensionEnabled, "facialPartsExtensionEnabled", true);
        Scribe_Values.Look(ref experimentalAutoEyePatchEnabled, "experimentalAutoEyePatchEnabled", true);
        Scribe_Values.Look(ref lovinExtensionEnabled, "lovinExtensionEnabled", true);
        Scribe_Values.Look(ref lovinMutualConsent, "lovinMutualConsent", true);
        Scribe_Values.Look(ref lovinRespectIdeology, "lovinRespectIdeology", true);
        Scribe_Values.Look(ref heroModuleEnabled, "heroModuleEnabled", true);
        Scribe_Values.Look(ref hideManualLovinOptionWhenIntimacyInstalled, "hideManualLovinOptionWhenIntimacyInstalled", true);
        Scribe_Values.Look(ref allowRelatedLovinWhenBirdsOfAFeatherInstalled, "allowRelatedLovinWhenBirdsOfAFeatherInstalled", false);
        Scribe_Values.Look(ref heroKarmaEnableGlobalKarma, "heroKarmaEnableGlobalKarma", true);
        Scribe_Values.Look(ref heroKarmaEnableLocalRep, "heroKarmaEnableLocalRep", true);
        Scribe_Values.Look(ref heroKarmaEnableIdeologyApproval, "heroKarmaEnableIdeologyApproval", true);
        Scribe_Values.Look(ref heroKarmaEchoDiagnosticsToLog, "heroKarmaEchoDiagnosticsToLog", false);
        Scribe_Values.Look(ref heroKarmaAllowOffMapPlayerFactionSettlementWordOfMouth, "heroKarmaAllowOffMapPlayerFactionSettlementWordOfMouth", true);
        Scribe_Values.Look(ref heroKarmaMigrateLegacyKarmaToStanding, "heroKarmaMigrateLegacyKarmaToStanding", false);
        Scribe_Values.Look(ref heroKarmaStandingEnableEffects, "heroKarmaStandingEnableEffects", true);
        Scribe_Values.Look(ref heroKarmaLocalRepInfluencePrisoners, "heroKarmaLocalRepInfluencePrisoners", true);
        Scribe_Values.Look(ref heroKarmaLocalRepArrestCompliance, "heroKarmaLocalRepArrestCompliance", true);
        Scribe_Values.Look(ref heroKarmaLocalRepGoodwillBias, "heroKarmaLocalRepGoodwillBias", true);
        Scribe_Values.Look(ref heroKarmaLocalRepTradePricing, "heroKarmaLocalRepTradePricing", true);
        Scribe_Values.Look(ref heroKarmaDebugUI, "heroKarmaDebugUI", false);
        Scribe_Values.Look(ref hkDevHookExecutePrisoner, "hkDevHookExecutePrisoner", true);
        Scribe_Values.Look(ref hkDevHookTendOutsider, "hkDevHookTendOutsider", true);
        Scribe_Values.Look(ref hkDevHookReleasePrisoner, "hkDevHookReleasePrisoner", true);
        Scribe_Values.Look(ref hkDevHookEnslaveAttempt, "hkDevHookEnslaveAttempt", true);
        Scribe_Values.Look(ref hkDevHookOrganHarvest, "hkDevHookOrganHarvest", true);
        Scribe_Values.Look(ref hkDevHookCharityGift, "hkDevHookCharityGift", true);
        Scribe_Values.Look(ref hkDevHookAttackNeutral, "hkDevHookAttackNeutral", true);
        Scribe_Values.Look(ref hkDevHookArrestNeutral, "hkDevHookArrestNeutral", true);
        Scribe_Values.Look(ref hkDevHookRescueOutsider, "hkDevHookRescueOutsider", true);
        Scribe_Values.Look(ref hkDevHookStabilizeOutsider, "hkDevHookStabilizeOutsider", true);
        Scribe_Values.Look(ref hkDevHookKillDownedNeutral, "hkDevHookKillDownedNeutral", true);
        Scribe_Values.Look(ref hkDevHookHarmGuest, "hkDevHookHarmGuest", true);
        Scribe_Values.Look(ref hkDevHookFreeSlave, "hkDevHookFreeSlave", true);
        Scribe_Values.Look(ref hkDevHookDonateToBeggars, "hkDevHookDonateToBeggars", true);
        Scribe_Values.Look(ref hkDevHookSellCaptive, "hkDevHookSellPrisoner", true); // preserve legacy setting key

        Scribe_Values.Look(ref nudityEnabled, "nudityEnabled", true);
        Scribe_Values.Look(ref renderGenitalsEnabled, "renderGenitalsEnabled", true);
        Scribe_Values.Look(ref soundVolume, "soundVolume", 1f);
        Scribe_Values.Look(ref workshopExportRootPath, "workshopExportRootPath", "");
        Scribe_Collections.Look(ref headTypeBlacklistDefNames, "headTypeBlacklistDefNames", LookMode.Value);
        Scribe_Collections.Look(ref allowedDefaultDisabledHeadDefNames, "allowedDefaultDisabledHeadDefNames", LookMode.Value);

        headTypeBlacklistDefNames ??= new List<string>();
        allowedDefaultDisabledHeadDefNames ??= new List<string>();

        // Player-facing Hero Karma settings are currently intentionally minimal.
        // If a prior version saved custom advanced values, reset them to safe defaults
        // so players don't get stuck with hidden toggles.
        if (Scribe.mode == LoadSaveMode.LoadingVars && !Prefs.DevMode)
        {
            heroKarmaEnableGlobalKarma = true;
            heroKarmaEnableLocalRep = true;
            heroKarmaEnableIdeologyApproval = true;
            heroKarmaStandingEnableEffects = true;
            heroKarmaLocalRepInfluencePrisoners = true;
            heroKarmaLocalRepArrestCompliance = true;
            heroKarmaLocalRepGoodwillBias = true;
            heroKarmaLocalRepTradePricing = true;
            heroKarmaDebugUI = false;
            heroKarmaEchoDiagnosticsToLog = false;
        }

        base.ExposeData();
    }
}
