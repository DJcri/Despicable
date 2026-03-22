using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
/// <summary>
/// Render-only owner for lovin anatomy nodes. Runtime lovin activity and clothes suppression now live in
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
        if (!CanRenderLovinAnatomy(pawn))
            return null;

        bool isWorkshopPreview = WorkshopRenderContext.Active;
        bool hasRuntimeAnimationPlaying = AnimatorComp?.hasAnimPlaying == true;
        bool isAnimating = isWorkshopPreview || hasRuntimeAnimationPlaying;
        bool shouldShowForLovinVisual = LovinVisualRuntime.IsLovinVisualActiveForRender(pawn);

        if (!AnatomyQuery.TryGetResolvedParts(pawn, out List<AnatomyPartDef> parts) || parts == null || parts.Count == 0)
            return null;

        GetBasicApparelCoverage(pawn, out bool hasPants, out bool hasShirt);
        List<PawnRenderNode> nodes = new List<PawnRenderNode>(parts.Count);
        for (int i = 0; i < parts.Count; i++)
            TryAddAnatomyNode(nodes, pawn, parts[i], isAnimating, shouldShowForLovinVisual, hasPants, hasShirt);

        return nodes.Count > 0 ? nodes : null;
    }

    private static void TryAddAnatomyNode(List<PawnRenderNode> nodes, Pawn pawn, AnatomyPartDef part, bool isAnimating, bool shouldShowForLovinVisual, bool hasPants, bool hasShirt)
    {
        if (part == null || !part.visibleByDefault || part.slot?.renderable == false)
            return;

        if (isAnimating && !part.showWhileAnimating)
            return;

        if (!isAnimating && !part.showOutsideAnimation)
            return;

        if (!shouldShowForLovinVisual && !isAnimating && IsCoveredByApparel(part, hasPants, hasShirt))
            return;

        PawnRenderNodeProperties sourceProps = part.properties;
        if (sourceProps == null)
            return;

        AnatomyPartNodeProperties anatomyProps = CreateOwnedNodeProperties(sourceProps, part);
        if (anatomyProps == null)
            return;

        AnatomySlotDef slot = part.slot;
        anatomyProps.anchorTag = slot?.anchorTag ?? "Body";
        anatomyProps.texPath = AnatomyAppearanceResolver.ResolveTexturePath(pawn, part, isAnimating);

        if (anatomyProps.texPath.NullOrEmpty())
            return;

        PawnRenderNodeTagDef parentTag = slot?.parentTagDef ?? PawnRenderNodeTagDefOf.Body;
        PawnRenderNode node = CommonUtil.CreateNodeFromOwnedProps(pawn, anatomyProps, parentTag);
        if (node != null)
            nodes.Add(node);
    }

    private static AnatomyPartNodeProperties CreateOwnedNodeProperties(PawnRenderNodeProperties sourceProps, AnatomyPartDef part)
    {
        if (sourceProps == null || part == null)
            return null;

        AnatomyPartNodeProperties anatomyProps = new AnatomyPartNodeProperties();
        CopyFields(sourceProps, anatomyProps);
        anatomyProps.anatomyPart = part;
        return anatomyProps;
    }

    private static void CopyFields(PawnRenderNodeProperties source, PawnRenderNodeProperties target)
    {
        System.Type currentType = source.GetType();
        while (currentType != null)
        {
            System.Reflection.FieldInfo[] fields = currentType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
                fields[i].SetValue(target, fields[i].GetValue(source));

            currentType = currentType.BaseType;
        }
    }

    private static bool CanRenderLovinAnatomy(Pawn pawn)
    {
        Settings settings = CommonUtil.GetSettings();
        return (settings?.nudityEnabled ?? true)
            && (settings?.renderGenitalsEnabled ?? true)
            && pawn != null
            && pawn.RaceProps?.Humanlike == true
            && pawn.ageTracker?.Adult == true
            && IsStructurallyEligibleForLovinParts(pawn);
    }

    private static void GetBasicApparelCoverage(Pawn pawn, out bool hasPants, out bool hasShirt)
    {
        hasPants = false;
        hasShirt = false;
        pawn?.apparel?.HasBasicApparel(out hasPants, out hasShirt);
    }

    private static bool IsCoveredByApparel(AnatomyPartDef part, bool hasPants, bool hasShirt)
    {
        string slotDefName = part?.slot?.defName;
        if (slotDefName == "ExternalGenitals")
            return hasPants;

        if (slotDefName == "Chest")
            return hasShirt;

        return false;
    }

    private static bool IsStructurallyEligibleForLovinParts(Pawn pawn)
    {
        return AnatomyQuery.TryGetResolvedParts(pawn, out List<AnatomyPartDef> parts)
            && parts != null
            && parts.Count > 0;
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
