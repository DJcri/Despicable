using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
/// <summary>
/// Render-only owner for lovin genital nodes. Runtime lovin activity and clothes suppression now live in
/// LovinVisualRuntime so the comp can stay responsive without carrying a per-pawn tick loop.
/// </summary>
public class CompLovinParts : ThingComp
{
    private CompExtendedAnimator cachedAnimator;
    private bool animatorResolved;

    internal static void RehydrateAllLovinPartsAfterRuntimeReset()
    {
        LovinVisualRuntime.RehydrateAllAfterRuntimeReset();
    }

    internal static void RefreshAllLovinPartsForSettingsChange()
    {
        LovinVisualRuntime.RefreshAllForSettingsChange();
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        LovinVisualRuntime.ClearPawn(OwnerPawn);
    }

    public override void PostDestroy(DestroyMode mode, Map previousMap)
    {
        base.PostDestroy(mode, previousMap);
        LovinVisualRuntime.ClearPawn(OwnerPawn);
    }

    public override List<PawnRenderNode> CompRenderNodes()
    {
        Pawn pawn = OwnerPawn;
        if (!CanRenderLovinGenitals(pawn))
            return null;

        GenitalDef genitalDef = LovinModule_GenitalDefOf.Genital_Penis;
        if (genitalDef == null)
            return null;

        bool isWorkshopPreview = WorkshopRenderContext.Active;
        bool isAnimating = isWorkshopPreview || (AnimatorComp?.hasAnimPlaying == true);
        bool shouldShowForCoverage = !HasPants(pawn);
        if (!shouldShowForCoverage)
            shouldShowForCoverage = LovinVisualRuntime.IsLovinVisualActiveForRender(pawn);

        if (!shouldShowForCoverage && !isAnimating)
            return null;

        PawnRenderNodeProperties genitalProps = CommonUtil.CloneNodeProperties(genitalDef.properties);
        if (genitalProps == null)
            return null;

        genitalProps.anchorTag = "Body";
        genitalProps.texPath = isAnimating
            ? genitalDef.texPathAroused
            : genitalDef.properties.texPath;

        PawnRenderNode genitalNode = CommonUtil.CreateNodeFromOwnedProps(pawn, genitalProps, PawnRenderNodeTagDefOf.Body);
        if (genitalNode == null)
            return null;

        return new List<PawnRenderNode>(1) { genitalNode };
    }

    private static bool CanRenderLovinGenitals(Pawn pawn)
    {
        Settings settings = CommonUtil.GetSettings();
        return (settings?.nudityEnabled ?? true)
            && (settings?.renderGenitalsEnabled ?? true)
            && pawn != null
            && pawn.RaceProps?.Humanlike == true
            && pawn.ageTracker?.Adult == true
            && IsStructurallyEligibleForLovinParts(pawn);
    }

    private static bool IsStructurallyEligibleForLovinParts(Pawn pawn)
    {
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return false;

        if (Despicable.NSFW.Integrations.IntegrationGuards.IsGenderWorksLoaded())
            return Despicable.NSFW.Integrations.GenderWorks.GenderWorksUtil.HasMaleReproductiveOrganTag(pawn);

        return PawnStateUtil.ComparePawnGenderToByte(pawn, (byte)Gender.Male);
    }

    private static bool HasPants(Pawn pawn)
    {
        bool hasPants = false;
        bool hasShirt = false;
        pawn?.apparel?.HasBasicApparel(out hasPants, out hasShirt);
        return hasPants;
    }

    private Pawn OwnerPawn => parent as Pawn;

    private CompExtendedAnimator AnimatorComp
    {
        get
        {
            if (!animatorResolved)
            {
                cachedAnimator = OwnerPawn?.TryGetComp<CompExtendedAnimator>();
                animatorResolved = true;
            }

            return cachedAnimator;
        }
    }
}
