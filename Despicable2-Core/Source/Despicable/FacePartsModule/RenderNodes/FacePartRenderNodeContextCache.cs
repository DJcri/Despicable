using System;
using UnityEngine;
using Verse;

namespace Despicable;

internal enum FacePartExpressionOffsetKind
{
    None = 0,
    Eyes,
    Brows,
    Mouth,
    FaceDetail,
    EyeDetail
}

internal static class FacePartRenderNodeContextCache
{
    public static CompFaceParts ResolveCompFaceParts(Pawn pawn)
    {
        return pawn?.TryGetComp<CompFaceParts>();
    }

    public static CompFaceParts ResolveCompFaceParts(PawnRenderNode node, Pawn fallbackPawn = null)
    {
        if (node is PawnRenderNode_EyeAddon eyeAddonNode)
            return eyeAddonNode.CompFaceParts;

        if (node is PawnRenderNode_Mouth mouthNode)
            return mouthNode.CompFaceParts;

        return ResolveCompFaceParts(fallbackPawn ?? node?.tree?.pawn);
    }

    public static string GetDebugLabel(PawnRenderNode node)
    {
        if (node is PawnRenderNode_EyeAddon eyeAddonNode)
            return eyeAddonNode.DebugLabel;

        if (node is PawnRenderNode_Mouth mouthNode)
            return mouthNode.DebugLabel;

        return node?.Props?.debugLabel;
    }

    public static bool IsRightCounterpartNode(PawnRenderNode node)
    {
        if (node is PawnRenderNode_EyeAddon eyeAddonNode)
            return eyeAddonNode.IsRightCounterpartNodeCached;

        if (node is PawnRenderNode_Mouth mouthNode)
            return mouthNode.IsRightCounterpartNodeCached;

        string debugLabel = GetDebugLabel(node);
        return !debugLabel.NullOrEmpty() && debugLabel.EndsWith("_R", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsLeftCounterpartNode(PawnRenderNode node)
    {
        if (node is PawnRenderNode_EyeAddon eyeAddonNode)
            return eyeAddonNode.IsLeftCounterpartNodeCached;

        if (node is PawnRenderNode_Mouth mouthNode)
            return mouthNode.IsLeftCounterpartNodeCached;

        string debugLabel = GetDebugLabel(node);
        return !debugLabel.NullOrEmpty() && debugLabel.EndsWith("_L", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBrowNode(PawnRenderNode node)
    {
        if (node is PawnRenderNode_EyeAddon eyeAddonNode)
            return eyeAddonNode.IsBrowNodeCached;

        string debugLabel = GetDebugLabel(node);
        return IsBrowDebugLabel(debugLabel);
    }

    public static bool IsEyeFacingNode(PawnRenderNode node)
    {
        if (node is PawnRenderNode_EyeAddon eyeAddonNode)
            return eyeAddonNode.IsEyeFacingNodeCached;

        string debugLabel = GetDebugLabel(node);
        return MatchesEyeFacingDebugLabel(debugLabel);
    }

    public static Vector3? GetExpressionOffset(ExpressionDef expressionDef, PawnRenderNode node)
    {
        if (expressionDef == null || node == null)
            return null;

        FacePartExpressionOffsetKind kind = node switch
        {
            PawnRenderNode_EyeAddon eyeAddonNode => eyeAddonNode.ExpressionOffsetKind,
            PawnRenderNode_Mouth mouthNode => mouthNode.ExpressionOffsetKind,
            _ => ResolveOffsetKind(GetDebugLabel(node))
        };

        return kind switch
        {
            FacePartExpressionOffsetKind.Eyes => expressionDef.eyesOffset,
            FacePartExpressionOffsetKind.Brows => expressionDef.browsOffset ?? expressionDef.eyesOffset,
            FacePartExpressionOffsetKind.Mouth => expressionDef.mouthOffset,
            FacePartExpressionOffsetKind.FaceDetail => expressionDef.faceDetailOffset ?? expressionDef.mouthOffset,
            FacePartExpressionOffsetKind.EyeDetail => expressionDef.eyeDetailOffset ?? expressionDef.detailOffset,
            _ => null
        };
    }

    internal static bool IsBrowDebugLabel(string debugLabel)
    {
        return !debugLabel.NullOrEmpty()
            && (debugLabel.Equals("FacePart_Brow_L", StringComparison.OrdinalIgnoreCase)
                || debugLabel.Equals("FacePart_Brow_R", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool MatchesEyeFacingDebugLabel(string debugLabel)
    {
        return !debugLabel.NullOrEmpty()
            && (debugLabel.Equals("FacePart_Eye_L", StringComparison.OrdinalIgnoreCase)
                || debugLabel.Equals("FacePart_Eye_R", StringComparison.OrdinalIgnoreCase)
                || debugLabel.Equals("FacePart_EyeBase_L", StringComparison.OrdinalIgnoreCase)
                || debugLabel.Equals("FacePart_EyeBase_R", StringComparison.OrdinalIgnoreCase)
                || debugLabel.Equals("FacePart_EyeDetail_L", StringComparison.OrdinalIgnoreCase)
                || debugLabel.Equals("FacePart_EyeDetail_R", StringComparison.OrdinalIgnoreCase)
                || debugLabel.Equals("FacePart_AutoEyePatch_L", StringComparison.OrdinalIgnoreCase)
                || debugLabel.Equals("FacePart_AutoEyePatch_R", StringComparison.OrdinalIgnoreCase));
    }

    internal static FacePartExpressionOffsetKind ResolveOffsetKind(string debugLabel)
    {
        if (debugLabel.NullOrEmpty())
            return FacePartExpressionOffsetKind.None;

        switch (debugLabel)
        {
            case "FacePart_Eye_L":
            case "FacePart_Eye_R":
                return FacePartExpressionOffsetKind.Eyes;
            case "FacePart_Brow_L":
            case "FacePart_Brow_R":
                return FacePartExpressionOffsetKind.Brows;
            case "FacePart_Mouth":
            case "FacePart_Mouth_L":
            case "FacePart_Mouth_R":
                return FacePartExpressionOffsetKind.Mouth;
            case "FacePart_FaceDetail":
                return FacePartExpressionOffsetKind.FaceDetail;
            case "FacePart_EyeDetail_L":
            case "FacePart_EyeDetail_R":
            case "FacePart_SecondaryDetail_L":
            case "FacePart_SecondaryDetail_R":
                return FacePartExpressionOffsetKind.EyeDetail;
            default:
                return FacePartExpressionOffsetKind.None;
        }
    }
}
