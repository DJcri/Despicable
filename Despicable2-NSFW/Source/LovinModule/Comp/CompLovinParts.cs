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

        bool isWorkshopPreview = WorkshopRenderContext.Active;
        bool hasRuntimeAnimationPlaying = AnimatorComp?.hasAnimPlaying == true;
        bool isAnimating = isWorkshopPreview || hasRuntimeAnimationPlaying;
        bool shouldShowForLovinVisual = LovinVisualRuntime.IsLovinVisualActiveForRender(pawn);

        // Route A policy: normal anatomy should not ambient-render on the live map merely because pants are absent.
        // Keep genital visuals scoped to explicit lovin visuals, active animation playback, or workshop preview.
        if (!shouldShowForLovinVisual && !isAnimating)
            return null;

        List<PawnRenderNode> nodes = new List<PawnRenderNode>(2);
        TryAddGenitalNode(nodes, pawn, LovinModule_GenitalDefOf.Genital_Penis, AnatomyQuery.HasPenis(pawn), isAnimating);

        // Female genital overlays proved visually noisy during active animation and preview playback.
        // Suppress them whenever we are sampling an animation scene, including workshop/authoring preview.
        bool shouldShowVaginaNode = AnatomyQuery.HasVagina(pawn) && !isAnimating;
        TryAddGenitalNode(nodes, pawn, LovinModule_GenitalDefOf.Genital_Vagina, shouldShowVaginaNode, isAnimating);

        return nodes.Count > 0 ? nodes : null;
    }

    private static void TryAddGenitalNode(List<PawnRenderNode> nodes, Pawn pawn, GenitalDef genitalDef, bool shouldAdd, bool isAnimating)
    {
        if (!shouldAdd || genitalDef == null)
            return;

        PawnRenderNodeProperties genitalProps = CommonUtil.CloneNodeProperties(genitalDef.properties);
        if (genitalProps == null)
            return;

        genitalProps.anchorTag = "Body";
        genitalProps.texPath = isAnimating
            ? (genitalDef.texPathAroused.NullOrEmpty() ? genitalDef.properties?.texPath : genitalDef.texPathAroused)
            : genitalDef.properties?.texPath;

        if (genitalProps.texPath.NullOrEmpty())
            return;

        PawnRenderNode genitalNode = CommonUtil.CreateNodeFromOwnedProps(pawn, genitalProps, PawnRenderNodeTagDefOf.Body);
        if (genitalNode != null)
            nodes.Add(genitalNode);
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

        return AnatomyQuery.HasPenis(pawn) || AnatomyQuery.HasVagina(pawn);
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
