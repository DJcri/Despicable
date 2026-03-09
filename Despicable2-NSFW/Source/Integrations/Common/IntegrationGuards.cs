using RimWorld;
using Verse;

namespace Despicable.NSFW.Integrations;
/// <summary>
/// Centralizes "blast radius" rules for third-party integrations.
/// Keep these conservative to minimize side-effects.
/// </summary>
internal static class IntegrationGuards
{
    // --- General pair checks ---

    /// <summary>
    /// Broad allowance for animation-only integrations:
    /// humanoid (Humanlike) + flesh, excluding mechanoids and animals.
    /// This includes Humanoid Alien Races, etc.
    /// </summary>
    internal static bool IsHumanoidFleshPairForAnimation(Pawn a, Pawn b)
    {
        if (a == null || b == null) return false;
        var ra = a.RaceProps; var rb = b.RaceProps;
        if (ra == null || rb == null) return false;
        if (!ra.Humanlike || !rb.Humanlike) return false;
        if (!ra.IsFlesh || !rb.IsFlesh) return false;
        if (ra.IsMechanoid || rb.IsMechanoid) return false;
        return true;
    }

    /// <summary>
    /// Strict allowance for pregnancy/cooldown gating logic.
    /// We keep this Human↔Human only by default, so alien reproduction systems are not impacted.
    /// </summary>
    internal static bool IsVanillaHumanPairForPregnancy(Pawn a, Pawn b)
    {
        if (a == null || b == null) return false;
        return a.def == ThingDefOf.Human && b.def == ThingDefOf.Human;
    }

    // --- Per-mod feature gates ---

    internal static bool IsIntimacyLoaded()
        => ModsConfig.IsActive("LovelyDovey.Sex.WithEuterpe");

    /// <summary>
    /// When Intimacy is installed, Despicable defers autonomous lovin initiation to it.
    /// Manual lovin visibility is controlled separately by the user setting.
    /// </summary>
    internal static bool ShouldDeferLovinToIntimacy()
        => IsIntimacyLoaded();

    /// <summary>
    /// When Intimacy is installed, Despicable keeps its own ordered manual lovin flow
    /// but borrows Intimacy's validation rules instead of using Despicable-only consent checks.
    /// </summary>
    internal static bool ShouldUseIntimacyForLovinValidation()
        => IsIntimacyLoaded();

    internal static bool ShouldHideManualLovinOptionWithIntimacy()
    {
        if (!IsIntimacyLoaded()) return false;

        object settings = CommonUtil.GetSettings();
        if (settings == null) return true;

        var settingsType = settings.GetType();
        var field = settingsType.GetField("hideManualLovinOptionWhenIntimacyInstalled");
        if (field != null && field.FieldType == typeof(bool))
        {
            return (bool)field.GetValue(settings);
        }

        var prop = settingsType.GetProperty("hideManualLovinOptionWhenIntimacyInstalled");
        if (prop != null && prop.PropertyType == typeof(bool) && prop.CanRead)
        {
            return (bool)prop.GetValue(settings, null);
        }

        return true;
    }

    internal static bool IsGenderWorksLoaded()
        => ModsConfig.IsActive("LovelyDovey.Sex.WithRosaline");
}
