using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;

internal static class AutoEyePatchEligibility
{
    internal sealed class Result
    {
        public bool Eligible;
        public string HeadKey;
        public string GraphicPath;
        public Texture2D SouthTexture;
        public Texture2D EastTexture;
        public Texture2D WestTexture;
        public AutoEyePatchSkipReason Reasons;
    }

    public static Result Evaluate(HeadTypeDef headType)
    {
        Result result = new();
        if (headType == null)
        {
            result.Reasons = AutoEyePatchSkipReason.HeadIneligible;
            return result;
        }

        result.HeadKey = headType.defName ?? string.Empty;
        result.GraphicPath = headType.graphicPath ?? string.Empty;

        if (!IsGlobalFeatureEnabled() || !IsHeadEligibleByPolicy(headType))
        {
            result.Reasons = AutoEyePatchSkipReason.HeadIneligible;
            return result;
        }

        if (!TryResolveHeadTextures(headType, out string graphicPath, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture))
        {
            result.GraphicPath = graphicPath ?? result.GraphicPath;
            result.Reasons = AutoEyePatchSkipReason.TextureMissing;
            return result;
        }

        result.Eligible = true;
        result.GraphicPath = graphicPath;
        result.SouthTexture = southTexture;
        result.EastTexture = eastTexture;
        result.WestTexture = westTexture;
        return result;
    }

    private static bool IsGlobalFeatureEnabled()
    {
        Settings settings = ModMain.Instance?.settings;
        if (settings == null)
            return false;

        return settings.facialPartsExtensionEnabled && settings.experimentalAutoEyePatchEnabled && !ModMain.IsNlFacialInstalled;
    }

    private static bool IsHeadEligibleByPolicy(HeadTypeDef headType)
    {
        if (headType == null)
            return false;

        if (FacePartsUtil.IsHeadBlacklisted(headType))
            return false;

        return !headType.graphicPath.NullOrEmpty();
    }

    private static bool TryResolveHeadTextures(HeadTypeDef headType, out string graphicPath, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture)
    {
        graphicPath = headType?.graphicPath;
        southTexture = null;
        eastTexture = null;
        westTexture = null;

        if (headType == null || graphicPath.NullOrEmpty())
            return false;

        try
        {
            Graphic graphic = GraphicDatabase.Get<Graphic_Multi>(graphicPath, ShaderDatabase.CutoutSkin, Vector2.one, Color.white);
            if (graphic != null)
            {
                southTexture = graphic.MatAt(Rot4.South)?.mainTexture as Texture2D;
                eastTexture = graphic.MatAt(Rot4.East)?.mainTexture as Texture2D;
                westTexture = graphic.MatAt(Rot4.West)?.mainTexture as Texture2D;
            }
        }
        catch (Exception ex)
        {
            string headName = headType?.defName ?? "<null>";
            string pathName = graphicPath ?? "<null>";
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "AutoEyePatchEligibility.TryResolveHeadTextures.Graphic:" + headName,
                $"Auto eye patch eligibility could not resolve GraphicDatabase head textures for '{headName}' at '{pathName}'.",
                ex);
        }

        try
        {
            southTexture ??= ContentFinder<Texture2D>.Get(graphicPath, false);
        }
        catch (Exception ex)
        {
            string headName = headType?.defName ?? "<null>";
            string pathName = graphicPath ?? "<null>";
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "AutoEyePatchEligibility.TryResolveHeadTextures.ContentFinder:" + headName,
                $"Auto eye patch eligibility could not resolve ContentFinder head texture for '{headName}' at '{pathName}'.",
                ex);
        }

        return southTexture != null;
    }
}
