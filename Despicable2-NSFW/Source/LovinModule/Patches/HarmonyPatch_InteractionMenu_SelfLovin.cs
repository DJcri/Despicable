using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Verse;

namespace Despicable;

/// <summary>
/// NSFW contribution point: inject solo lovin types into the Core self-interaction submenu.
/// Core stays SFW while NSFW extends the self lane additively.
/// </summary>
[HarmonyPatch]
internal static class HarmonyPatch_InteractionMenu_GenerateSelfOptions
{
    private static IEnumerable<System.Reflection.MethodBase> TargetMethods()
    {
        var m = AccessTools.Method(typeof(InteractionMenu), "GenerateSelfOptionSpecs");
        if (m != null)
            yield return m;
    }

    private static void Postfix(Pawn pawn, LocalTargetInfo target, ref IEnumerable<ManualMenuOptionSpec> __result)
    {
        var list = __result?.ToList() ?? new List<ManualMenuOptionSpec>();

        Pawn targetPawn = target.Pawn;
        if (pawn == null || targetPawn == null || pawn != targetPawn)
        {
            __result = list;
            return;
        }

        list.AddRange(LovinInteractions.GenerateSelfLovinOptionSpecs(pawn));
        __result = list;
    }
}
