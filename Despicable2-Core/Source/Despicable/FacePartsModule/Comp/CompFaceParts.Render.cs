using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Despicable;
public partial class CompFaceParts
{
    public override List<PawnRenderNode> CompRenderNodes()
    {
        if (ModMain.IsNlFacialInstalled)
            return null;

        if (pawn == null || !enabled || pawn.RaceProps?.Humanlike != true)
            return null;

        // Assign styles if not already assigned
        if (mouthStyleDef == null || eyeStyleDef == null)
            AssignStylesRandomByWeight();

        List<PawnRenderNode> facePartNodes = new();

        bool useAutoEyePatchNodes = false;
        if (pawn.story?.headType != null
            && AutoEyePatchRuntime.TryGetOrEnsureHeadResult(pawn.story.headType, out AutoEyePatchHeadResult autoEyePatchResult)
            && autoEyePatchResult != null
            && autoEyePatchResult.ReplacesLegacyEyeBase)
        {
            useAutoEyePatchNodes = true;
        }

        PawnRenderNodeProperties detailProps = CommonUtil.CloneNodeProperties(DefDatabase<FacePartDef>.GetNamed("FacePart_Detail_L").properties);
        detailProps.texPath = "FaceParts/Details/detail_empty";

        // Create symmetrical nodes for details
        PawnRenderNode detailNode = CommonUtil.CreateNodeFromOwnedProps(pawn, detailProps, PawnRenderNodeTagDefOf.Head);
        if (detailNode != null)
            facePartNodes.Add(detailNode);

        detailProps = CommonUtil.CloneNodeProperties(DefDatabase<FacePartDef>.GetNamed("FacePart_Detail_R").properties);
        detailProps.texPath = detailNode?.Props?.texPath ?? "FaceParts/Details/detail_empty";
        PawnRenderNode detailNodeMirror = CommonUtil.CreateNodeFromOwnedProps(pawn, detailProps, PawnRenderNodeTagDefOf.Head);
        if (detailNodeMirror != null)
            facePartNodes.Add(detailNodeMirror);

        // Render using animation first, conditional second, style last
        List<FacePartDef> allFacePartDefs = DefDatabase<FacePartDef>.AllDefsListForReading;
        for (int i = 0; i < allFacePartDefs.Count; i++)
        {
            FacePartDef facePartDef = allFacePartDefs[i];
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
                    if (debugLabel.Equals("FacePart_EyeBase_L", StringComparison.OrdinalIgnoreCase))
                    {
                        TryAddAutoEyePatchNode(facePartNodes, "FacePart_EyeBase_L", "FacePart_AutoEyePatch_L");
                        continue;
                    }

                    if (debugLabel.Equals("FacePart_EyeBase_R", StringComparison.OrdinalIgnoreCase))
                    {
                        TryAddAutoEyePatchNode(facePartNodes, "FacePart_EyeBase_R", "FacePart_AutoEyePatch_R");
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

    private void TryAddAutoEyePatchNode(List<PawnRenderNode> facePartNodes, string sourceDefName, string runtimeDebugLabel)
    {
        FacePartDef sourceDef = DefDatabase<FacePartDef>.GetNamedSilentFail(sourceDefName);
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
