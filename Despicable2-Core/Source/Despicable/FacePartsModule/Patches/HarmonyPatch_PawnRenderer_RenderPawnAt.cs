using HarmonyLib;
using Verse;

namespace Despicable;

[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
internal static class HarmonyPatch_PawnRenderer_RenderPawnAt
{
    private static readonly AccessTools.FieldRef<PawnRenderer, Pawn> PawnFieldRef = CreatePawnFieldRef();

    private static AccessTools.FieldRef<PawnRenderer, Pawn> CreatePawnFieldRef()
    {
        try
        {
            return AccessTools.FieldRefAccess<PawnRenderer, Pawn>("pawn");
        }
        catch
        {
            return null;
        }
    }

    private static void Postfix(PawnRenderer __instance)
    {
        if (ModMain.IsNlFacialInstalled || __instance == null || WorkshopRenderContext.Active || FacePartsPortraitRenderContext.NonWorkshopPortraitActive)
            return;

        Pawn pawn = PawnFieldRef != null
            ? PawnFieldRef(__instance)
            : PawnOwnerReflectionUtil.TryGetPawn(__instance, warnKeyPrefix: "HarmonyPatch_PawnRenderer_RenderPawnAt.ReadPawnMember", warnMessagePrefix: "RenderPawnAt patch could not read pawn member");
        if (pawn?.Spawned != true)
            return;

        Map currentMap = Find.CurrentMap;
        if (currentMap == null || pawn.Map != currentMap)
            return;

        CompFaceParts compFaceParts = pawn.TryGetComp<CompFaceParts>();
        if (compFaceParts == null)
            return;

        compFaceParts.NotifyRuntimeRendered(Find.TickManager?.TicksGame ?? 0);
    }
}
