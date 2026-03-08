using System.Collections.Generic;
using HarmonyLib;
using Verse;
using Despicable.HeroKarma.UI;

namespace Despicable.HeroKarma.Patches;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
public static class HarmonyPatch_AddHeroKarmaGizmos
{
    [HarmonyPostfix]
    private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
    {
        if (__result != null)
        {
            foreach (Gizmo gizmo in __result)
                if (gizmo != null)
                    yield return gizmo;
        }

        if (!HKSettingsUtil.ModuleEnabled)
            yield break;

        if (!HKUIData.IsEligibleHero(__instance))
            yield break;

        Pawn hero = HKRuntime.GetHeroPawnSafe();
        if (hero == __instance)
        {
            yield return new Command_OpenHeroKarma();
            yield break;
        }

        yield return new Command_SetHeroKarma(__instance);
    }
}
