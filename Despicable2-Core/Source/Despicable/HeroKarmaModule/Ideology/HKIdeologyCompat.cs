using Verse;

namespace Despicable.HeroKarma;

/// <summary>
/// Shared Ideology availability gate for HeroKarma UI and mechanics.
/// Keeps DLC-presence checks centralized so no-Ideology loads fail closed cleanly.
/// </summary>
public static class HKIdeologyCompat
{
    public static bool IsAvailable
    {
        get
        {
            try
            {
                return ModsConfig.IdeologyActive;
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool IsStandingEnabled
    {
        get
        {
            return HKSettingsUtil.ModuleEnabled && IsAvailable && HKSettingsUtil.EnableIdeologyApproval;
        }
    }

    public static bool IsStandingEffectsEnabled
    {
        get
        {
            return IsStandingEnabled && HKSettingsUtil.StandingEnableEffects;
        }
    }
}
