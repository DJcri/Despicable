using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
internal static class LovinRenderPatchUtil
{
    internal static bool ShouldProcessLovinRenderPatch(Pawn pawn)
    {
        // Apparel suppression owns visual nudity only. Genital node rendering is gated separately in CompLovinParts.
        if (!(CommonUtil.GetSettings()?.nudityEnabled ?? true)) return false;
        if (!VisualActivityTracker.AnyLovinVisualsActive) return false;
        if (pawn == null) return false;
        if (pawn.RaceProps?.Humanlike != true) return false;
        return VisualActivityTracker.IsLovinVisualActive(pawn);
    }
}

[HarmonyPatch(typeof(PawnRenderNodeWorker_Apparel_Body), "CanDrawNow")]
public static class HarmonyPatch_ApparelBody_CanDrawNow
{
    [HarmonyPostfix]
    public static void Postfix(PawnRenderNode __instance, PawnDrawParms parms, ref bool __result)
    {
        Pawn pawn = parms.pawn;
        if (!LovinRenderPatchUtil.ShouldProcessLovinRenderPatch(pawn)) return;
        __result = false;
    }
}

[HarmonyPatch(typeof(PawnRenderNodeWorker_Apparel_Head), "CanDrawNow")]
public static class HarmonyPatch_ApparelHead_CanDrawNow
{
    [HarmonyPostfix]
    public static void Postfix(PawnRenderNode __instance, PawnDrawParms parms, ref bool __result)
    {
        Pawn pawn = parms.pawn;
        if (!LovinRenderPatchUtil.ShouldProcessLovinRenderPatch(pawn)) return;
        __result = false;
    }
}

// Prevent pawns from becoming bald while 'lovin'.
[HarmonyPatch(typeof(PawnRenderTree), "ParallelPreDraw")]
public static class HarmonyPatch_PawnRenderTreeBaldingFix
{
    // Use a Prefix to modify the parms before they're used for rendering.
    public static void Prefix(ref PawnDrawParms parms, PawnRenderTree __instance)
    {
        // We can now access the pawn from the PawnRenderTree instance.
        Pawn pawn = __instance.pawn;
        if (!LovinRenderPatchUtil.ShouldProcessLovinRenderPatch(pawn)) return;

        // Unset the Headgear and Clothes flags.
        parms.flags &= ~PawnRenderFlags.Headgear;
        parms.flags &= ~PawnRenderFlags.Clothes;
    }
}
