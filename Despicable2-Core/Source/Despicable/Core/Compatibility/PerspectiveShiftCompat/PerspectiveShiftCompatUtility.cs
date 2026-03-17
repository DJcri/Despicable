using System;
using System.Reflection;
using HarmonyLib;
using Verse;
using Despicable.HeroKarma;
using Despicable.HeroKarma.UI;

namespace Despicable.Core.Compatibility.PerspectiveShiftCompat;
internal static class PerspectiveShiftCompatUtility
{
    private static readonly Type StateType = AccessTools.TypeByName("PerspectiveShift.State");
    private static readonly Type AvatarType = AccessTools.TypeByName("PerspectiveShift.Avatar");
    private static readonly FieldInfo CurrentModeField = StateType != null ? AccessTools.Field(StateType, "CurrentMode") : null;
    private static readonly PropertyInfo CurrentProperty = StateType != null ? AccessTools.Property(StateType, "Current") : null;
    private static readonly FieldInfo AvatarPawnField = AvatarType != null ? AccessTools.Field(AvatarType, "pawn") : null;

    public static bool IsAvailable => StateType != null;

    public static bool IsAuthenticModeActive()
    {
        if (!IsAvailable)
            return false;

        object mode = CurrentModeField?.GetValue(null);
        return string.Equals(mode?.ToString(), "Authentic", StringComparison.Ordinal);
    }

    public static Pawn GetCurrentAvatarPawn()
    {
        if (!IsAvailable)
            return null;

        object avatar = CurrentProperty?.GetValue(null, null);
        if (avatar == null)
            return null;

        return AvatarPawnField?.GetValue(avatar) as Pawn;
    }

    public static bool ShouldSuppressManualHeroAssignGizmo(Pawn pawn)
    {
        return pawn != null
            && IsAuthenticModeActive()
            && GetCurrentAvatarPawn() != null
            && HKUIData.IsEligibleHero(pawn);
    }

    public static void TryAssignCurrentAuthenticAvatarAsHero()
    {
        if (!HKSettingsUtil.ModuleEnabled || !IsAuthenticModeActive())
            return;

        TryAssignHeroFromAvatar(GetCurrentAvatarPawn());
    }

    public static void TryAssignHeroFromAvatar(Pawn pawn)
    {
        if (!HKSettingsUtil.ModuleEnabled || !IsAuthenticModeActive())
            return;

        if (!HKUIData.IsEligibleHero(pawn))
            return;

        if (HeroKarmaBridge.GetHeroPawnSafe() == pawn)
            return;

        HeroKarmaBridge.SetHero(pawn);
    }
}
