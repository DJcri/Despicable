using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;

internal static class FaceDetailBoundaryRuntime
{
    private sealed class FaceDetailBoundaryVariantSet
    {
        public Texture2D SouthTexture;
        public Texture2D EastTexture;
        public Texture2D WestTexture;

        public bool HasAny => SouthTexture != null || EastTexture != null || WestTexture != null;
    }

    // Guardrail-Allow-Static: Guarded head analyses are shared runtime cache owned by FaceDetailBoundaryRuntime; reset via ResetRuntimeState() on load/new game transitions.
    private static readonly Dictionary<string, AutoEyePatchTextureAnalysis> _headAnalysisByKey = new();
    // Guardrail-Allow-Static: Guarded face-detail runtime textures are shared by head/detail path and owned by FaceDetailBoundaryRuntime; reset via ResetRuntimeState() on load/new game transitions.
    private static readonly Dictionary<string, FaceDetailBoundaryVariantSet> _variantSetByKey = new();
    // Guardrail-Allow-Static: Mirrored runtime textures are cache derivatives owned by FaceDetailBoundaryRuntime; reset via ResetRuntimeState() on load/new game transitions.
    private static readonly Dictionary<Texture2D, Texture2D> _mirroredRuntimeTexturesBySource = new();

    private const int RuntimeVersion = 8;
    private const float SideFacingHalfHeadWidthOffsetFactor = 0.5f;

    public static void ResetRuntimeState()
    {
        HashSet<Texture2D> destroyed = new();
        foreach (FaceDetailBoundaryVariantSet variantSet in _variantSetByKey.Values)
        {
            DestroyRuntimeTexture(variantSet?.SouthTexture, destroyed);
            DestroyRuntimeTexture(variantSet?.EastTexture, destroyed);
            DestroyRuntimeTexture(variantSet?.WestTexture, destroyed);
        }

        foreach (Texture2D mirrored in _mirroredRuntimeTexturesBySource.Values)
            DestroyRuntimeTexture(mirrored, destroyed);

        _headAnalysisByKey.Clear();
        _variantSetByKey.Clear();
        _mirroredRuntimeTexturesBySource.Clear();
    }

