using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace Despicable;
// Renders the pawn's body when they are 'lovin',
// overriding the game's default rendering logic.
[HarmonyPatch(typeof(PawnRenderNodeWorker_Body), "CanDrawNow")]
public class HarmonyPatch_PawnRenderNodeWorkerBody
{
    public static void Postfix(ref bool __result, PawnDrawParms parms)
    {
        Pawn pawn = parms.pawn;
        if (!LovinRenderPatchUtil.ShouldProcessLovinRenderPatch(pawn)) return;
        __result = true;
    }
}
