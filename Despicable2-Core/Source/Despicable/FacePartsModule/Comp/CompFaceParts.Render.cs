using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
public partial class CompFaceParts
{
    private const string FacePartDetailTagDefName = "FacePart_Detail";
    private const string FacePartSecondaryDetailTagDefName = "FacePart_SecondaryDetail";
    private const string FacePartDetailLeftDebugLabel = "FacePart_Detail_L";
    private const string FacePartDetailRightDebugLabel = "FacePart_Detail_R";
    private const string FacePartSecondaryDetailLeftDebugLabel = "FacePart_SecondaryDetail_L";
    private const string FacePartSecondaryDetailRightDebugLabel = "FacePart_SecondaryDetail_R";
    private const string FacePartEyeBaseLeftDebugLabel = "FacePart_EyeBase_L";
    private const string FacePartEyeBaseRightDebugLabel = "FacePart_EyeBase_R";
    private const string FacePartAutoEyePatchLeftDebugLabel = "FacePart_AutoEyePatch_L";
    private const string FacePartAutoEyePatchRightDebugLabel = "FacePart_AutoEyePatch_R";

    // Guardrail-Allow-Static: Shared filtered FacePartDef cache owned by CompFaceParts render setup; safe because defs are immutable for the current load and rebuild on assembly/domain reload.
    private static List<FacePartDef> cachedRenderableFacePartDefs;
    // Guardrail-Allow-Static: Shared eye-base source def cache owned by CompFaceParts render setup; safe because the resolved FacePartDef is immutable for the current load.
    private static FacePartDef cachedEyeBaseLeftFacePartDef;
    // Guardrail-Allow-Static: Shared eye-base source def cache owned by CompFaceParts render setup; safe because the resolved FacePartDef is immutable for the current load.
    private static FacePartDef cachedEyeBaseRightFacePartDef;