    public static bool TryResolveGuardedTextures(Pawn pawn, string detailTexPath, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture)
    {
        southTexture = null;
        eastTexture = null;
        westTexture = null;

        if (pawn?.story?.headType == null || detailTexPath.NullOrEmpty() || string.Equals(detailTexPath, CompFaceParts.EMPTY_DETAIL_TEX_PATH, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!TryGetOrCreateVariantSet(pawn, pawn.story.headType, detailTexPath, out FaceDetailBoundaryVariantSet variantSet) || variantSet == null || !variantSet.HasAny)
            return false;

        southTexture = variantSet.SouthTexture;
        eastTexture = variantSet.EastTexture ?? southTexture;
        westTexture = variantSet.WestTexture ?? eastTexture ?? southTexture;
        return southTexture != null || eastTexture != null || westTexture != null;
    }

    public static bool TryResolveGuardedTexture(Pawn pawn, string detailTexPath, Rot4 facing, out Texture2D runtimeTexture)
    {
        runtimeTexture = null;
        if (!TryResolveGuardedTextures(pawn, detailTexPath, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture))
            return false;

        if (facing == Rot4.East)
            runtimeTexture = eastTexture ?? southTexture;
        else if (facing == Rot4.West)
            runtimeTexture = westTexture ?? eastTexture ?? southTexture;
        else
            runtimeTexture = southTexture ?? eastTexture ?? westTexture;

        return runtimeTexture != null;
    }

    private static bool TryGetOrCreateVariantSet(Pawn pawn, HeadTypeDef headType, string detailTexPath, out FaceDetailBoundaryVariantSet variantSet)
    {
        variantSet = null;
        if (headType == null || detailTexPath.NullOrEmpty())
            return false;

        float eyeSizeFactor = pawn?.ageTracker?.CurLifeStage?.eyeSizeFactor ?? 1f;
        string cacheKey = string.Concat(headType.defName ?? string.Empty, "|", detailTexPath, "|", eyeSizeFactor.ToString("R"), "|", RuntimeVersion.ToString());
        if (_variantSetByKey.TryGetValue(cacheKey, out variantSet) && variantSet != null)
            return variantSet.HasAny;

        if (!TryResolveDetailTextures(detailTexPath, out Texture2D southDetailTexture, out Texture2D eastDetailTexture, out Texture2D westDetailTexture))
        {
            _variantSetByKey[cacheKey] = new FaceDetailBoundaryVariantSet();
            return false;
        }

        if (!TryResolveHeadTextures(headType, out string graphicPath, out Texture2D southHeadTexture, out Texture2D eastHeadTexture, out Texture2D westHeadTexture))
        {
            _variantSetByKey[cacheKey] = new FaceDetailBoundaryVariantSet();
            return false;
        }

        variantSet = new FaceDetailBoundaryVariantSet
        {
            // South stays unguarded for now; the contour work matters much more on side views.
            SouthTexture = southDetailTexture,
        };

        if (eastDetailTexture != null
            && eastHeadTexture != null
            && TryGetOrAnalyzeHeadTexture(graphicPath, headType, eastHeadTexture, true, "east", out AutoEyePatchTextureAnalysis eastAnalysis)
            && TryClipDetailTexture(eastDetailTexture, eastHeadTexture, eastAnalysis, detailTexPath, headType, GetFacingHorizontalOffsetPixels(pawn, headType, eastHeadTexture, Rot4.East), "east", out Texture2D eastRuntimeTexture))
        {
            variantSet.EastTexture = eastRuntimeTexture;
        }
        else
        {
            variantSet.EastTexture = eastDetailTexture ?? southDetailTexture;
        }

        if (westDetailTexture != null
            && westHeadTexture != null
            && TryGetOrAnalyzeHeadTexture(graphicPath, headType, westHeadTexture, true, "west", out AutoEyePatchTextureAnalysis westAnalysis)
            && TryClipDetailTexture(westDetailTexture, westHeadTexture, westAnalysis, detailTexPath, headType, GetFacingHorizontalOffsetPixels(pawn, headType, westHeadTexture, Rot4.West), "west", out Texture2D westRuntimeTexture))
        {
            variantSet.WestTexture = westRuntimeTexture;
        }
        else
        {
            variantSet.WestTexture = westDetailTexture ?? southDetailTexture;
        }

        if (variantSet.SouthTexture == null && southDetailTexture == null)
            variantSet.SouthTexture = variantSet.EastTexture ?? variantSet.WestTexture;

        if (variantSet.EastTexture == null)
            variantSet.EastTexture = variantSet.SouthTexture ?? variantSet.WestTexture;

        if (variantSet.WestTexture == null)
            variantSet.WestTexture = variantSet.EastTexture ?? variantSet.SouthTexture;

        _variantSetByKey[cacheKey] = variantSet;
        return variantSet.HasAny;
    }

    private static bool TryResolveHeadTextures(HeadTypeDef headType, out string graphicPath, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture)
    {
        graphicPath = headType?.graphicPath;
        southTexture = null;
        eastTexture = null;
        westTexture = null;

        if (headType == null || graphicPath.NullOrEmpty())
            return false;

        TryResolveMultiFacingTextures(
            graphicPath,
            ShaderDatabase.CutoutSkin,
            out southTexture,
            out eastTexture,
            out westTexture,
            "FaceDetailBoundaryRuntime.TryResolveHeadTextures",
            headType?.defName);

        NormalizeSideFacingTextures(ref eastTexture, ref westTexture);
        return southTexture != null || eastTexture != null || westTexture != null;
    }

    private static bool TryResolveDetailTextures(string detailTexPath, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture)
    {
        southTexture = null;
        eastTexture = null;
        westTexture = null;

        if (detailTexPath.NullOrEmpty())
            return false;

        TryResolveMultiFacingTextures(
            detailTexPath,
            ShaderDatabase.Cutout,
            out southTexture,
            out eastTexture,
            out westTexture,
            "FaceDetailBoundaryRuntime.TryResolveDetailTextures",
            detailTexPath);

        NormalizeSideFacingTextures(ref eastTexture, ref westTexture);

        southTexture = FacePartTextureRuntime.PrepareTexture(southTexture);
        eastTexture = FacePartTextureRuntime.PrepareTexture(eastTexture);
        westTexture = FacePartTextureRuntime.PrepareTexture(westTexture);

        southTexture ??= eastTexture ?? westTexture;
        eastTexture ??= southTexture ?? westTexture;
        westTexture ??= eastTexture ?? southTexture;
        return southTexture != null || eastTexture != null || westTexture != null;
    }

    private static void NormalizeSideFacingTextures(ref Texture2D eastTexture, ref Texture2D westTexture)
    {
        // Graphic_Multi may hand back the east bitmap for west-facing materials and flip it at draw time.
        // Pixel-space clipping needs an actual mirrored bitmap instead of the shared source texture.
        if (eastTexture != null && westTexture != null)
        {
            if (ReferenceEquals(eastTexture, westTexture) || eastTexture.GetInstanceID() == westTexture.GetInstanceID())
                westTexture = GetOrCreateMirrored(eastTexture);

            return;
        }

        if (westTexture == null && eastTexture != null)
        {
            westTexture = GetOrCreateMirrored(eastTexture);
            return;
        }

        if (eastTexture == null && westTexture != null)
            eastTexture = GetOrCreateMirrored(westTexture);
    }

    private static void TryResolveMultiFacingTextures(string graphicPath, Shader shader, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture, string warnKeyStem, string debugName)
    {
        southTexture = null;
        eastTexture = null;
        westTexture = null;

        if (graphicPath.NullOrEmpty())
            return;

        try
        {
            Graphic graphic = GraphicDatabase.Get<Graphic_Multi>(graphicPath, shader, Vector2.one, Color.white);
            if (graphic != null)
            {
                southTexture = graphic.MatAt(Rot4.South)?.mainTexture as Texture2D;
                eastTexture = graphic.MatAt(Rot4.East)?.mainTexture as Texture2D;
                westTexture = graphic.MatAt(Rot4.West)?.mainTexture as Texture2D;
            }
        }
        catch (Exception ex)
        {
            string name = debugName ?? "<null>";
            string pathName = graphicPath ?? "<null>";
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                warnKeyStem + ".Graphic:" + name,
                $"Face detail boundary guard could not resolve GraphicDatabase textures for '{name}' at '{pathName}'.",
                ex);
        }

        try
        {
            southTexture ??= ContentFinder<Texture2D>.Get(graphicPath, false);
        }
        catch (Exception ex)
        {
            string name = debugName ?? "<null>";
            string pathName = graphicPath ?? "<null>";
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                warnKeyStem + ".ContentFinder:" + name,
                $"Face detail boundary guard could not resolve ContentFinder texture for '{name}' at '{pathName}'.",
                ex);
        }
    }

    private static float GetFacingHorizontalOffsetPixels(Pawn pawn, HeadTypeDef headType, Texture2D headTexture, Rot4 facing)
    {
        if (headTexture == null || facing == Rot4.South || facing == Rot4.North)
            return 0f;

        float eyeOffset = 0.13f;
        if (headType?.eyeOffsetEastWest.HasValue == true)
            eyeOffset = headType.eyeOffsetEastWest.Value.x;

        float eyeSizeFactor = pawn?.ageTracker?.CurLifeStage?.eyeSizeFactor ?? 1f;
        float worldOffset = eyeOffset * eyeSizeFactor * eyeSizeFactor;
        float signedWorldOffset = facing == Rot4.East ? worldOffset : facing == Rot4.West ? -worldOffset : 0f;
        float signedSideFacingTextureOffset = facing == Rot4.East
            ? SideFacingHalfHeadWidthOffsetFactor
            : facing == Rot4.West
                ? -SideFacingHalfHeadWidthOffsetFactor
                : 0f;

        return (signedWorldOffset + signedSideFacingTextureOffset) * headTexture.width;
    }

    private static bool TryGetOrAnalyzeHeadTexture(string graphicPath, HeadTypeDef headType, Texture2D headTexture, bool sideMode, string facingLabel, out AutoEyePatchTextureAnalysis analysis)
    {
        string headKey = headType?.defName ?? string.Empty;
        string cacheKey = string.Concat(graphicPath ?? string.Empty, "|", headKey, "|", facingLabel ?? (sideMode ? "side" : "south"), "|", headTexture != null ? headTexture.GetInstanceID().ToString() : "<null>");
        if (_headAnalysisByKey.TryGetValue(cacheKey, out analysis) && analysis != null)
            return analysis.SafeInteriorBounds.width > 0 && analysis.SafeInteriorBounds.height > 0;

        analysis = AutoEyePatchAnalyzer.Analyze(cacheKey, graphicPath, headTexture, sideMode);
        _headAnalysisByKey[cacheKey] = analysis;
        return analysis != null && analysis.SafeInteriorBounds.width > 0 && analysis.SafeInteriorBounds.height > 0 && headTexture != null;
    }

    private static bool TryClipDetailTexture(Texture2D detailTexture, Texture2D headTexture, AutoEyePatchTextureAnalysis headAnalysis, string detailTexPath, HeadTypeDef headType, float horizontalOffsetPixels, string facingLabel, out Texture2D runtimeTexture)
    {
        runtimeTexture = null;
        if (detailTexture == null || headTexture == null || headAnalysis == null)
            return false;

        if (!AutoEyePatchAnalyzer.TryReadPixels(detailTexture, out Color[] detailPixels))
            return false;

        if (!AutoEyePatchAnalyzer.TryReadPixels(headTexture, out Color[] headPixels))
            return false;

        int detailWidth = detailTexture.width;
        int detailHeight = detailTexture.height;
        int headWidth = Mathf.Max(1, headTexture.width);
        int headHeight = Mathf.Max(1, headTexture.height);
        if (headPixels.Length != headWidth * headHeight)
            return false;

        if (!TryBuildContourSafeMask(headPixels, headWidth, headHeight, headAnalysis, out bool[] allowedMask, out int[] allowedDistance))
            return false;

        if (!TryProjectContourDataToDetailSpace(
                detailWidth,
                detailHeight,
                headWidth,
                headHeight,
                headPixels,
                allowedMask,
                allowedDistance,
                0f,
                out bool[] projectedAllowedMask,
                out int[] projectedAllowedDistance,
                out float[] projectedSilhouetteMultiplier))
        {
            return false;
        }

        float detailTranslationPixels = headWidth > 0
            ? (horizontalOffsetPixels / headWidth) * detailWidth
            : 0f;

        int reference = Mathf.Max(1, Mathf.Min(headAnalysis.OpaqueBounds.width, headAnalysis.OpaqueBounds.height));
        float featherPx = Mathf.Clamp(Mathf.RoundToInt(reference * 0.030f), 1, 4);
        Color[] output = new Color[detailPixels.Length];
        bool wroteAny = false;

        for (int y = 0; y < detailHeight; y++)
        {
            for (int x = 0; x < detailWidth; x++)
            {
                int index = (y * detailWidth) + x;
                int sampleX = Mathf.FloorToInt(x - detailTranslationPixels);
                if (sampleX < 0 || sampleX >= detailWidth)
                {
                    output[index] = Color.clear;
                    continue;
                }

                Color src = detailPixels[(y * detailWidth) + sampleX];
                if (src.a <= 0.001f)
                {
                    output[index] = Color.clear;
                    continue;
                }

                if (!projectedAllowedMask[index])
                {
                    output[index] = Color.clear;
                    continue;
                }

                float contourMultiplier = ComputeDistanceFade(projectedAllowedDistance[index], featherPx);
                float silhouetteMultiplier = projectedSilhouetteMultiplier[index];
                float alphaMultiplier = contourMultiplier * silhouetteMultiplier;
                if (alphaMultiplier <= 0.001f)
                {
                    output[index] = Color.clear;
                    continue;
                }

                src.a *= alphaMultiplier;
                output[index] = src;
                wroteAny = true;
            }
        }

        if (!wroteAny)
            return false;

        runtimeTexture = new Texture2D(detailWidth, detailHeight, TextureFormat.RGBA32, false, false)
        {
            name = string.Concat(detailTexture.name, "_guard_", headType?.defName ?? "Head", "_", facingLabel),
        };
        FacePartTextureRuntime.FinalizeRuntimeTexture(runtimeTexture, output, detailWidth, detailHeight);
        return true;
    }

    private static bool TryProjectContourDataToDetailSpace(int detailWidth, int detailHeight, int headWidth, int headHeight, Color[] headPixels, bool[] allowedMask, int[] allowedDistance, float horizontalOffsetPixels, out bool[] projectedAllowedMask, out int[] projectedAllowedDistance, out float[] projectedSilhouetteMultiplier)
    {
        projectedAllowedMask = null;
        projectedAllowedDistance = null;
        projectedSilhouetteMultiplier = null;

        int detailPixelCount = detailWidth * detailHeight;
        if (detailWidth <= 0
            || detailHeight <= 0
            || headWidth <= 0
            || headHeight <= 0
            || headPixels == null
            || allowedMask == null
            || allowedDistance == null
            || headPixels.Length != headWidth * headHeight
            || allowedMask.Length != headPixels.Length
            || allowedDistance.Length != headPixels.Length)
        {
            return false;
        }

        projectedAllowedMask = new bool[detailPixelCount];
        projectedAllowedDistance = new int[detailPixelCount];
        projectedSilhouetteMultiplier = new float[detailPixelCount];

        for (int y = 0; y < detailHeight; y++)
        {
            float v = (y + 0.5f) / detailHeight;
            int headY = Mathf.Clamp(Mathf.FloorToInt(v * headHeight), 0, headHeight - 1);
            for (int x = 0; x < detailWidth; x++)
            {
                int index = (y * detailWidth) + x;
                float u = (x + 0.5f) / detailWidth;
                float shiftedHeadX = (u * headWidth) + horizontalOffsetPixels;
                if (shiftedHeadX < 0f || shiftedHeadX >= headWidth)
                    continue;

                int headX = Mathf.Clamp(Mathf.FloorToInt(shiftedHeadX), 0, headWidth - 1);
                int headIndex = (headY * headWidth) + headX;
                if (headIndex < 0 || headIndex >= headPixels.Length)
                    continue;

                projectedAllowedMask[index] = allowedMask[headIndex];
                projectedAllowedDistance[index] = allowedDistance[headIndex];
                projectedSilhouetteMultiplier[index] = Mathf.Clamp01((headPixels[headIndex].a - 0.01f) / 0.99f);
            }
        }

        return true;
    }

    private static bool TryBuildContourSafeMask(Color[] headPixels, int width, int height, AutoEyePatchTextureAnalysis headAnalysis, out bool[] allowedMask, out int[] allowedDistance)
    {
        allowedMask = null;
        allowedDistance = null;
        if (headPixels == null || headPixels.Length != width * height || headAnalysis == null)
            return false;

        int reference = Mathf.Max(1, Mathf.Min(headAnalysis.OpaqueBounds.width, headAnalysis.OpaqueBounds.height));
        int outlineThicknessPx = Mathf.Clamp(Mathf.RoundToInt(reference * 0.060f), 3, 8);
        int outlineSearchDepthPx = Mathf.Clamp(outlineThicknessPx + 4, outlineThicknessPx + 2, 14);
        int minimumInsetPx = Mathf.Clamp(Mathf.RoundToInt(reference * 0.035f), 1, 4);

        BuildContourProtectionData(headPixels, width, height, outlineThicknessPx, outlineSearchDepthPx, preferInsetContour: true, out bool[] opaqueMask, out int[] edgeDistance, out bool[] protectedOutlineMask);
        allowedMask = BuildAllowedMaskFromContour(opaqueMask, edgeDistance, protectedOutlineMask, minimumInsetPx);
        if (!HasAnyTrue(allowedMask))
            allowedMask = BuildFallbackAllowedMask(width, height, opaqueMask, headAnalysis.SafeInteriorBounds);

        if (!HasAnyTrue(allowedMask))
            return false;

        allowedDistance = BuildAllowedDistanceField(allowedMask, width, height);
        return allowedDistance != null && allowedDistance.Length == allowedMask.Length;
    }

    private static bool[] BuildAllowedMaskFromContour(bool[] opaqueMask, int[] edgeDistance, bool[] protectedOutlineMask, int minimumInsetPx)
    {
        if (opaqueMask == null || edgeDistance == null || protectedOutlineMask == null)
            return Array.Empty<bool>();

        bool[] allowedMask = new bool[opaqueMask.Length];
        for (int i = 0; i < allowedMask.Length; i++)
        {
            allowedMask[i] = opaqueMask[i]
                && !protectedOutlineMask[i]
                && edgeDistance[i] != int.MaxValue
                && edgeDistance[i] >= minimumInsetPx;
        }

        return allowedMask;
    }

    private static bool[] BuildFallbackAllowedMask(int width, int height, bool[] opaqueMask, RectInt safeBounds)
    {
        bool[] allowedMask = new bool[width * height];
        if (opaqueMask == null || opaqueMask.Length != allowedMask.Length || safeBounds.width <= 0 || safeBounds.height <= 0)
            return allowedMask;

        for (int y = safeBounds.yMin; y < safeBounds.yMax; y++)
        {
            for (int x = safeBounds.xMin; x < safeBounds.xMax; x++)
            {
                int index = (y * width) + x;
                if (index >= 0 && index < allowedMask.Length && opaqueMask[index])
                    allowedMask[index] = true;
            }
        }

        return allowedMask;
    }

    private static int[] BuildAllowedDistanceField(bool[] allowedMask, int width, int height)
    {
        int[] distance = new int[allowedMask.Length];
        Queue<int> frontier = new();
        bool anyBlocked = false;
        for (int i = 0; i < allowedMask.Length; i++)
        {
            if (allowedMask[i])
            {
                distance[i] = int.MaxValue;
                continue;
            }

            distance[i] = 0;
            frontier.Enqueue(i);
            anyBlocked = true;
        }

        if (!anyBlocked)
        {
            int fallbackDistance = Mathf.Max(width, height);
            for (int i = 0; i < distance.Length; i++)
                distance[i] = fallbackDistance;
            return distance;
        }

        while (frontier.Count > 0)
        {
            int current = frontier.Dequeue();
            int currentDistance = distance[current];
            int x = current % width;
            int y = current / width;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    int nextIndex = (ny * width) + nx;
                    if (!allowedMask[nextIndex])
                        continue;

                    int nextDistance = currentDistance + 1;
                    if (nextDistance >= distance[nextIndex])
                        continue;

                    distance[nextIndex] = nextDistance;
                    frontier.Enqueue(nextIndex);
                }
            }
        }

        return distance;
    }

    private static float ComputeDistanceFade(int allowedDistance, float featherPx)
    {
        if (allowedDistance == int.MaxValue)
            return 1f;

        return allowedDistance >= featherPx
            ? 1f
            : Mathf.Clamp01((allowedDistance - 0.25f) / Mathf.Max(1f, featherPx));
    }

    private static bool HasAnyTrue(bool[] values)
    {
        if (values == null)
            return false;

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i])
                return true;
        }

        return false;
    }

    private static void BuildContourProtectionData(Color[] contourPixels, int width, int height, int outlineThicknessPx, int outlineSearchDepthPx, bool preferInsetContour, out bool[] opaqueMask, out int[] edgeDistance, out bool[] protectedOutlineMask)
    {
        opaqueMask = new bool[width * height];
        edgeDistance = new int[width * height];
        BuildOpaqueDistanceField(contourPixels, width, height, opaqueMask, edgeDistance);

        protectedOutlineMask = new bool[width * height];
        if (preferInsetContour && TryBuildEdgeAnchoredContourMask(contourPixels, opaqueMask, edgeDistance, width, height, outlineThicknessPx, outlineSearchDepthPx, protectedOutlineMask))
            return;

        Queue<int> frontier = new();
        float seedDarkThreshold = preferInsetContour ? 0.28f : 0.22f;
        float growDarkThreshold = preferInsetContour ? 0.46f : 0.34f;
        float seedContrastThreshold = preferInsetContour ? 0.07f : 0.10f;
        int maxSeedDistance = preferInsetContour ? Mathf.Max(outlineThicknessPx + 2, outlineSearchDepthPx) : Mathf.Min(1, outlineSearchDepthPx);

        for (int i = 0; i < contourPixels.Length; i++)
        {
            if (!opaqueMask[i])
                continue;

            int distance = edgeDistance[i];
            if (distance == int.MaxValue || distance > maxSeedDistance)
                continue;

            float luma = ComputeLuma(contourPixels[i]);
            if (luma > seedDarkThreshold)
                continue;

            int x = i % width;
            int y = i / width;
            bool allowSeed = preferInsetContour
                ? IsLikelyContourPixel(contourPixels, opaqueMask, width, height, x, y, luma, seedContrastThreshold)
                : (distance == 0 || HasLighterNeighbor(contourPixels, opaqueMask, width, height, x, y, luma + 0.10f));
            if (!allowSeed)
                continue;

            protectedOutlineMask[i] = true;
            frontier.Enqueue(i);
        }

        while (frontier.Count > 0)
        {
            int current = frontier.Dequeue();
            int x = current % width;
            int y = current / width;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    int nextIndex = (ny * width) + nx;
                    if (protectedOutlineMask[nextIndex] || !opaqueMask[nextIndex])
                        continue;

                    int distance = edgeDistance[nextIndex];
                    if (distance == int.MaxValue || distance > outlineSearchDepthPx)
                        continue;

                    float luma = ComputeLuma(contourPixels[nextIndex]);
                    if (luma > growDarkThreshold)
                        continue;

                    bool hasProtectedNeighbor = false;
                    for (int oy = -1; oy <= 1 && !hasProtectedNeighbor; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            if (ox == 0 && oy == 0)
                                continue;

                            int px = nx + ox;
                            int py = ny + oy;
                            if (px < 0 || px >= width || py < 0 || py >= height)
                                continue;

                            if (protectedOutlineMask[(py * width) + px])
                            {
                                hasProtectedNeighbor = true;
                                break;
                            }
                        }
                    }

                    if (!hasProtectedNeighbor)
                        continue;

                    bool keep = preferInsetContour
                        ? IsLikelyContourPixel(contourPixels, opaqueMask, width, height, nx, ny, luma, 0.05f) || luma <= seedDarkThreshold + 0.04f
                        : (luma <= seedDarkThreshold || HasLighterNeighbor(contourPixels, opaqueMask, width, height, nx, ny, luma + 0.10f));
                    if (!keep)
                        continue;

                    protectedOutlineMask[nextIndex] = true;
                    frontier.Enqueue(nextIndex);
                }
            }
        }

        if (preferInsetContour)
            ExpandProtectedStrokeThickness(contourPixels, opaqueMask, edgeDistance, width, height, outlineSearchDepthPx, protectedOutlineMask, outlineThicknessPx);

        int dilationSteps = preferInsetContour
            ? Mathf.Clamp(Mathf.RoundToInt(outlineThicknessPx * 0.25f), 1, 2)
            : Mathf.Clamp(Mathf.RoundToInt(outlineThicknessPx * 0.20f), 0, 1);
        for (int step = 0; step < dilationSteps; step++)
        {
            bool[] expanded = (bool[])protectedOutlineMask.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    if (!opaqueMask[index] || protectedOutlineMask[index])
                        continue;

                    int distance = edgeDistance[index];
                    if (distance == int.MaxValue || distance > outlineSearchDepthPx + dilationSteps)
                        continue;

                    bool keep = preferInsetContour
                        ? IsLikelyContourPixel(contourPixels, opaqueMask, width, height, x, y, ComputeLuma(contourPixels[index]), 0.03f)
                        : distance <= Mathf.Max(1, outlineThicknessPx);
                    if (!keep)
                        continue;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        bool found = false;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                                continue;

                            if (protectedOutlineMask[(ny * width) + nx])
                            {
                                expanded[index] = true;
                                found = true;
                                break;
                            }
                        }

                        if (found)
                            break;
                    }
                }
            }

            protectedOutlineMask = expanded;
        }
    }

    private static bool TryBuildEdgeAnchoredContourMask(Color[] contourPixels, bool[] opaqueMask, int[] edgeDistance, int width, int height, int outlineThicknessPx, int outlineSearchDepthPx, bool[] protectedOutlineMask)
    {
        Array.Clear(protectedOutlineMask, 0, protectedOutlineMask.Length);

        List<int> boundary = new();
        float globalInteriorLuma = 0f;
        int globalInteriorCount = 0;
        for (int i = 0; i < opaqueMask.Length; i++)
        {
            if (!opaqueMask[i])
                continue;

            if (edgeDistance[i] > Mathf.Max(1, outlineThicknessPx + 1) && edgeDistance[i] != int.MaxValue)
            {
                globalInteriorLuma += ComputeLuma(contourPixels[i]);
                globalInteriorCount++;
            }

            int x = i % width;
            int y = i / width;
            if (IsSilhouetteEdge(opaqueMask, width, height, x, y))
                boundary.Add(i);
        }

        if (boundary.Count == 0)
            return false;

        float fallbackInteriorLuma = globalInteriorCount > 0 ? (globalInteriorLuma / globalInteriorCount) : 0.65f;
        Queue<int> frontier = new();

        for (int i = 0; i < boundary.Count; i++)
        {
            int boundaryIndex = boundary[i];
            int boundaryX = boundaryIndex % width;
            int boundaryY = boundaryIndex / width;

            if (!TryGetInwardStep(opaqueMask, width, height, boundaryX, boundaryY, out int stepX, out int stepY))
                continue;

            float interiorBaseline = SampleRayInteriorBaseline(contourPixels, opaqueMask, edgeDistance, width, height, boundaryX, boundaryY, stepX, stepY, outlineThicknessPx, outlineSearchDepthPx, fallbackInteriorLuma);
            int bestIndex = -1;
            float bestScore = 0f;

            for (int step = 1; step <= outlineSearchDepthPx; step++)
            {
                int sx = boundaryX + (stepX * step);
                int sy = boundaryY + (stepY * step);
                if (sx < 0 || sx >= width || sy < 0 || sy >= height)
                    break;

                int sampleIndex = (sy * width) + sx;
                if (!opaqueMask[sampleIndex])
                    break;

                int distance = edgeDistance[sampleIndex];
                if (distance == int.MaxValue || distance > outlineSearchDepthPx + 1)
                    continue;

                float luma = ComputeLuma(contourPixels[sampleIndex]);
                float contrast = interiorBaseline - luma;
                if (contrast < 0.035f)
                    continue;

                if (distance > outlineThicknessPx + 2)
                    continue;

                float closeness = 1f - Mathf.Clamp01((distance - 1f) / Mathf.Max(1f, outlineThicknessPx + 1f));
                float darkness = Mathf.Clamp01((contrast - 0.02f) / 0.25f);
                float score = (darkness * 0.7f) + (closeness * 0.3f);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestIndex = sampleIndex;
            }

            if (bestIndex < 0 || protectedOutlineMask[bestIndex])
                continue;

            protectedOutlineMask[bestIndex] = true;
            frontier.Enqueue(bestIndex);
        }

        if (frontier.Count == 0)
            return false;

        int growMaxDistance = Mathf.Clamp(outlineSearchDepthPx + Mathf.RoundToInt(outlineThicknessPx * 0.5f), outlineSearchDepthPx, outlineSearchDepthPx + 4);
        while (frontier.Count > 0)
        {
            int current = frontier.Dequeue();
            int x = current % width;
            int y = current / width;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    int nextIndex = (ny * width) + nx;
                    if (protectedOutlineMask[nextIndex] || !opaqueMask[nextIndex])
                        continue;

                    int distance = edgeDistance[nextIndex];
                    if (distance == int.MaxValue || distance > growMaxDistance)
                        continue;

                    float baseline = SampleLocalInteriorBaseline(contourPixels, opaqueMask, edgeDistance, width, height, nx, ny, outlineThicknessPx, fallbackInteriorLuma);
                    float luma = ComputeLuma(contourPixels[nextIndex]);
                    bool keep = distance <= outlineThicknessPx + 1
                        && (luma <= 0.50f || luma <= (baseline - 0.025f));
                    if (!keep)
                        continue;

                    protectedOutlineMask[nextIndex] = true;
                    frontier.Enqueue(nextIndex);
                }
            }
        }

        int dilationSteps = Mathf.Clamp(Mathf.RoundToInt(outlineThicknessPx * 0.25f), 1, 2);
        for (int step = 0; step < dilationSteps; step++)
        {
            bool[] expanded = (bool[])protectedOutlineMask.Clone();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    if (!opaqueMask[index] || protectedOutlineMask[index])
                        continue;

                    int distance = edgeDistance[index];
                    if (distance == int.MaxValue || distance > outlineThicknessPx + 2)
                        continue;

                    bool touchesProtected = false;
                    for (int dy = -1; dy <= 1 && !touchesProtected; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                                continue;

                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                                continue;

                            if (protectedOutlineMask[(ny * width) + nx])
                            {
                                touchesProtected = true;
                                break;
                            }
                        }
                    }

                    if (!touchesProtected)
                        continue;

                    float baseline = SampleLocalInteriorBaseline(contourPixels, opaqueMask, edgeDistance, width, height, x, y, outlineThicknessPx, fallbackInteriorLuma);
                    float luma = ComputeLuma(contourPixels[index]);
                    if (luma <= 0.56f || luma <= (baseline - 0.015f))
                        expanded[index] = true;
                }
            }

            Array.Copy(expanded, protectedOutlineMask, protectedOutlineMask.Length);
        }

        return HasAnyTrue(protectedOutlineMask);
    }

    private static bool TryGetInwardStep(bool[] opaqueMask, int width, int height, int x, int y, out int stepX, out int stepY)
    {
        stepX = 0;
        stepY = 0;
        int bestScore = -1;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                int neighborIndex = (ny * width) + nx;
                if (!opaqueMask[neighborIndex])
                    continue;

                int score = CountOpaqueNeighbors(opaqueMask, width, height, nx, ny);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                stepX = dx;
                stepY = dy;
            }
        }

        return bestScore >= 0;
    }

    private static int CountOpaqueNeighbors(bool[] opaqueMask, int width, int height, int x, int y)
    {
        int count = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                if (opaqueMask[(ny * width) + nx])
                    count++;
            }
        }

        return count;
    }

    private static float SampleRayInteriorBaseline(Color[] contourPixels, bool[] opaqueMask, int[] edgeDistance, int width, int height, int x, int y, int stepX, int stepY, int outlineThicknessPx, int outlineSearchDepthPx, float fallback)
    {
        float sum = 0f;
        int count = 0;
        int startStep = Mathf.Max(2, outlineThicknessPx + 1);
        int endStep = Mathf.Max(startStep, outlineSearchDepthPx + outlineThicknessPx + 1);

        for (int step = startStep; step <= endStep; step++)
        {
            int sx = x + (stepX * step);
            int sy = y + (stepY * step);
            if (sx < 0 || sx >= width || sy < 0 || sy >= height)
                break;

            int index = (sy * width) + sx;
            if (!opaqueMask[index])
                break;

            if (edgeDistance[index] <= outlineThicknessPx + 1)
                continue;

            sum += ComputeLuma(contourPixels[index]);
            count++;
        }

        return count > 0 ? (sum / count) : fallback;
    }

    private static float SampleLocalInteriorBaseline(Color[] contourPixels, bool[] opaqueMask, int[] edgeDistance, int width, int height, int x, int y, int outlineThicknessPx, float fallback)
    {
        float sum = 0f;
        int count = 0;
        for (int oy = -2; oy <= 2; oy++)
        {
            int sy = y + oy;
            if (sy < 0 || sy >= height)
                continue;

            for (int ox = -2; ox <= 2; ox++)
            {
                int sx = x + ox;
                if (sx < 0 || sx >= width)
                    continue;

                int index = (sy * width) + sx;
                if (!opaqueMask[index])
                    continue;

                if (edgeDistance[index] <= outlineThicknessPx + 1)
                    continue;

                sum += ComputeLuma(contourPixels[index]);
                count++;
            }
        }

        return count > 0 ? (sum / count) : fallback;
    }

    private static void ExpandProtectedStrokeThickness(Color[] pixels, bool[] opaqueMask, int[] edgeDistance, int width, int height, int outlineSearchDepthPx, bool[] protectedOutlineMask, int outlineThicknessPx)
    {
        int maxFloodSteps = Mathf.Clamp(Mathf.RoundToInt(outlineThicknessPx * 0.75f), 1, 4);
        int maxDistance = Mathf.Clamp(outlineSearchDepthPx + maxFloodSteps + 1, outlineSearchDepthPx, outlineSearchDepthPx + 5);
        float floodDarkThreshold = 0.52f;
        Queue<int> frontier = new();
        int[] floodSteps = new int[protectedOutlineMask.Length];
        Array.Fill(floodSteps, -1);

        for (int i = 0; i < protectedOutlineMask.Length; i++)
        {
            if (!protectedOutlineMask[i])
                continue;

            floodSteps[i] = 0;
            frontier.Enqueue(i);
        }

        while (frontier.Count > 0)
        {
            int current = frontier.Dequeue();
            int nextStep = floodSteps[current] + 1;
            if (nextStep > maxFloodSteps)
                continue;

            int x = current % width;
            int y = current / width;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    int nextIndex = (ny * width) + nx;
                    if (floodSteps[nextIndex] >= 0 || !opaqueMask[nextIndex])
                        continue;

                    int distance = edgeDistance[nextIndex];
                    if (distance == int.MaxValue || distance > maxDistance)
                        continue;

                    float luma = ComputeLuma(pixels[nextIndex]);
                    if (luma > floodDarkThreshold)
                        continue;

                    int darkNeighbors = CountNearbyDarkPixels(pixels, opaqueMask, width, height, nx, ny, floodDarkThreshold + 0.04f);
                    bool hasProtectedNeighbor = false;
                    for (int oy = -1; oy <= 1 && !hasProtectedNeighbor; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            if (ox == 0 && oy == 0)
                                continue;

                            int px = nx + ox;
                            int py = ny + oy;
                            if (px < 0 || px >= width || py < 0 || py >= height)
                                continue;

                            if (protectedOutlineMask[(py * width) + px])
                            {
                                hasProtectedNeighbor = true;
                                break;
                            }
                        }
                    }

                    if (!hasProtectedNeighbor || darkNeighbors < 2)
                        continue;

                    protectedOutlineMask[nextIndex] = true;
                    floodSteps[nextIndex] = nextStep;
                    frontier.Enqueue(nextIndex);
                }
            }
        }
    }

    private static int CountNearbyDarkPixels(Color[] pixels, bool[] opaqueMask, int width, int height, int x, int y, float darkThreshold)
    {
        int count = 0;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    continue;

                int index = (ny * width) + nx;
                if (!opaqueMask[index])
                    continue;

                if (ComputeLuma(pixels[index]) <= darkThreshold)
                    count++;
            }
        }

        return count;
    }

    private static bool IsLikelyContourPixel(Color[] pixels, bool[] opaqueMask, int width, int height, int x, int y, float luma, float contrastThreshold)
    {
        int lighterNeighbors = 0;
        bool touchesBoundary = false;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                {
                    touchesBoundary = true;
                    continue;
                }

                int index = (ny * width) + nx;
                if (!opaqueMask[index])
                {
                    touchesBoundary = true;
                    continue;
                }

                if (ComputeLuma(pixels[index]) >= luma + contrastThreshold)
                    lighterNeighbors++;
            }
        }

        return touchesBoundary || lighterNeighbors >= 2;
    }

    private static bool HasLighterNeighbor(Color[] pixels, bool[] opaqueMask, int width, int height, int x, int y, float lighterThan)
    {
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    return true;

                int index = (ny * width) + nx;
                if (!opaqueMask[index])
                    return true;

                if (ComputeLuma(pixels[index]) >= lighterThan)
                    return true;
            }
        }

        return false;
    }

    private static void BuildOpaqueDistanceField(Color[] contourPixels, int width, int height, bool[] opaqueMask, int[] edgeDistance)
    {
        Queue<int> frontier = new();
        for (int i = 0; i < edgeDistance.Length; i++)
        {
            edgeDistance[i] = int.MaxValue;
            opaqueMask[i] = contourPixels[i].a > 0.10f;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                if (!opaqueMask[index])
                    continue;

                if (IsSilhouetteEdge(opaqueMask, width, height, x, y))
                {
                    edgeDistance[index] = 0;
                    frontier.Enqueue(index);
                }
            }
        }

        while (frontier.Count > 0)
        {
            int current = frontier.Dequeue();
            int currentDistance = edgeDistance[current];
            int x = current % width;
            int y = current / width;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0)
                        continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    int nextIndex = (ny * width) + nx;
                    if (!opaqueMask[nextIndex])
                        continue;

                    int nextDistance = currentDistance + 1;
                    if (nextDistance >= edgeDistance[nextIndex])
                        continue;

                    edgeDistance[nextIndex] = nextDistance;
                    frontier.Enqueue(nextIndex);
                }
            }
        }
    }

    private static bool IsSilhouetteEdge(bool[] opaqueMask, int width, int height, int x, int y)
    {
        int index = (y * width) + x;
        if (!opaqueMask[index])
            return false;

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                int nx = x + dx;
                int ny = y + dy;
                if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                    return true;

                if (!opaqueMask[(ny * width) + nx])
                    return true;
            }
        }

        return false;
    }

    private static float ComputeLuma(Color c) => (0.2126f * c.r) + (0.7152f * c.g) + (0.0722f * c.b);

    private static Texture2D GetOrCreateMirrored(Texture2D source)
    {
        if (source == null)
            return null;

        if (_mirroredRuntimeTexturesBySource.TryGetValue(source, out Texture2D cached) && cached != null)
            return cached;

        int width = source.width;
        int height = source.height;
        if (!AutoEyePatchAnalyzer.TryReadPixels(source, out Color[] src) || src == null || src.Length != width * height)
            return source;

        Texture2D flipped = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = source.name + "_flipX",
            filterMode = source.filterMode,
            wrapMode = source.wrapMode,
            anisoLevel = source.anisoLevel,
        };

        Color[] dst = new Color[src.Length];
        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
                dst[row + x] = src[row + (width - 1 - x)];
        }

        FacePartTextureRuntime.FinalizeRuntimeTexture(flipped, dst, width, height);
        _mirroredRuntimeTexturesBySource[source] = flipped;
        return flipped;
    }

    private static void DestroyRuntimeTexture(Texture2D texture, HashSet<Texture2D> destroyed)
    {
        if (texture == null || destroyed == null || !destroyed.Add(texture) || !IsOwnedRuntimeTexture(texture))
            return;

        UnityEngine.Object.Destroy(texture);
    }

    private static bool IsOwnedRuntimeTexture(Texture2D texture)
    {
        if (texture == null)
            return false;

        string name = texture.name ?? string.Empty;
        return name.IndexOf("_guard_", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("_flipX", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
