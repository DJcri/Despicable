using HarmonyLib;

namespace Despicable.Core.Compatibility.PerspectiveShiftCompat;
internal sealed class PerspectiveShiftCompatModule : IModCompat
{
    public string Id
    {
        get { return "PerspectiveShift"; }
    }

    public bool CanActivate()
    {
        return PerspectiveShiftCompatUtility.IsAvailable
            || HarmonyPatch_PerspectiveShift_AvatarInteractionProviders.CanApply()
            || HarmonyPatch_PerspectiveShift_State_SetAvatar.CanApply();
    }

    public void Activate()
    {
        ModMain.harmony ??= new Harmony(DespicableBootstrap.HarmonyId);
        HarmonyPatch_PerspectiveShift_AvatarInteractionProviders.Apply(ModMain.harmony);
        HarmonyPatch_PerspectiveShift_State_SetAvatar.Apply(ModMain.harmony);
    }

    public string ReportStatus()
    {
        return "suppressed overlapping avatar chat/insult float-menu entries, auto-synced the Authentic avatar into HeroKarma, and hid manual hero reassignment while Authentic mode is active";
    }
}