    public override List<PawnRenderNode> CompRenderNodes()
    {
        if (ModMain.IsNlFacialInstalled)
            return null;

        if (pawn == null || pawn.RaceProps?.Humanlike != true || !IsRenderActiveNow())
            return null;

        // Assign styles if not already assigned
        if (!AreStyleSlotsAssigned())
            AssignStylesRandomByWeight();

        List<PawnRenderNode> facePartNodes = new();

        bool useAutoEyePatchNodes = false;
        if (pawn.story?.headType != null
            && AutoEyePatchRuntime.TryGetOrRequestHeadResult(pawn.story.headType, pawn, out AutoEyePatchHeadResult autoEyePatchResult)
            && autoEyePatchResult != null
            && autoEyePatchResult.ReplacesLegacyEyeBase)
        {
            useAutoEyePatchNodes = true;
        }

        // Render using animation first, conditional second, style last
        List<FacePartDef> renderableFacePartDefs = GetRenderableFacePartDefs();
        for (int i = 0; i < renderableFacePartDefs.Count; i++)
        {
            FacePartDef facePartDef = renderableFacePartDefs[i];
            try
            {
                PawnRenderNodeProperties facePartProps = CommonUtil.CloneNodeProperties(facePartDef.properties);
                if (facePartProps == null)
                {
                    continue;
                }

                switch (facePartProps.debugLabel)
                {
                    case "FacePart_Eye_L":
                    case "FacePart_Eye_R":
                        facePartProps.texPath = FacePartsUtil.GetEyePath(pawn, facePartProps.texPath);
                        break;
                }

                string debugLabel = facePartProps.debugLabel ?? string.Empty;
                if (useAutoEyePatchNodes)
                {
                    if (debugLabel.Equals(FacePartEyeBaseLeftDebugLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        TryAddAutoEyePatchNode(facePartNodes, GetEyeBaseLeftFacePartDef(), FacePartAutoEyePatchLeftDebugLabel);
                        continue;
                    }

                    if (debugLabel.Equals(FacePartEyeBaseRightDebugLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        TryAddAutoEyePatchNode(facePartNodes, GetEyeBaseRightFacePartDef(), FacePartAutoEyePatchRightDebugLabel);
                        continue;
                    }
                }

                // Don't render if nothing to render
                if (facePartProps.texPath.NullOrEmpty() || facePartProps.texPath.StartsWith("Gendered/"))
                {
                    continue;
                }

                // Treat right-side nodes as permanently mirrored.
                // Actual draw visibility is still handled by the worker based on parms.facing.
                bool isRightHalf = !facePartProps.debugLabel.NullOrEmpty()
                    && facePartProps.debugLabel.EndsWith("_R", StringComparison.OrdinalIgnoreCase);
                facePartProps.flipGraphic = isRightHalf;

                // Prevent face part from rendering on the back side of the pawn's head when flipped.
                facePartProps.oppositeFacingLayerWhenFlipped = false;

                PawnRenderNode facePartNode = CommonUtil.CreateNodeFromOwnedProps(pawn, facePartProps, PawnRenderNodeTagDefOf.Head);
                if (facePartNode != null)
                {
                    facePartNodes.Add(facePartNode);
                }
            }
            catch (Exception e)
            {
                Log.Error($"[Despicable] - Error in CompFaceParts CompRenderNodes for {facePartDef.defName}: {e}");
                continue;
            }
        }

        return facePartNodes;
    }

    private static List<FacePartDef> GetRenderableFacePartDefs()
    {
        if (cachedRenderableFacePartDefs != null)
            return cachedRenderableFacePartDefs;

        List<FacePartDef> renderableFacePartDefs = new();
        List<FacePartDef> allFacePartDefs = DefDatabase<FacePartDef>.AllDefsListForReading;
        for (int i = 0; i < allFacePartDefs.Count; i++)
        {
            FacePartDef facePartDef = allFacePartDefs[i];
            if (facePartDef?.properties == null || ShouldSkipFacePartDef(facePartDef.properties))
                continue;

            renderableFacePartDefs.Add(facePartDef);
        }

        cachedRenderableFacePartDefs = renderableFacePartDefs;
        return cachedRenderableFacePartDefs;
    }

    private static bool ShouldSkipFacePartDef(PawnRenderNodeProperties facePartProps)
    {
        string debugLabel = facePartProps.debugLabel ?? string.Empty;
        string tagDefName = facePartProps.tagDef?.defName ?? string.Empty;
        return tagDefName.Equals(FacePartDetailTagDefName, StringComparison.OrdinalIgnoreCase)
            || tagDefName.Equals(FacePartSecondaryDetailTagDefName, StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals(FacePartDetailLeftDebugLabel, StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals(FacePartDetailRightDebugLabel, StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals(FacePartSecondaryDetailLeftDebugLabel, StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals(FacePartSecondaryDetailRightDebugLabel, StringComparison.OrdinalIgnoreCase);
    }

    private static FacePartDef GetEyeBaseLeftFacePartDef()
    {
        cachedEyeBaseLeftFacePartDef ??= DefDatabase<FacePartDef>.GetNamedSilentFail(FacePartEyeBaseLeftDebugLabel);
        return cachedEyeBaseLeftFacePartDef;
    }

    private static FacePartDef GetEyeBaseRightFacePartDef()
    {
        cachedEyeBaseRightFacePartDef ??= DefDatabase<FacePartDef>.GetNamedSilentFail(FacePartEyeBaseRightDebugLabel);
        return cachedEyeBaseRightFacePartDef;
    }

    private void TryAddAutoEyePatchNode(List<PawnRenderNode> facePartNodes, FacePartDef sourceDef, string runtimeDebugLabel)
    {
        PawnRenderNodeProperties props = CommonUtil.CloneNodeProperties(sourceDef?.properties);
        if (props == null)
            return;

        props.debugLabel = runtimeDebugLabel;
        props.nodeClass = typeof(PawnRenderNode_EyeAddon);
        props.workerClass = typeof(PawnRenderNodeWorker_AutoEyePatch);
        props.texPath = "FaceParts/Details/detail_empty";
        props.drawSize = PawnRenderNode_EyeAddon.ResolveHeadOverlayDrawSize(pawn, props.drawSize);

        bool isRightHalf = !runtimeDebugLabel.NullOrEmpty()
            && runtimeDebugLabel.EndsWith("_R", StringComparison.OrdinalIgnoreCase);
        props.flipGraphic = isRightHalf;
        props.oppositeFacingLayerWhenFlipped = false;

        PawnRenderNode node = CommonUtil.CreateNodeFromOwnedProps(pawn, props, PawnRenderNodeTagDefOf.Head);
        if (node != null)
            facePartNodes.Add(node);
    }
}
