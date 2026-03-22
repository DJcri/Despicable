using System;
using System.Collections.Generic;
using System.Threading;
using RimWorld;
using UnityEngine;
using Verse;

// Guardrail-Reason: Auto eye patch generation stays centralized because asset discovery, rule expansion, and output assembly share one fragile pipeline.
namespace Despicable;

internal static class AutoEyePatchGenerator
{
    private const float CamouflageDonorLumaThreshold = 0.50f;

    [ThreadStatic] private static Queue<int> _scratchIndexQueue;
    [ThreadStatic] private static List<int> _scratchIntListA;
    [ThreadStatic] private static List<int> _scratchIntListB;

    private static bool ShouldEmitVerboseDiagnostics => Prefs.DevMode;

    private static Queue<int> GetScratchIndexQueue()
    {
        _scratchIndexQueue ??= new Queue<int>();
        _scratchIndexQueue.Clear();
        return _scratchIndexQueue;
    }

    private static List<int> GetScratchIntListA()
    {
        _scratchIntListA ??= new List<int>();
        _scratchIntListA.Clear();
        return _scratchIntListA;
    }

    private static List<int> GetScratchIntListB()
    {
        _scratchIntListB ??= new List<int>();
        _scratchIntListB.Clear();
        return _scratchIntListB;
    }

    public static AutoEyePatchHeadResult Generate(HeadTypeDef headType, AutoEyePatchEligibility.Result eligibility, AutoEyePatchTextureAnalysis southAnalysis, AutoEyePatchTextureAnalysis eastAnalysis)
    {
        AutoEyePatchHeadResult result = new()
        {
            HeadKey = eligibility?.HeadKey ?? headType?.defName ?? string.Empty,
            GraphicPath = eligibility?.GraphicPath ?? headType?.graphicPath ?? string.Empty,
            Version = AutoEyePatchRuntime.GenerationVersion,
            North = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.North, Status = AutoEyePatchVariantStatus.None },
        };

        if (headType == null || eligibility == null || !eligibility.Eligible)
        {
            result.Status = AutoEyePatchHeadStatus.Skipped;
            result.SummaryReasons = eligibility?.Reasons ?? AutoEyePatchSkipReason.HeadIneligible;
            result.South = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.South, Status = AutoEyePatchVariantStatus.Skipped, Reasons = result.SummaryReasons };
            result.East = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.East, Status = AutoEyePatchVariantStatus.Skipped, Reasons = result.SummaryReasons };
            result.West = new AutoEyePatchVariantResult { Slot = AutoEyePatchFacingSlot.West, Status = AutoEyePatchVariantStatus.Skipped, Reasons = result.SummaryReasons };
            return result;
        }

        result.South = BuildSouthVariant(eligibility.SouthTexture, southAnalysis);
        result.East = BuildEastDirectVariant(eligibility.EastTexture, eastAnalysis);
        result.West = BuildMirroredWestVariant(result.East);
        result.Status = ResolveHeadStatus(result.South, result.East, result.West);
        result.ReplacesLegacyEyeBase = result.South.Status == AutoEyePatchVariantStatus.Generated
            || result.East.Status == AutoEyePatchVariantStatus.Generated
            || result.West.Status == AutoEyePatchVariantStatus.Generated;
        result.Confidence = ResolveHeadConfidence(result.South, result.East, result.West);
        result.SummaryReasons = result.South.Reasons | result.East.Reasons | result.West.Reasons;
        return result;
    }

    private static AutoEyePatchVariantResult BuildSouthVariant(Texture2D texture, AutoEyePatchTextureAnalysis analysis)
    {
        AutoEyePatchVariantResult variant = new() { Slot = AutoEyePatchFacingSlot.South };
        if (texture == null || analysis == null)
        {
            variant.Status = AutoEyePatchVariantStatus.Skipped;
            variant.Reasons = AutoEyePatchSkipReason.TextureMissing;
            return variant;
        }

        AutoEyePatchDarkCandidate leftCandidate = null;
        AutoEyePatchDarkCandidate rightCandidate = null;
        AutoEyePatchDarkCandidate fallbackCandidate = null;
        float bestLeftScore = float.MinValue;
        float bestRightScore = float.MinValue;
        float bestFallbackScore = float.MinValue;

        List<AutoEyePatchDarkCandidate> candidates = analysis.Candidates;
        for (int i = 0; i < candidates.Count; i++)
        {
            AutoEyePatchDarkCandidate candidate = candidates[i];
            if (candidate == null)
                continue;

            float score = ScoreCandidate(candidate);
            if (score > bestFallbackScore)
            {
                bestFallbackScore = score;
                fallbackCandidate = candidate;
            }

            if (candidate.CenterUV.x <= 0.5f)
            {
                if (score > bestLeftScore)
                {
                    bestLeftScore = score;
                    leftCandidate = candidate;
                }
            }
            else if (score > bestRightScore)
            {
                bestRightScore = score;
                rightCandidate = candidate;
            }
        }

        bool havePair = leftCandidate != null && rightCandidate != null;
        if (havePair)
        {
            bool leftOk = TryBuildDescriptor(texture, analysis, leftCandidate, out AutoEyePatchDescriptor left, out AutoEyePatchSkipReason leftReasons);
            bool rightOk = TryBuildDescriptor(texture, analysis, rightCandidate, out AutoEyePatchDescriptor right, out AutoEyePatchSkipReason rightReasons);
            if (leftOk && rightOk)
            {
                variant.Status = AutoEyePatchVariantStatus.Generated;
                variant.Primary = left;
                variant.Secondary = right;
                variant.Reasons = AutoEyePatchSkipReason.None;
                return variant;
            }
        }

        if (fallbackCandidate == null)
        {
            variant.Status = AutoEyePatchVariantStatus.Skipped;
            variant.Reasons = analysis.AnalysisReasons | AutoEyePatchSkipReason.NoDarkCandidate;
            return variant;
        }

        if (!TryBuildDescriptor(texture, analysis, fallbackCandidate, out AutoEyePatchDescriptor fallback, out AutoEyePatchSkipReason fallbackReasons))
        {
            variant.Status = AutoEyePatchVariantStatus.Skipped;
            variant.Reasons = analysis.AnalysisReasons | fallbackReasons | AutoEyePatchSkipReason.PairInvalid;
            return variant;
        }

        variant.Status = AutoEyePatchVariantStatus.Generated;
        variant.Primary = fallback;
        variant.Reasons = AutoEyePatchSkipReason.None;
        return variant;
    }

    private static AutoEyePatchVariantResult BuildEastDirectVariant(Texture2D eastTexture, AutoEyePatchTextureAnalysis eastAnalysis)
    {
        AutoEyePatchVariantResult variant = new() { Slot = AutoEyePatchFacingSlot.East };
        string eastName = eastTexture != null ? (eastTexture.name ?? "<unnamed>") : "<null>";
        string reason;

        if (eastTexture == null || eastAnalysis == null)
        {
            variant.Status = AutoEyePatchVariantStatus.Skipped;
            variant.Reasons = AutoEyePatchSkipReason.TextureMissing;
            reason = eastTexture == null ? "east_texture_null" : "east_analysis_null";
            if (ShouldEmitVerboseDiagnostics)
                Log.Message($"[Despicable] EAST-GATE-TRACE texture={eastName} eastTex={(eastTexture != null)} eastAnalysis={(eastAnalysis != null)} result=skipped reason={reason} reasons={variant.Reasons}");
            return variant;
        }

        AutoEyePatchDarkCandidate candidate = null;
        float bestCandidateScore = float.MinValue;
        List<AutoEyePatchDarkCandidate> eastCandidates = eastAnalysis.Candidates;
        for (int i = 0; i < eastCandidates.Count; i++)
        {
            AutoEyePatchDarkCandidate next = eastCandidates[i];
            if (next == null)
                continue;

            float score = ScoreCandidate(next);
            if (score > bestCandidateScore)
            {
                bestCandidateScore = score;
                candidate = next;
            }
        }
        if (candidate == null)
        {
            variant.Status = AutoEyePatchVariantStatus.Skipped;
            variant.Reasons = eastAnalysis.AnalysisReasons | AutoEyePatchSkipReason.NoDarkCandidate;
            reason = "east_no_candidate";
            if (ShouldEmitVerboseDiagnostics)
                Log.Message($"[Despicable] EAST-GATE-TRACE texture={eastName} eastTex={(eastTexture != null)} eastAnalysis={(eastAnalysis != null)} result=skipped reason={reason} reasons={variant.Reasons}");
            return variant;
        }

        if (!TryBuildDescriptor(eastTexture, eastAnalysis, candidate, out AutoEyePatchDescriptor direct, out AutoEyePatchSkipReason buildReasons))
        {
            variant.Status = AutoEyePatchVariantStatus.Skipped;
            variant.Reasons = eastAnalysis.AnalysisReasons | buildReasons;
            reason = "east_direct_failed";
            if (ShouldEmitVerboseDiagnostics)
                Log.Message($"[Despicable] EAST-GATE-TRACE texture={eastName} eastTex={(eastTexture != null)} eastAnalysis={(eastAnalysis != null)} result=skipped reason={reason} reasons={variant.Reasons}");
            return variant;
        }

        variant.Status = AutoEyePatchVariantStatus.Generated;
        variant.Primary = direct;
        variant.Reasons = AutoEyePatchSkipReason.None;
        reason = "east_direct_success";
        if (ShouldEmitVerboseDiagnostics)
            Log.Message($"[Despicable] EAST-GATE-TRACE texture={eastName} eastTex={(eastTexture != null)} eastAnalysis={(eastAnalysis != null)} result=generated reason={reason} reasons={variant.Reasons}");
        return variant;
    }

    private static AutoEyePatchVariantResult BuildMirroredWestVariant(AutoEyePatchVariantResult eastVariant)
    {
        AutoEyePatchVariantResult variant = new() { Slot = AutoEyePatchFacingSlot.West };
        if (eastVariant == null)
        {
            variant.Status = AutoEyePatchVariantStatus.Skipped;
            variant.Reasons = AutoEyePatchSkipReason.TextureMissing;
            return variant;
        }

        variant.Status = eastVariant.Status;
        variant.Primary = eastVariant.Primary;
        variant.Secondary = eastVariant.Secondary;
        variant.Reasons = eastVariant.Reasons;
        return variant;
    }

    private static float ScoreCandidate(AutoEyePatchDarkCandidate candidate) => candidate.DarknessScore * candidate.CompactnessScore * candidate.OutlineSafetyScore;

    private static bool TryBuildSideDirectFromSouthMask(AutoEyePatchDescriptor southDescriptor, Texture2D eastTexture, AutoEyePatchTextureAnalysis eastAnalysis, out AutoEyePatchDescriptor descriptor)
    {
        descriptor = null;
        if (southDescriptor?.RuntimeTexture == null || eastTexture == null || eastAnalysis == null)
            return false;

        if (!TryBuildCandidateFromRuntimeMask(southDescriptor.RuntimeTexture, out AutoEyePatchDarkCandidate candidate))
            return false;

        if (!TryBuildFootprintOverlayTexture(eastTexture, eastAnalysis.OpaqueBounds, candidate, southDescriptor.FillColor, targetCenterOverride: null, allowAdaptiveInflation: false, out Texture2D runtimeTexture, out RectInt finalBoundsPx, out float featherPx, out Color[] runtimePixels))
            return false;

        Color[] contourPixels = null;
        string preForensics = null;
        if (AutoEyePatchAnalyzer.TryReadPixels(eastTexture, out contourPixels))
            preForensics = BuildEastOverlayForensics("pre", runtimePixels, eastTexture.width, eastTexture.height, contourPixels, eastAnalysis.OpaqueBounds);
        else
            preForensics = BuildEastOverlayForensics("pre", runtimeTexture, eastTexture, eastAnalysis.OpaqueBounds);

        bool cullSucceeded = TryApplyContourSubtractionToOverlay(runtimeTexture, eastTexture, eastAnalysis, southDescriptor.FillColor, out Texture2D clippedTexture, out RectInt clippedBoundsPx, out Color[] clippedPixels, contourPixels);
        string postForensics = cullSucceeded
            ? (contourPixels != null
                ? BuildEastOverlayForensics("post", clippedPixels, eastTexture.width, eastTexture.height, contourPixels, eastAnalysis.OpaqueBounds)
                : BuildEastOverlayForensics("post", clippedTexture, eastTexture, eastAnalysis.OpaqueBounds))
            : "post[late_cull_failed]";
        if (ShouldEmitVerboseDiagnostics)
            Log.Message($"[Despicable] EAST-MASK-FORENSICS texture={eastTexture.name ?? "<unnamed>"} result={(cullSucceeded ? "ok" : "late_cull_failed")} {preForensics} {postForensics}");

        if (!cullSucceeded)
        {
            UnityEngine.Object.Destroy(runtimeTexture);
            return false;
        }

        descriptor = CloneDescriptorWithRuntime(southDescriptor, clippedTexture, clippedBoundsPx, preserveTemplateGeometry: true);
        if (descriptor == null)
        {
            UnityEngine.Object.Destroy(clippedTexture);
            return false;
        }

        descriptor.FeatherUV = 0f;
        return true;
    }

    private static bool TryBuildCandidateFromRuntimeMask(Texture2D runtimeMask, out AutoEyePatchDarkCandidate candidate)
    {
        candidate = null;
        if (runtimeMask == null)
            return false;

        if (!AutoEyePatchAnalyzer.TryReadPixels(runtimeMask, out Color[] pixels))
            return false;

        return TryBuildCandidateFromRuntimeMaskPixels(pixels, runtimeMask.width, runtimeMask.height, out candidate);
    }

    private static bool TryBuildCandidateFromRuntimeMaskPixels(Color[] pixels, int width, int height, out AutoEyePatchDarkCandidate candidate)
    {
        candidate = null;
        if (pixels == null || pixels.Length != width * height || width <= 0 || height <= 0)
            return false;

        List<Vector2Int> footprint = new();
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[(y * width) + x].a <= 0.001f)
                    continue;

                footprint.Add(new Vector2Int(x, y));
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (footprint.Count == 0)
            return false;

        RectInt bounds = new(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        int croppedWidth = bounds.width;
        int croppedHeight = bounds.height;
        float[] croppedAlpha = new float[croppedWidth * croppedHeight];
        for (int y = 0; y < croppedHeight; y++)
        {
            int sourceRow = (bounds.yMin + y) * width;
            int targetRow = y * croppedWidth;
            for (int x = 0; x < croppedWidth; x++)
                croppedAlpha[targetRow + x] = pixels[sourceRow + bounds.xMin + x].a;
        }

        candidate = new AutoEyePatchDarkCandidate
        {
            BoundsPx = bounds,
            CenterUV = new Vector2(bounds.center.x / (float)width, bounds.center.y / (float)height),
            RadiusUV = new Vector2(Mathf.Max(1f, bounds.width * 0.5f) / width, Mathf.Max(1f, bounds.height * 0.5f) / height),
            DarknessScore = 1f,
            CompactnessScore = 1f,
            OutlineSafetyScore = 1f,
            FootprintPixels = footprint,
            CroppedAlpha = croppedAlpha,
            CroppedAlphaWidth = croppedWidth,
            CroppedAlphaHeight = croppedHeight,
        };

        return true;
    }

private static bool TryApplyContourSubtractionToOverlay(Texture2D overlayTexture, Texture2D contourTexture, AutoEyePatchTextureAnalysis contourAnalysis, Color fillColor, out Texture2D clippedOverlay, out RectInt finalBoundsPx, out Color[] clippedPixels, Color[] contourPixelsOverride = null)
{
    clippedOverlay = null;
    finalBoundsPx = default;
    clippedPixels = null;

    if (overlayTexture == null || contourTexture == null || contourAnalysis == null)
        return false;

    if (!AutoEyePatchAnalyzer.TryReadPixels(overlayTexture, out Color[] overlayPixels))
        return false;
    Color[] contourPixels = contourPixelsOverride;
    if (contourPixels == null)
    {
        if (!AutoEyePatchAnalyzer.TryReadPixels(contourTexture, out contourPixels))
            return false;
    }

    int width = overlayTexture.width;
    int height = overlayTexture.height;
    if (contourTexture.width != width || contourTexture.height != height)
        return false;

    try
    {
        const int alphaThresholdX100 = 20;

        int reference = Mathf.Max(1, Mathf.Min(contourAnalysis.OpaqueBounds.width, contourAnalysis.OpaqueBounds.height));
        int maxOutlineWidth = Mathf.Clamp(Mathf.RoundToInt(reference * 0.28f), 12, 28);

        bool[] opaqueMask = new bool[width * height];
        for (int i = 0; i < opaqueMask.Length; i++)
            opaqueMask[i] = IsSilhouettePixel(contourPixels[i], alphaThresholdX100);

        if (!TryBuildDirectionalCutRows(overlayPixels, contourPixels, width, height, alphaThresholdX100, maxOutlineWidth, out int[] cutXByRow, out _))
            return false;

        Color[] output = (Color[])overlayPixels.Clone();
        bool wroteAny = false;
        int finalMinX = width;
        int finalMinY = height;
        int finalMaxX = -1;
        int finalMaxY = -1;

        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width;
            int rowCutX = cutXByRow[y];
            for (int x = 0; x < width; x++)
            {
                int index = rowStart + x;
                Color src = output[index];
                if (src.a <= 0.001f)
                    continue;

                if (!opaqueMask[index])
                {
                    output[index] = Color.clear;
                    continue;
                }

                if (rowCutX >= 0 && x > rowCutX)
                {
                    output[index] = Color.clear;
                    continue;
                }

                wroteAny = true;
                if (x < finalMinX) finalMinX = x;
                if (y < finalMinY) finalMinY = y;
                if (x > finalMaxX) finalMaxX = x;
                if (y > finalMaxY) finalMaxY = y;
            }
        }

        if (!wroteAny)
            return false;

        Texture2D runtimeOverlay = new(width, height, TextureFormat.RGBA32, false);
        runtimeOverlay.filterMode = FilterMode.Bilinear;
        BleedTransparentPixels(output, width, height, 1);
        FloodTransparentPixels(output, fillColor);
        runtimeOverlay.wrapMode = TextureWrapMode.Clamp;
        runtimeOverlay.SetPixels(output, 0);
        runtimeOverlay.Apply(false, false);
        UnityEngine.Object.Destroy(overlayTexture);
        clippedOverlay = runtimeOverlay;
        clippedPixels = output;
        finalBoundsPx = new RectInt(finalMinX, finalMinY, (finalMaxX - finalMinX) + 1, (finalMaxY - finalMinY) + 1);
        return true;
    }
    catch (Exception e)
    {
        Log.Warning($"[Despicable] Failed to apply contour subtraction to east auto eye patch overlay: {e}");
        return false;
    }
}

private static bool[] BuildDarkEdgeDangerMask(Color[] contourPixels, bool[] opaqueMask, int[] edgeDistance, int width, int height, float hardKillLuma, float softKillLuma, int maxDarkEdgeInset, int growRadius)
{
    bool[] dangerMask = new bool[width * height];
    int safeInset = Mathf.Max(1, maxDarkEdgeInset);

    for (int i = 0; i < dangerMask.Length; i++)
    {
        if (!opaqueMask[i])
            continue;

        int distance = edgeDistance[i];
        if (distance == int.MaxValue || distance > safeInset)
            continue;

        Color pixel = contourPixels[i];
        if (pixel.a <= 0.001f)
            continue;

        float t = 1f - Mathf.Clamp01(distance / (float)safeInset);
        float threshold = Mathf.Lerp(softKillLuma, hardKillLuma, t);
        if (ComputeLuma(pixel) <= threshold)
            dangerMask[i] = true;
    }

    if (growRadius <= 0)
        return dangerMask;

    bool[] current = dangerMask;
    for (int step = 0; step < growRadius; step++)
    {
        bool[] expanded = (bool[])current.Clone();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                if (!opaqueMask[index] || current[index])
                    continue;

                bool touchesDanger = false;
                for (int oy = -1; oy <= 1 && !touchesDanger; oy++)
                {
                    int sy = y + oy;
                    if (sy < 0 || sy >= height)
                        continue;

                    for (int ox = -1; ox <= 1; ox++)
                    {
                        int sx = x + ox;
                        if (sx < 0 || sx >= width)
                            continue;

                        if (current[(sy * width) + sx])
                        {
                            touchesDanger = true;
                            break;
                        }
                    }
                }

                if (touchesDanger)
                    expanded[index] = true;
            }
        }

        current = expanded;
    }

    return current;
}

private static bool ShouldBrutallyCullOverlayPixel(Color[] contourPixels, int width, int height, int x, int y, int alphaThresholdX100, float hardKillLuma, float softKillLuma, int radius)
{
    int centerIndex = (y * width) + x;
    Color center = contourPixels[centerIndex];

    // Direct hit: if the sampled contour pixel is dark, kill it.
    // Do not require silhouette membership here or the fringe becomes invisible.
    if (center.a > 0.001f && ComputeLuma(center) <= hardKillLuma)
        return true;

    // Nearby dark fringe: only count it when it actually hugs the silhouette edge.
    for (int oy = -radius; oy <= radius; oy++)
    {
        int sy = y + oy;
        if (sy < 0 || sy >= height)
            continue;

        for (int ox = -radius; ox <= radius; ox++)
        {
            int sx = x + ox;
            if (sx < 0 || sx >= width)
                continue;

            int sampleIndex = (sy * width) + sx;
            Color sample = contourPixels[sampleIndex];

            // Ignore fully empty padding, but do not require silhouette alpha.
            if (sample.a <= 0.001f)
                continue;

            if (ComputeLuma(sample) > softKillLuma)
                continue;

            if (TouchesSilhouette(contourPixels, width, height, sx, sy, alphaThresholdX100))
                return true;
        }
    }

    return false;
}

private static bool TouchesSilhouette(Color[] contourPixels, int width, int height, int x, int y, int alphaThresholdX100)
{
    for (int oy = -1; oy <= 1; oy++)
    {
        int sy = y + oy;
        if (sy < 0 || sy >= height)
            continue;

        for (int ox = -1; ox <= 1; ox++)
        {
            int sx = x + ox;
            if (sx < 0 || sx >= width)
                continue;

            int index = (sy * width) + sx;
            if (IsSilhouettePixel(contourPixels[index], alphaThresholdX100))
                return true;
        }
    }

    return false;
}

private static string BuildEastOverlayForensics(string stage, Texture2D overlayTexture, Texture2D contourTexture, RectInt safeInteriorBounds)
{
    if (overlayTexture == null || contourTexture == null)
        return $"{stage}[missing_texture]";

    if (!AutoEyePatchAnalyzer.TryReadPixels(overlayTexture, out Color[] overlayPixels))
        return $"{stage}[overlay_unreadable]";
    if (!AutoEyePatchAnalyzer.TryReadPixels(contourTexture, out Color[] contourPixels))
        return $"{stage}[contour_unreadable]";

    int width = overlayTexture.width;
    int height = overlayTexture.height;
    if (contourTexture.width != width || contourTexture.height != height)
        return $"{stage}[size_mismatch overlay={width}x{height} contour={contourTexture.width}x{contourTexture.height}]";

    return BuildEastOverlayForensics(stage, overlayPixels, width, height, contourPixels, safeInteriorBounds);
}

private static string BuildEastOverlayForensics(string stage, Color[] overlayPixels, int width, int height, Color[] contourPixels, RectInt safeInteriorBounds)
{
    if (overlayPixels == null || contourPixels == null || overlayPixels.Length != width * height || contourPixels.Length != width * height)
        return $"{stage}[invalid_pixels]";

    const int alphaThresholdX100 = 20;
    const float hardKillLuma = 0.24f;

    int reference = Mathf.Max(1, Mathf.Min(safeInteriorBounds.width, safeInteriorBounds.height));
    int outlineThicknessPx = Mathf.Clamp(Mathf.RoundToInt(reference * 0.060f), 3, 8);
    int outlineSearchDepthPx = Mathf.Clamp(outlineThicknessPx + 4, outlineThicknessPx + 2, 14);
    int fringeThicknessPx = Mathf.Clamp(Mathf.RoundToInt(outlineThicknessPx * 0.50f), 1, 4);
    int maxOutlineWidth = Mathf.Clamp(Mathf.RoundToInt(reference * 0.18f), 8, 18);
    BuildContourProtectionData(contourPixels, width, height, outlineThicknessPx, outlineSearchDepthPx, preferInsetContour: true, out bool[] opaqueMask, out int[] edgeDistance, out bool[] protectedOutlineMask);
    TryBuildDirectionalCutRows(overlayPixels, contourPixels, width, height, alphaThresholdX100, maxOutlineWidth, out int[] cutXByRow, out _);

    int overlayCount = 0;
    int outsideSilhouetteCount = 0;
    int protectedOutlineCount = 0;
    int prepassUnsafeCount = 0;
    int directDarkCount = 0;
    int lateCullEligibleCount = 0;

    for (int y = 0; y < height; y++)
    {
        int rowStart = y * width;
        for (int x = 0; x < width; x++)
        {
            int index = rowStart + x;
            if (overlayPixels[index].a <= 0.001f)
                continue;

            overlayCount++;
            if (!IsSilhouettePixel(contourPixels[index], alphaThresholdX100))
                outsideSilhouetteCount++;
            if (protectedOutlineMask[index])
                protectedOutlineCount++;

            float prepassMultiplier = ComputeOutlineSubtractionMultiplierAtPixel(contourPixels, opaqueMask, edgeDistance, protectedOutlineMask, width, height, x, y, outlineThicknessPx, fringeThicknessPx);
            if (prepassMultiplier < 0.999f)
                prepassUnsafeCount++;

            if (contourPixels[index].a > 0.001f && ComputeLuma(contourPixels[index]) <= hardKillLuma)
                directDarkCount++;

            if (cutXByRow[y] >= 0 && x > cutXByRow[y])
                lateCullEligibleCount++;
        }
    }

    string boundsText = TryComputeNonZeroBounds(overlayPixels, width, height, 0.001f, out RectInt bounds)
        ? $"{bounds.xMin},{bounds.yMin},{bounds.width}x{bounds.height}"
        : "none";

    return $"{stage}[overlay={overlayCount},outside={outsideSilhouetteCount},protected={protectedOutlineCount},preUnsafe={prepassUnsafeCount},directDark={directDarkCount},lateCull={lateCullEligibleCount},bounds={boundsText}]";
}

private static bool TryComputeNonZeroBounds(Color[] pixels, int width, int height, float alphaThreshold, out RectInt bounds)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            int rowStart = y * width;
            for (int x = 0; x < width; x++)
            {
                if (pixels[rowStart + x].a <= alphaThreshold)
                    continue;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            bounds = default;
            return false;
        }

        bounds = new RectInt(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        return true;
    }

    private static bool IsSilhouettePixel(Color pixel, int alphaThresholdX100) => Mathf.RoundToInt(pixel.a * 100f) >= alphaThresholdX100;

    private static int FindRightmostSilhouettePixelOnRow(Color[] contourPixels, int width, int y, int alphaThresholdX100)
    {
        int rowStart = y * width;
        for (int x = width - 1; x >= 0; x--)
        {
            if (IsSilhouettePixel(contourPixels[rowStart + x], alphaThresholdX100))
                return x;
        }

        return -1;
    }

    private static int ResolveAggressiveOutlineCutForRow(Color[] overlayPixels, Color[] contourPixels, int width, int height, int y, int rightEdgeX, int overlayRowMinX, int overlayRowMaxX, int alphaThresholdX100, int maxOutlineWidth)
    {
        int searchFloorX = Mathf.Min(rightEdgeX - Mathf.Max(1, maxOutlineWidth), overlayRowMinX - 2);
        int minX = Mathf.Max(0, searchFloorX);
        int rowStart = y * width;
        float interiorLuma = ComputeLooseInteriorReferenceLumaForRow(contourPixels, width, y, minX, overlayRowMinX, overlayRowMaxX, rightEdgeX, alphaThresholdX100);
        float entryDarknessDelta = Mathf.Clamp(interiorLuma * 0.06f, 0.015f, 0.05f);
        const float nearPeakTolerance = 0.030f;
        const float relativeRecoveryThreshold = 0.20f;
        const float absoluteRecoveryThreshold = 0.020f;
        const float recoveryAlphaThreshold = 0.06f;
        const int recoveryConfirmationNeeded = 1;

        int fallbackDarkOverlayX = FindLeftmostLooseDarkOverlayPixelOnRow(overlayPixels, contourPixels, width, y, overlayRowMinX, overlayRowMaxX, interiorLuma, alphaThresholdX100);
        int fallbackCutX = fallbackDarkOverlayX >= 0 ? Mathf.Max(minX, fallbackDarkOverlayX - 1) : rightEdgeX;

        bool foundBorderBand = false;
        float peakLuma = 1f;
        float peakDarkness = 0f;
        int bestSafeX = rightEdgeX;
        int recoveryStreak = 0;

        for (int x = rightEdgeX; x >= minX; x--)
        {
            Color sample = contourPixels[rowStart + x];
            if (sample.a <= 0.001f)
            {
                recoveryStreak = 0;
                continue;
            }

            float luma = ComputeLuma(sample);
            float darkness = 1f - luma;
            bool darkByAbsolute = luma <= 0.30f;
            bool darkerThanInterior = (interiorLuma - luma) >= entryDarknessDelta;

            if (!foundBorderBand)
            {
                if (!darkerThanInterior && !darkByAbsolute)
                    continue;

                foundBorderBand = true;
                peakLuma = luma;
                peakDarkness = darkness;
                bestSafeX = Mathf.Max(minX, x - 1);
                recoveryStreak = 0;
                continue;
            }

            bool stillInsideBand = darkByAbsolute || darkerThanInterior || darkness >= peakDarkness - nearPeakTolerance || luma <= peakLuma + nearPeakTolerance;
            if (stillInsideBand)
            {
                if (luma < peakLuma)
                    peakLuma = luma;
                if (darkness > peakDarkness)
                    peakDarkness = darkness;

                bestSafeX = Mathf.Max(minX, x - 1);
                recoveryStreak = 0;
                continue;
            }

            if (sample.a < recoveryAlphaThreshold)
            {
                recoveryStreak = 0;
                continue;
            }

            float relativeRecovery = peakDarkness > 0.001f ? (peakDarkness - darkness) / peakDarkness : 0f;
            float absoluteRecovery = luma - peakLuma;
            bool recovered = relativeRecovery >= relativeRecoveryThreshold || absoluteRecovery >= absoluteRecoveryThreshold;
            if (!recovered)
            {
                recoveryStreak = 0;
                continue;
            }

            recoveryStreak++;
            if (recoveryStreak >= recoveryConfirmationNeeded)
                return Mathf.Clamp(Mathf.Min(x, fallbackCutX), minX, rightEdgeX);
        }

        if (foundBorderBand || fallbackDarkOverlayX >= 0)
            return Mathf.Clamp(Mathf.Min(bestSafeX, fallbackCutX), minX, rightEdgeX);

        return rightEdgeX;
    }

    private static float ComputeInteriorReferenceLumaForRow(Color[] contourPixels, int width, int y, int minX, int rightEdgeX, int alphaThresholdX100)
    {
        int sampleEnd = Mathf.Max(minX, rightEdgeX - 2);
        int sampleStart = Mathf.Max(0, sampleEnd - 5);
        float sum = 0f;
        int count = 0;
        int rowStart = y * width;

        for (int x = sampleStart; x <= sampleEnd; x++)
        {
            Color sample = contourPixels[rowStart + x];
            if (!IsSilhouettePixel(sample, alphaThresholdX100))
                continue;

            sum += ComputeLuma(sample);
            count++;
        }

        if (count > 0)
            return sum / count;

        Color fallback = contourPixels[rowStart + Mathf.Clamp(sampleEnd, 0, width - 1)];
        return ComputeLuma(fallback);
    }


    private static float ComputeLooseInteriorReferenceLumaForRow(Color[] contourPixels, int width, int y, int minX, int overlayRowMinX, int overlayRowMaxX, int rightEdgeX, int alphaThresholdX100)
    {
        int rowStart = y * width;
        int sampleStart = Mathf.Max(0, Mathf.Min(minX, overlayRowMinX - 6));
        int sampleEnd = Mathf.Clamp(Mathf.Min(rightEdgeX, overlayRowMaxX), 0, width - 1);
        float brightest = -1f;

        for (int x = sampleStart; x <= sampleEnd; x++)
        {
            Color sample = contourPixels[rowStart + x];
            if (sample.a <= 0.05f)
                continue;
            if (!IsSilhouettePixel(sample, alphaThresholdX100) && sample.a < 0.20f)
                continue;

            float luma = ComputeLuma(sample);
            if (luma > brightest)
                brightest = luma;
        }

        if (brightest >= 0f)
            return brightest;

        return ComputeInteriorReferenceLumaForRow(contourPixels, width, y, minX, rightEdgeX, alphaThresholdX100);
    }

    private static int FindLeftmostLooseDarkOverlayPixelOnRow(Color[] overlayPixels, Color[] contourPixels, int width, int y, int minX, int maxX, float interiorLuma, int alphaThresholdX100)
    {
        int rowStart = y * width;
        int clampedMinX = Mathf.Clamp(minX, 0, width - 1);
        int clampedMaxX = Mathf.Clamp(maxX, 0, width - 1);
        if (clampedMaxX < clampedMinX)
            return -1;

        for (int x = clampedMinX; x <= clampedMaxX; x++)
        {
            if (overlayPixels[rowStart + x].a <= 0.001f)
                continue;

            Color sample = contourPixels[rowStart + x];
            if (sample.a <= 0.001f)
                continue;
            if (!IsSilhouettePixel(sample, alphaThresholdX100) && sample.a < 0.05f)
                continue;

            float luma = ComputeLuma(sample);
            float darknessDelta = interiorLuma - luma;
            bool darkByAbsolute = luma <= 0.30f;
            bool darkByContrast = darknessDelta >= 0.03f;
            if (!darkByAbsolute && !darkByContrast)
                continue;

            return x;
        }

        return -1;
    }

    private static bool IsNearBlackOutlinePixel(Color[] contourPixels, int width, int height, int x, int y, int alphaThresholdX100)
    {
        int index = (y * width) + x;
        Color pixel = contourPixels[index];
        if (!IsSilhouettePixel(pixel, alphaThresholdX100))
            return false;

        float luma = ComputeLuma(pixel);
        if (luma <= 0.18f)
            return true;

        float leftLuma = x > 0 ? ComputeLuma(contourPixels[index - 1]) : luma;
        float rightLuma = x + 1 < width ? ComputeLuma(contourPixels[index + 1]) : luma;
        float localContrast = Mathf.Max(Mathf.Abs(luma - leftLuma), Mathf.Abs(luma - rightLuma));
        return luma <= 0.24f && localContrast >= 0.02f;
    }

    private static bool TryBuildDirectionalCutRows(Color[] overlayPixels, Color[] contourPixels, int width, int height, int alphaThresholdX100, int maxOutlineWidth, out int[] cutXByRow, out RectInt overlayBounds)
    {
        cutXByRow = new int[height];
        for (int i = 0; i < cutXByRow.Length; i++)
            cutXByRow[i] = -1;

        if (!TryComputeNonZeroBounds(overlayPixels, width, height, 0.001f, out overlayBounds))
            return false;

        for (int y = overlayBounds.yMin; y <= overlayBounds.yMax; y++)
        {
            bool rowHasOverlay = false;
            int rowStart = y * width;
            int overlayRowMinX = width;
            int overlayRowMaxX = -1;
            for (int x = overlayBounds.xMin; x <= overlayBounds.xMax; x++)
            {
                if (overlayPixels[rowStart + x].a > 0.001f)
                {
                    rowHasOverlay = true;
                    if (x < overlayRowMinX) overlayRowMinX = x;
                    if (x > overlayRowMaxX) overlayRowMaxX = x;
                }
            }

            if (!rowHasOverlay)
                continue;

            int rightEdgeX = FindRightmostSilhouettePixelOnRow(contourPixels, width, y, alphaThresholdX100);
            if (rightEdgeX < 0)
                continue;

            cutXByRow[y] = ResolveAggressiveOutlineCutForRow(overlayPixels, contourPixels, width, height, y, rightEdgeX, overlayRowMinX, overlayRowMaxX, alphaThresholdX100, maxOutlineWidth);
        }

        FillMissingDirectionalCutRows(cutXByRow, overlayBounds.yMin, overlayBounds.yMax);
        SmoothDirectionalCutRows(cutXByRow, overlayBounds.yMin, overlayBounds.yMax, overlayBounds);
        return true;
    }

    private static void FillMissingDirectionalCutRows(int[] cutXByRow, int minY, int maxY)
    {
        int lastKnown = -1;
        for (int y = minY; y <= maxY; y++)
        {
            if (cutXByRow[y] >= 0)
            {
                lastKnown = cutXByRow[y];
                continue;
            }

            int nextKnown = -1;
            for (int look = y + 1; look <= maxY; look++)
            {
                if (cutXByRow[look] >= 0)
                {
                    nextKnown = cutXByRow[look];
                    break;
                }
            }

            if (lastKnown >= 0 && nextKnown >= 0)
                cutXByRow[y] = Mathf.RoundToInt((lastKnown + nextKnown) * 0.5f);
            else if (lastKnown >= 0)
                cutXByRow[y] = lastKnown;
            else if (nextKnown >= 0)
                cutXByRow[y] = nextKnown;
        }
    }

    private static void SmoothDirectionalCutRows(int[] cutXByRow, int minY, int maxY, RectInt overlayBounds)
    {
        if (minY > maxY)
            return;

        int[] snapshot = (int[])cutXByRow.Clone();
        int[] samples = new int[5];

        for (int y = minY; y <= maxY; y++)
        {
            int count = 0;
            for (int offset = -2; offset <= 2; offset++)
            {
                int sampleY = Mathf.Clamp(y + offset, minY, maxY);
                int sample = snapshot[sampleY];
                if (sample < 0)
                    continue;

                samples[count++] = sample;
            }

            if (count == 0)
                continue;

            Array.Sort(samples, 0, count);
            int median = samples[count / 2];
            int clamped = median;
            int current = snapshot[y];
            if (current >= 0 && current < median - 2)
                clamped = median - 2;
            else if (current >= 0 && current > median + 2)
                clamped = median + 2;

            clamped = Mathf.Clamp(clamped, overlayBounds.xMin, overlayBounds.xMax);
            cutXByRow[y] = clamped;
        }
    }

    private static AutoEyePatchDescriptor CloneDescriptorWithRuntime(AutoEyePatchDescriptor template, Texture2D runtimeTexture, RectInt finalBoundsPx, bool preserveTemplateGeometry = false)
    {
        if (template == null || runtimeTexture == null)
            return null;

        int width = runtimeTexture.width;
        int height = runtimeTexture.height;
        float halfWidth = Mathf.Max(1f, finalBoundsPx.width * 0.5f);
        float halfHeight = Mathf.Max(1f, finalBoundsPx.height * 0.5f);

        return new AutoEyePatchDescriptor
        {
            EnvelopeType = template.EnvelopeType,
            CenterUV = preserveTemplateGeometry ? template.CenterUV : new Vector2(finalBoundsPx.center.x / (float)width, finalBoundsPx.center.y / (float)height),
            RadiusUV = preserveTemplateGeometry ? template.RadiusUV : new Vector2(halfWidth / width, halfHeight / height),
            FeatherUV = template.FeatherUV,
            FillColor = template.FillColor,
            Confidence = template.Confidence,
            OutlineSafetyScore = template.OutlineSafetyScore,
            LocalColorStabilityScore = template.LocalColorStabilityScore,
            FootprintCompactnessScore = template.FootprintCompactnessScore,
            RuntimeTexture = runtimeTexture,
        };
    }

    private static int ComputeContourWorkScale(int width, int height)
    {
        int minDimension = Mathf.Max(1, Mathf.Min(width, height));
        int targetMinimum = 256;
        int scale = Mathf.CeilToInt(targetMinimum / (float)minDimension);
        return Mathf.Clamp(scale, 1, 8);
    }

    private static Color[] UpscalePixelsNearest(Color[] source, int width, int height, int scale)
    {
        if (scale <= 1)
            return (Color[])source.Clone();

        int workWidth = width * scale;
        int workHeight = height * scale;
        Color[] result = new Color[workWidth * workHeight];
        for (int y = 0; y < workHeight; y++)
        {
            int sourceY = Mathf.Min(height - 1, y / scale);
            int sourceRow = sourceY * width;
            int resultRow = y * workWidth;
            for (int x = 0; x < workWidth; x++)
            {
                int sourceX = Mathf.Min(width - 1, x / scale);
                result[resultRow + x] = source[sourceRow + sourceX];
            }
        }

        return result;
    }

    private static Color[] DownsamplePixelsBox(Color[] source, int width, int height, int scale)
    {
        if (scale <= 1)
            return (Color[])source.Clone();

        int targetWidth = width / scale;
        int targetHeight = height / scale;
        Color[] result = new Color[targetWidth * targetHeight];
        float sampleCount = scale * scale;
        for (int y = 0; y < targetHeight; y++)
        {
            for (int x = 0; x < targetWidth; x++)
            {
                float alphaSum = 0f;
                float redSum = 0f;
                float greenSum = 0f;
                float blueSum = 0f;

                for (int sy = 0; sy < scale; sy++)
                {
                    int sourceY = (y * scale) + sy;
                    int sourceRow = sourceY * width;
                    for (int sx = 0; sx < scale; sx++)
                    {
                        Color sample = source[sourceRow + (x * scale) + sx];
                        alphaSum += sample.a;
                        redSum += sample.r * sample.a;
                        greenSum += sample.g * sample.a;
                        blueSum += sample.b * sample.a;
                    }
                }

                float alpha = alphaSum / sampleCount;
                if (alpha <= 0.001f)
                {
                    result[(y * targetWidth) + x] = Color.clear;
                    continue;
                }

                float normalizer = Mathf.Max(0.0001f, alphaSum);
                result[(y * targetWidth) + x] = new Color(redSum / normalizer, greenSum / normalizer, blueSum / normalizer, alpha);
            }
        }

        return result;
    }

    private static float ComputeProjectedOutlineSubtractionMultiplier(Color[] contourPixels, bool[] opaqueMask, int[] edgeDistance, bool[] protectedOutlineMask, int width, int height, float centerX, float centerY, float halfSpanX, float halfSpanY, int outlineThicknessPx, int fringeThicknessPx)
    {
        int minX = Mathf.FloorToInt(centerX - halfSpanX);
        int maxX = Mathf.CeilToInt(centerX + halfSpanX) - 1;
        int minY = Mathf.FloorToInt(centerY - halfSpanY);
        int maxY = Mathf.CeilToInt(centerY + halfSpanY) - 1;

        if (minX < 0 || minY < 0 || maxX >= width || maxY >= height)
            return 0f;

        float weightedCoverage = 0f;
        int totalSamples = 0;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                totalSamples++;
                float sampleMultiplier = ComputeOutlineSubtractionMultiplierAtPixel(contourPixels, opaqueMask, edgeDistance, protectedOutlineMask, width, height, x, y, outlineThicknessPx, fringeThicknessPx);
                if (sampleMultiplier <= 0.001f)
                    continue;

                weightedCoverage += sampleMultiplier;
            }
        }

        if (totalSamples <= 0)
            return 0f;

        return Mathf.Clamp01(weightedCoverage / totalSamples);
    }

    private static bool TouchesForbiddenPaintZone(Color[] contourPixels, bool[] opaqueMask, int[] edgeDistance, bool[] protectedOutlineMask, int width, int height, int centerX, int centerY, int guardX, int guardY, int outlineThicknessPx, int fringeThicknessPx)
    {
        for (int y = centerY - guardY; y <= centerY + guardY; y++)
        {
            for (int x = centerX - guardX; x <= centerX + guardX; x++)
            {
                if (x < 0 || x >= width || y < 0 || y >= height)
                    return true;

                if (IsVisualOutlinePixel(contourPixels, opaqueMask, edgeDistance, protectedOutlineMask, width, height, x, y, outlineThicknessPx, fringeThicknessPx))
                    return true;
            }
        }

        return false;
    }

    private static bool IsVisualOutlinePixel(Color[] contourPixels, bool[] opaqueMask, int[] edgeDistance, bool[] protectedOutlineMask, int width, int height, int x, int y, int outlineThicknessPx, int fringeThicknessPx)
    {
        int index = (y * width) + x;
        if (index < 0 || index >= opaqueMask.Length)
            return true;

        // Hard reject outside the filled head silhouette.
        if (!opaqueMask[index])
            return true;

        // Existing protected contour remains forbidden.
        if (protectedOutlineMask[index])
            return true;

        int distance = edgeDistance[index];
        if (distance == int.MaxValue)
            return true;

        float luma = ComputeLuma(contourPixels[index]);

        // Treat the immediate interior fringe as forbidden. This catches pixels that are
        // technically inside the silhouette but still visually part of the border.
        if (distance <= Mathf.Max(1, fringeThicknessPx + 1))
            return true;

        // Build a local interior baseline from nearby, safer pixels a bit farther from the edge.
        float localInteriorLuma = 0f;
        int localInteriorCount = 0;
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

                int sIndex = (sy * width) + sx;
                if (!opaqueMask[sIndex])
                    continue;

                int sDistance = edgeDistance[sIndex];
                if (sDistance == int.MaxValue)
                    continue;

                // Prefer samples that are slightly deeper inside the silhouette so we compare
                // against skin/fill, not the edge itself.
                if (sDistance <= outlineThicknessPx + fringeThicknessPx + 1)
                    continue;

                localInteriorLuma += ComputeLuma(contourPixels[sIndex]);
                localInteriorCount++;
            }
        }

        float localAverageLuma = localInteriorCount > 0 ? (localInteriorLuma / localInteriorCount) : luma;

        // Visual outline = darker than nearby interior + near silhouette.
        bool strongLocalContrast = luma <= (localAverageLuma - 0.045f);
        bool softAABorder = luma <= 0.48f && distance <= outlineThicknessPx + fringeThicknessPx + 2;
        bool likelyContour = distance <= outlineThicknessPx + fringeThicknessPx + 2
            && (strongLocalContrast || softAABorder || IsLikelyContourPixel(contourPixels, opaqueMask, width, height, x, y, luma, 0.02f));
        if (likelyContour)
            return true;

        // Tiny inward dilation: if any immediate neighbor is already a strong outline pixel
        // in the edge band, treat this one as forbidden too.
        for (int oy = -1; oy <= 1; oy++)
        {
            int sy = y + oy;
            if (sy < 0 || sy >= height)
                continue;

            for (int ox = -1; ox <= 1; ox++)
            {
                int sx = x + ox;
                if (sx < 0 || sx >= width)
                    continue;

                int sIndex = (sy * width) + sx;
                if (!opaqueMask[sIndex] || protectedOutlineMask[sIndex])
                    return true;

                int sDistance = edgeDistance[sIndex];
                if (sDistance == int.MaxValue)
                    continue;

                float sLuma = ComputeLuma(contourPixels[sIndex]);
                bool strongNeighborOutline = sDistance <= outlineThicknessPx + fringeThicknessPx + 1
                    && (sLuma <= 0.42f || sLuma <= (localAverageLuma - 0.035f));
                if (strongNeighborOutline)
                    return true;
            }
        }

        return false;
    }

    private static float ComputeOutlineSubtractionMultiplierAtPixel(Color[] contourPixels, bool[] opaqueMask, int[] edgeDistance, bool[] protectedOutlineMask, int width, int height, int x, int y, int outlineThicknessPx, int fringeThicknessPx)
    {
        int index = (y * width) + x;
        if (index < 0 || index >= opaqueMask.Length || !opaqueMask[index])
            return 0f;

        if (protectedOutlineMask[index])
            return 0f;

        int distance = edgeDistance[index];
        if (distance == int.MaxValue)
            return 0f;

        int hardInset = Mathf.Max(1, outlineThicknessPx + fringeThicknessPx);
        if (distance <= hardInset)
            return 0f;

        float luma = ComputeLuma(contourPixels[index]);
        int darkGuardInset = Mathf.Max(hardInset, outlineThicknessPx + Mathf.Max(1, fringeThicknessPx * 2));
        if (distance <= darkGuardInset && luma <= 0.42f)
            return 0f;

        int softInset = darkGuardInset + Mathf.Max(1, fringeThicknessPx);
        if (distance <= softInset)
        {
            float t = Mathf.Clamp01((distance - darkGuardInset) / (float)Mathf.Max(1, softInset - darkGuardInset));
            return Mathf.Lerp(0.35f, 1f, t);
        }

        return 1f;
    }

    private static void BuildOpaqueDistanceField(Color[] contourPixels, int width, int height, bool[] opaqueMask, int[] edgeDistance)
    {
        Queue<int> frontier = GetScratchIndexQueue();
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

    private static void ChooseBestSidePlacement(Color[] overlayPixels, Color[] contourPixels, bool[] opaqueMask, int[] edgeDistance, bool[] protectedOutlineMask, int width, int height, int outlineThicknessPx, int maxShiftPx, int inwardDirection, out int bestShift, out float bestScale)
    {
        bestShift = 0;
        bestScale = 1f;

        float[] scales = { 1.00f, 0.97f, 0.94f, 0.90f };
        float centerX = (width - 1) * 0.5f;
        float centerY = (height - 1) * 0.5f;
        float bestPenalty = float.MaxValue;

        for (int scaleIndex = 0; scaleIndex < scales.Length; scaleIndex++)
        {
            float scale = scales[scaleIndex];
            for (int step = 0; step <= maxShiftPx; step++)
            {
                int shift = inwardDirection == 0 ? 0 : step * inwardDirection;
                float penalty = 0f;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        Color src = overlayPixels[(y * width) + x];
                        if (src.a <= 0.001f)
                            continue;

                        int destX = Mathf.RoundToInt(centerX + ((x - centerX) * scale) + shift);
                        int destY = Mathf.RoundToInt(centerY + ((y - centerY) * scale));
                        if (destX < 0 || destX >= width || destY < 0 || destY >= height)
                        {
                            penalty += src.a * 18f;
                            continue;
                        }

                        int index = (destY * width) + destX;
                        penalty += src.a * ComputeContourPenalty(contourPixels[index], opaqueMask[index], edgeDistance[index], protectedOutlineMask[index], outlineThicknessPx);
                    }
                }

                if (penalty < bestPenalty)
                {
                    bestPenalty = penalty;
                    bestShift = shift;
                    bestScale = scale;
                }
            }
        }
    }

    private static float ComputeContourPenalty(Color contourPixel, bool isOpaque, int edgeDistance, bool isProtectedOutline, int outlineThicknessPx)
    {
        if (!isOpaque || edgeDistance == int.MaxValue)
            return 18f;
        if (isProtectedOutline)
            return 28f;

        if (edgeDistance > outlineThicknessPx)
            return 0f;

        float luma = ComputeLuma(contourPixel);
        float darkness = Mathf.Clamp01((0.30f - luma) / 0.30f);
        float proximity = 1f - Mathf.Clamp01(edgeDistance / (float)Mathf.Max(1, outlineThicknessPx));
        return proximity * (4f + (6f * darkness));
    }

    private static float ComputeContourAlphaMultiplier(Color[] contourPixels, bool[] opaqueMask, int[] edgeDistance, bool[] protectedOutlineMask, int width, int height, int x, int y, int outlineThicknessPx, int fringeThicknessPx, bool strictEdgeBand)
    {
        int index = (y * width) + x;
        if (!opaqueMask[index])
            return 0f;
        if (protectedOutlineMask[index])
            return 0f;

        int distance = edgeDistance[index];
        if (distance == int.MaxValue)
            return 0f;

        int fadeStart = strictEdgeBand ? outlineThicknessPx + 1 : Mathf.Max(0, outlineThicknessPx - 1);
        int fadeEnd = strictEdgeBand
            ? fadeStart + Mathf.Max(1, fringeThicknessPx)
            : outlineThicknessPx + Mathf.Max(1, fringeThicknessPx) + 1;
        if (distance >= fadeEnd)
            return 1f;

        float luma = ComputeLuma(contourPixels[index]);
        float darkness = Mathf.Clamp01((0.32f - luma) / 0.32f);
        if (distance <= fadeStart)
        {
            if (strictEdgeBand)
                return 0f;

            return Mathf.Lerp(0.35f, 0.55f, 1f - darkness);
        }

        float t = Mathf.Clamp01((distance - fadeStart) / (float)Mathf.Max(1, fadeEnd - fadeStart));
        float minimum = strictEdgeBand
            ? Mathf.Lerp(0.18f, 0.35f, 1f - darkness)
            : Mathf.Lerp(0.30f, 0.50f, 1f - darkness);
        return Mathf.Lerp(minimum, 1f, t);
    }

    private static void BuildContourProtectionData(Color[] contourPixels, int width, int height, int outlineThicknessPx, int outlineSearchDepthPx, bool preferInsetContour, out bool[] opaqueMask, out int[] edgeDistance, out bool[] protectedOutlineMask)
    {
        opaqueMask = new bool[width * height];
        edgeDistance = new int[width * height];
        BuildOpaqueDistanceField(contourPixels, width, height, opaqueMask, edgeDistance);

        protectedOutlineMask = new bool[width * height];
        if (preferInsetContour && TryBuildEdgeAnchoredContourMask(contourPixels, opaqueMask, edgeDistance, width, height, outlineThicknessPx, outlineSearchDepthPx, protectedOutlineMask))
            return;

        Queue<int> frontier = GetScratchIndexQueue();
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
        {
            ExpandProtectedStrokeThickness(contourPixels, opaqueMask, edgeDistance, width, height, outlineSearchDepthPx, protectedOutlineMask, outlineThicknessPx);
        }

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

        List<int> boundary = GetScratchIntListA();
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
        Queue<int> frontier = GetScratchIndexQueue();

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

            if (bestIndex < 0)
                continue;

            if (protectedOutlineMask[bestIndex])
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

        for (int i = 0; i < protectedOutlineMask.Length; i++)
        {
            if (protectedOutlineMask[i])
                return true;
        }

        return false;
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
        Queue<int> frontier = GetScratchIndexQueue();
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

    private static float ComputeLuma(Color c) => (0.2126f * c.r) + (0.7152f * c.g) + (0.0722f * c.b);

    private static Color ResolveSkinFillColor() => Color.white;

    private static bool TryBuildDescriptor(Texture2D texture, AutoEyePatchTextureAnalysis analysis, AutoEyePatchDarkCandidate candidate, out AutoEyePatchDescriptor descriptor, out AutoEyePatchSkipReason reasons)
    {
        descriptor = null;
        reasons = AutoEyePatchSkipReason.None;

        if (texture == null || analysis == null || candidate == null)
        {
            reasons = AutoEyePatchSkipReason.TextureMissing;
            return false;
        }

        // The runtime auto eye patch now renders through CutoutSkin, so a neutral white fill lets the
        // pawn's actual skin tint come from the shader at draw time instead of baking a head-texture
        // average into the generated overlay.
        Color fillColor = ResolveSkinFillColor();
        float colorStability = 1f;

        if (!TryBuildFootprintOverlayTexture(texture, analysis.OpaqueBounds, candidate, fillColor, out Texture2D runtimeTexture, out RectInt finalBoundsPx, out float featherPx))
        {
            reasons = AutoEyePatchSkipReason.GenerationError;
            return false;
        }

        float confidence = Mathf.Clamp01((candidate.OutlineSafetyScore * 0.45f) + (candidate.CompactnessScore * 0.25f) + (candidate.DarknessScore * 0.15f) + (colorStability * 0.15f));
        descriptor = new AutoEyePatchDescriptor
        {
            EnvelopeType = AutoEyePatchEnvelopeType.Blob,
            CenterUV = new Vector2(finalBoundsPx.center.x / (float)texture.width, finalBoundsPx.center.y / (float)texture.height),
            RadiusUV = new Vector2(Mathf.Max(1f, finalBoundsPx.width * 0.5f) / texture.width, Mathf.Max(1f, finalBoundsPx.height * 0.5f) / texture.height),
            FeatherUV = 0f,
            FillColor = fillColor,
            Confidence = confidence,
            OutlineSafetyScore = candidate.OutlineSafetyScore,
            LocalColorStabilityScore = colorStability,
            FootprintCompactnessScore = candidate.CompactnessScore,
            RuntimeTexture = runtimeTexture,
        };

        if (descriptor.RuntimeTexture == null)
        {
            reasons = AutoEyePatchSkipReason.GenerationError;
            descriptor = null;
            return false;
        }

        return true;
    }

    private static bool TrySampleFillColor(Texture2D texture, AutoEyePatchDarkCandidate candidate, RectInt safeInteriorBounds, out Color fillColor, out float stability)
    {
        fillColor = Color.clear;
        stability = 0f;
        if (!AutoEyePatchAnalyzer.TryReadPixels(texture, out Color[] pixels))
            return false;

        int width = texture.width;
        int pad = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(candidate.BoundsPx.width, candidate.BoundsPx.height) * 0.5f));
        float totalR = 0f;
        float totalG = 0f;
        float totalB = 0f;
        int count = 0;
        float minLuma = 1f;
        float maxLuma = 0f;

        for (int y = Mathf.Max(safeInteriorBounds.yMin, candidate.BoundsPx.yMin - pad); y < Mathf.Min(safeInteriorBounds.yMax, candidate.BoundsPx.yMax + pad); y++)
        {
            for (int x = Mathf.Max(safeInteriorBounds.xMin, candidate.BoundsPx.xMin - pad); x < Mathf.Min(safeInteriorBounds.xMax, candidate.BoundsPx.xMax + pad); x++)
            {
                if (candidate.BoundsPx.Contains(new Vector2Int(x, y)))
                    continue;

                Color c = pixels[(y * width) + x];
                if (c.a <= 0.10f)
                    continue;

                float luma = (0.2126f * c.r) + (0.7152f * c.g) + (0.0722f * c.b);
                if (luma < 0.18f)
                    continue;

                totalR += c.r;
                totalG += c.g;
                totalB += c.b;
                count++;
                if (luma < minLuma) minLuma = luma;
                if (luma > maxLuma) maxLuma = luma;
            }
        }

        if (count < 3)
            return false;

        fillColor = new Color(totalR / count, totalG / count, totalB / count, 1f);
        stability = Mathf.Clamp01(1f - (maxLuma - minLuma));
        return true;
    }

    private static bool TryBuildFootprintOverlayTexture(Texture2D sourceTexture, RectInt safeInteriorBounds, AutoEyePatchDarkCandidate candidate, Color fillColor, out Texture2D overlay, out RectInt finalBoundsPx, out float featherPx)
        => TryBuildFootprintOverlayTexture(sourceTexture, safeInteriorBounds, candidate, fillColor, null, allowAdaptiveInflation: true, out overlay, out finalBoundsPx, out featherPx, out _);

    private static bool TryBuildFootprintOverlayTexture(Texture2D sourceTexture, RectInt safeInteriorBounds, AutoEyePatchDarkCandidate candidate, Color fillColor, Vector2? targetCenterOverride, out Texture2D overlay, out RectInt finalBoundsPx, out float featherPx)
        => TryBuildFootprintOverlayTexture(sourceTexture, safeInteriorBounds, candidate, fillColor, targetCenterOverride, allowAdaptiveInflation: true, out overlay, out finalBoundsPx, out featherPx, out _);

    private static bool TryBuildFootprintOverlayTexture(Texture2D sourceTexture, RectInt safeInteriorBounds, AutoEyePatchDarkCandidate candidate, Color fillColor, Vector2? targetCenterOverride, bool allowAdaptiveInflation, out Texture2D overlay, out RectInt finalBoundsPx, out float featherPx)
        => TryBuildFootprintOverlayTexture(sourceTexture, safeInteriorBounds, candidate, fillColor, targetCenterOverride, allowAdaptiveInflation, out overlay, out finalBoundsPx, out featherPx, out _);

    private static bool TryBuildFootprintOverlayTexture(Texture2D sourceTexture, RectInt safeInteriorBounds, AutoEyePatchDarkCandidate candidate, Color fillColor, Vector2? targetCenterOverride, bool allowAdaptiveInflation, out Texture2D overlay, out RectInt finalBoundsPx, out float featherPx, out Color[] overlayPixels)
    {
        overlay = null;
        finalBoundsPx = default;
        featherPx = 0f;
        overlayPixels = null;

        if (sourceTexture == null || candidate == null || candidate.CroppedAlpha == null || candidate.CroppedAlpha.Length == 0 || candidate.CroppedAlphaWidth <= 0 || candidate.CroppedAlphaHeight <= 0)
            return false;

        try
        {
            int width = sourceTexture.width;
            int height = sourceTexture.height;
            int outlineBufferPx = 0;
            RectInt clampedSafeInterior = ShrinkRect(safeInteriorBounds, outlineBufferPx, width, height);
            if (clampedSafeInterior.width <= 0 || clampedSafeInterior.height <= 0)
                return false;
            if (!AutoEyePatchAnalyzer.TryReadPixels(sourceTexture, out Color[] sourcePixels))
                return false;

            int reference = Mathf.Max(1, Mathf.Min(safeInteriorBounds.width, safeInteriorBounds.height));
            int outlineThicknessPx = allowAdaptiveInflation
                ? Mathf.Clamp(Mathf.RoundToInt(reference * 0.035f), 1, 4)
                : Mathf.Clamp(Mathf.RoundToInt(reference * 0.060f), 3, 8);
            int outlineSearchDepthPx = allowAdaptiveInflation
                ? Mathf.Clamp(outlineThicknessPx + 1, outlineThicknessPx, 6)
                : Mathf.Clamp(outlineThicknessPx + 4, outlineThicknessPx + 2, 14);
            int fringeThicknessPx = allowAdaptiveInflation
                ? Mathf.Clamp(Mathf.RoundToInt(outlineThicknessPx * 0.75f), 1, 3)
                : Mathf.Clamp(Mathf.RoundToInt(outlineThicknessPx * 0.50f), 1, 4);
            BuildContourProtectionData(sourcePixels, width, height, outlineThicknessPx, outlineSearchDepthPx, preferInsetContour: !allowAdaptiveInflation, out bool[] opaqueMask, out int[] edgeDistance, out bool[] protectedOutlineMask);

            float inflateX = 1f;
            float inflateY = 1f;
            if (allowAdaptiveInflation)
            {
                ComputeAdaptiveFootprintInflation(candidate.FootprintPixels, opaqueMask, protectedOutlineMask, width, height, out inflateX, out inflateY);
                const float overshootRetention = 0.10f;
                inflateX = 1f + (Mathf.Max(0f, inflateX - 1f) * overshootRetention);
                inflateY = 1f + (Mathf.Max(0f, inflateY - 1f) * overshootRetention);
            }

            Vector2 targetCenter = targetCenterOverride ?? candidate.BoundsPx.center;
            if (!allowAdaptiveInflation)
                FitSideFootprintToContour(candidate, sourcePixels, opaqueMask, edgeDistance, protectedOutlineMask, width, height, outlineThicknessPx, fringeThicknessPx, ref targetCenter, ref inflateX, ref inflateY);

            featherPx = 0f;

            Vector2 sourceCenter = candidate.BoundsPx.center;
            int xShift = Mathf.RoundToInt(targetCenter.x - sourceCenter.x);
            int yShift = Mathf.RoundToInt(targetCenter.y - sourceCenter.y);

            float influenceX = inflateX + featherPx;
            float influenceY = inflateY + featherPx;
            int shiftedMinCandidateX = candidate.BoundsPx.xMin + xShift;
            int shiftedMaxCandidateX = candidate.BoundsPx.xMax + xShift;
            int minX = Mathf.Max(clampedSafeInterior.xMin, Mathf.FloorToInt(shiftedMinCandidateX - influenceX));
            int maxX = Mathf.Min(clampedSafeInterior.xMax, Mathf.CeilToInt(shiftedMaxCandidateX + influenceX));
            int shiftedMinCandidateY = candidate.BoundsPx.yMin + yShift;
            int shiftedMaxCandidateY = candidate.BoundsPx.yMax + yShift;
            int minY = Mathf.Max(clampedSafeInterior.yMin, Mathf.FloorToInt(shiftedMinCandidateY - influenceY));
            int maxY = Mathf.Min(clampedSafeInterior.yMax, Mathf.CeilToInt(shiftedMaxCandidateY + influenceY));
            if (minX >= maxX || minY >= maxY)
                return false;

            Texture2D runtimeOverlay = new(width, height, TextureFormat.RGBA32, false);
            runtimeOverlay.filterMode = FilterMode.Bilinear;
            Color[] colors = new Color[width * height];
            bool wroteAny = false;
            int finalMinX = width;
            int finalMinY = height;
            int finalMaxX = -1;
            int finalMaxY = -1;

            for (int y = minY; y < maxY; y++)
            {
                for (int x = minX; x < maxX; x++)
                {
                    int sourceX = x - xShift;
                    if (sourceX < 0 || sourceX >= width)
                        continue;

                    int sourceY = y - yShift;
                    if (sourceY < 0 || sourceY >= height)
                        continue;

                    float localFeatherPx = featherPx * ComputeProtectedOutlineFeatherScale(protectedOutlineMask, width, height, x, y, Mathf.Clamp(Mathf.CeilToInt(featherPx) + 1, 1, 4));
                    float alpha = ComputeExpandedFootprintAlpha(candidate, sourceX, sourceY, inflateX, inflateY, localFeatherPx);
                    if (alpha <= 0f)
                        continue;

                    float contourMultiplier = allowAdaptiveInflation
                        ? ComputeContourAlphaMultiplier(sourcePixels, opaqueMask, edgeDistance, protectedOutlineMask, width, height, x, y, outlineThicknessPx, fringeThicknessPx, strictEdgeBand: false)
                        : ComputeOutlineSubtractionMultiplierAtPixel(sourcePixels, opaqueMask, edgeDistance, protectedOutlineMask, width, height, x, y, outlineThicknessPx, fringeThicknessPx);

                    if (!allowAdaptiveInflation && contourMultiplier < 0.999f)
                        continue;

                    alpha *= contourMultiplier;
                    if (alpha <= 0.001f)
                        continue;

                    int index = (y * width) + x;
                    Color overlayColor = ResolveCamouflageOverlayColor(sourcePixels, width, height, x, y, fillColor);
                    colors[index] = new Color(overlayColor.r, overlayColor.g, overlayColor.b, alpha);
                    wroteAny = true;
                    if (x < finalMinX) finalMinX = x;
                    if (y < finalMinY) finalMinY = y;
                    if (x > finalMaxX) finalMaxX = x;
                    if (y > finalMaxY) finalMaxY = y;
                }
            }

            if (!wroteAny)
            {
                UnityEngine.Object.Destroy(runtimeOverlay);
                return false;
            }

            ApplyOpaqueColorBlur(colors, width, height, iterations: 2, blendStrength: 0.60f);
            BleedTransparentPixels(colors, width, height, 1);
            FloodTransparentPixels(colors, fillColor);
            runtimeOverlay.wrapMode = TextureWrapMode.Clamp;
            runtimeOverlay.SetPixels(colors, 0);
            runtimeOverlay.Apply(false, false);
            overlay = runtimeOverlay;
            overlayPixels = colors;
            finalBoundsPx = new RectInt(finalMinX, finalMinY, (finalMaxX - finalMinX) + 1, (finalMaxY - finalMinY) + 1);
            return true;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable] Failed to build auto eye patch texture: {e}");
            if (overlay != null)
            {
                UnityEngine.Object.Destroy(overlay);
                overlay = null;
            }

            return false;
        }
    }


    private static Color ResolveCamouflageOverlayColor(Color[] sourcePixels, int width, int height, int x, int y, Color fallbackColor)
    {
        if (sourcePixels == null || sourcePixels.Length != width * height || width <= 0 || height <= 0)
            return fallbackColor;

        if (x < 0 || x >= width || y < 0 || y >= height)
            return fallbackColor;

        Color donor = sourcePixels[(y * width) + x];
        if (donor.a <= 0.10f)
            return fallbackColor;

        if (ComputeLuma(donor) <= CamouflageDonorLumaThreshold)
            return fallbackColor;

        donor.a = 1f;
        return donor;
    }


    private static Color ResolveOverlayDonorColor(Color[] sourcePixels, int width, int height, AutoEyePatchDarkCandidate candidate, Vector2 sourceCenter, float sampleX, float sampleY, Color fallbackColor)
    {
        if (sourcePixels == null || sourcePixels.Length != width * height || candidate == null)
            return fallbackColor;

        const float exitThreshold = 0.05f;
        if (ComputeFootprintAlpha(candidate, sampleX, sampleY) <= exitThreshold)
            return SamplePixelsBilinear(sourcePixels, width, height, sampleX, sampleY, fallbackColor);

        float clampedX = Mathf.Clamp(sampleX, 0f, Mathf.Max(0f, width - 1));
        float clampedY = Mathf.Clamp(sampleY, 0f, Mathf.Max(0f, height - 1));
        float bestDistanceSq = float.MaxValue;
        float bestX = clampedX;
        float bestY = clampedY;
        bool found = false;

        int maxRadius = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(candidate.BoundsPx.width, candidate.BoundsPx.height) + 3f), 2, Mathf.Max(width, height));
        int centerX = Mathf.RoundToInt(clampedX);
        int centerY = Mathf.RoundToInt(clampedY);

        for (int radius = 1; radius <= maxRadius; radius++)
        {
            bool foundAtRadius = false;
            int minX = centerX - radius;
            int maxX = centerX + radius;
            int minY = centerY - radius;
            int maxY = centerY + radius;

            for (int py = minY; py <= maxY; py++)
            {
                for (int px = minX; px <= maxX; px++)
                {
                    if (px != minX && px != maxX && py != minY && py != maxY)
                        continue;
                    if (px < 0 || px >= width || py < 0 || py >= height)
                        continue;

                    float probeX = px + 0.5f;
                    float probeY = py + 0.5f;
                    if (ComputeFootprintAlpha(candidate, probeX, probeY) > exitThreshold)
                        continue;

                    float dx = probeX - clampedX;
                    float dy = probeY - clampedY;
                    float distanceSq = (dx * dx) + (dy * dy);
                    if (distanceSq >= bestDistanceSq)
                        continue;

                    bestDistanceSq = distanceSq;
                    bestX = probeX;
                    bestY = probeY;
                    found = true;
                    foundAtRadius = true;
                }
            }

            if (foundAtRadius)
                break;
        }

        if (found)
            return SamplePixelsBilinear(sourcePixels, width, height, bestX, bestY, fallbackColor);

        Vector2 fallbackDirection = new(clampedX - sourceCenter.x, clampedY - sourceCenter.y);
        if (fallbackDirection.sqrMagnitude < 0.0001f)
            fallbackDirection = new Vector2(0f, -1f);
        else
            fallbackDirection.Normalize();

        const float stepSize = 0.5f;
        float maxDistance = Mathf.Max(width, height);
        for (float distance = stepSize; distance <= maxDistance; distance += stepSize)
        {
            float probeX = clampedX + (fallbackDirection.x * distance);
            float probeY = clampedY + (fallbackDirection.y * distance);
            if (probeX < 0f || probeX > width - 1 || probeY < 0f || probeY > height - 1)
                break;

            if (ComputeFootprintAlpha(candidate, probeX, probeY) <= exitThreshold)
                return SamplePixelsBilinear(sourcePixels, width, height, probeX, probeY, fallbackColor);
        }

        return fallbackColor;
    }

    private static bool IsInsideCandidateFootprint(AutoEyePatchDarkCandidate candidate, float x, float y)
        => ComputeFootprintAlpha(candidate, x, y) > 0.05f;

    private static Color SamplePixelsBilinear(Color[] pixels, int width, int height, float x, float y, Color fallbackColor)
    {
        if (pixels == null || pixels.Length != width * height || width <= 0 || height <= 0)
            return fallbackColor;

        x = Mathf.Clamp(x, 0f, Mathf.Max(0f, width - 1));
        y = Mathf.Clamp(y, 0f, Mathf.Max(0f, height - 1));

        int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, width - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, height - 1);
        int x1 = Mathf.Clamp(x0 + 1, 0, width - 1);
        int y1 = Mathf.Clamp(y0 + 1, 0, height - 1);

        float tx = Mathf.Clamp01(x - x0);
        float ty = Mathf.Clamp01(y - y0);

        Color c00 = pixels[(y0 * width) + x0];
        Color c10 = pixels[(y0 * width) + x1];
        Color c01 = pixels[(y1 * width) + x0];
        Color c11 = pixels[(y1 * width) + x1];

        Color top = Color.Lerp(c00, c10, tx);
        Color bottom = Color.Lerp(c01, c11, tx);
        Color result = Color.Lerp(top, bottom, ty);
        result.a = 1f;
        return result;
    }

    private static void ComputeAdaptiveFootprintInflation(List<Vector2Int> footprintPixels, bool[] opaqueMask, bool[] protectedOutlineMask, int width, int height, out float inflateX, out float inflateY)
    {
        inflateX = 1f;
        inflateY = 1f;

        if (footprintPixels == null || footprintPixels.Count == 0 || opaqueMask == null || protectedOutlineMask == null)
            return;

        List<int> clearanceX = GetScratchIntListA();
        List<int> clearanceY = GetScratchIntListB();
        if (clearanceX.Capacity < footprintPixels.Count)
            clearanceX.Capacity = footprintPixels.Count;
        if (clearanceY.Capacity < footprintPixels.Count)
            clearanceY.Capacity = footprintPixels.Count;

        for (int i = 0; i < footprintPixels.Count; i++)
        {
            Vector2Int footprint = footprintPixels[i];
            if (footprint.x < 0 || footprint.x >= width || footprint.y < 0 || footprint.y >= height)
                continue;

            int localX = Mathf.Min(
                MeasureSafeRun(footprint.x, footprint.y, -1, 0, opaqueMask, protectedOutlineMask, width, height),
                MeasureSafeRun(footprint.x, footprint.y, 1, 0, opaqueMask, protectedOutlineMask, width, height));
            int localY = Mathf.Min(
                MeasureSafeRun(footprint.x, footprint.y, 0, -1, opaqueMask, protectedOutlineMask, width, height),
                MeasureSafeRun(footprint.x, footprint.y, 0, 1, opaqueMask, protectedOutlineMask, width, height));

            clearanceX.Add(localX);
            clearanceY.Add(localY);
        }

        if (clearanceX.Count > 0)
            inflateX = Mathf.Max(1f, ResolveRobustMedianClearance(clearanceX));
        if (clearanceY.Count > 0)
            inflateY = Mathf.Max(1f, ResolveRobustMedianClearance(clearanceY));
    }

    private static int MeasureSafeRun(int startX, int startY, int stepX, int stepY, bool[] opaqueMask, bool[] protectedOutlineMask, int width, int height)
    {
        int run = 0;
        int x = startX + stepX;
        int y = startY + stepY;

        while (x >= 0 && x < width && y >= 0 && y < height)
        {
            int index = (y * width) + x;
            if (!opaqueMask[index] || protectedOutlineMask[index])
                break;

            run++;
            x += stepX;
            y += stepY;
        }

        return run;
    }

    private static float ResolveRobustMedianClearance(List<int> clearances)
    {
        if (clearances == null || clearances.Count == 0)
            return 1f;

        clearances.Sort();
        int mid = clearances.Count / 2;
        if ((clearances.Count & 1) == 0)
            return (clearances[mid - 1] + clearances[mid]) * 0.5f;

        return clearances[mid];
    }


    private static void FitSideFootprintToContour(AutoEyePatchDarkCandidate candidate, Color[] contourPixels, bool[] opaqueMask, int[] edgeDistance, bool[] protectedOutlineMask, int width, int height, int outlineThicknessPx, int fringeThicknessPx, ref Vector2 targetCenter, ref float inflateX, ref float inflateY)
    {
        if (candidate == null || contourPixels == null || opaqueMask == null || edgeDistance == null || protectedOutlineMask == null)
            return;

        float baseHalfX = Mathf.Max(1f, candidate.BoundsPx.width * 0.5f * inflateX);
        float baseHalfY = Mathf.Max(1f, candidate.BoundsPx.height * 0.5f * inflateY);
        int anchorX = Mathf.Clamp(Mathf.RoundToInt(targetCenter.x), 0, width - 1);
        int anchorY = Mathf.Clamp(Mathf.RoundToInt(targetCenter.y), 0, height - 1);

        float[] scaleXCandidates = { 1.00f, 0.97f, 0.94f };
        float[] scaleYCandidates = { 1.00f, 0.96f, 0.92f, 0.88f, 0.84f, 0.80f, 0.76f, 0.72f, 0.68f };

        float bestScore = float.NegativeInfinity;
        float bestScaleX = 1f;
        float bestScaleY = 1f;
        int bestCenterY = anchorY;

        for (int yOffset = -4; yOffset <= 4; yOffset++)
        {
            int testCenterY = Mathf.Clamp(anchorY + yOffset, 0, height - 1);

            for (int sxIndex = 0; sxIndex < scaleXCandidates.Length; sxIndex++)
            {
                float scaleX = scaleXCandidates[sxIndex];
                float halfSpanX = Mathf.Max(1f, baseHalfX * scaleX);

                for (int syIndex = 0; syIndex < scaleYCandidates.Length; syIndex++)
                {
                    float scaleY = scaleYCandidates[syIndex];
                    float halfSpanY = Mathf.Max(1f, baseHalfY * scaleY);

                    float projectedCoverage = ComputeProjectedOutlineSubtractionMultiplier(
                        contourPixels,
                        opaqueMask,
                        edgeDistance,
                        protectedOutlineMask,
                        width,
                        height,
                        anchorX,
                        testCenterY,
                        halfSpanX,
                        halfSpanY,
                        outlineThicknessPx,
                        fringeThicknessPx);

                    int guardX = Mathf.Max(1, Mathf.CeilToInt(halfSpanX * 0.92f));
                    int guardY = Mathf.Max(1, Mathf.CeilToInt(halfSpanY * 0.92f));
                    bool touchesForbidden = TouchesForbiddenPaintZone(
                        contourPixels,
                        opaqueMask,
                        edgeDistance,
                        protectedOutlineMask,
                        width,
                        height,
                        anchorX,
                        testCenterY,
                        guardX,
                        guardY,
                        outlineThicknessPx,
                        fringeThicknessPx);

                    float score = projectedCoverage;
                    score -= Mathf.Abs(yOffset) * 0.0125f;
                    score -= (1f - scaleX) * 0.06f;
                    score -= (1f - scaleY) * 0.12f;
                    if (touchesForbidden)
                        score -= 0.60f;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestScaleX = scaleX;
                        bestScaleY = scaleY;
                        bestCenterY = testCenterY;
                    }
                }
            }
        }

        targetCenter = new Vector2(targetCenter.x, bestCenterY);
        inflateX *= bestScaleX;
        inflateY *= bestScaleY;
    }

    private static float ComputeFootprintAlpha(List<Vector2Int> footprintPixels, int x, int y, float inflateX, float inflateY, float featherPx)
    {
        float bestDistance = float.MaxValue;
        float sampleX = x + 0.5f;
        float sampleY = y + 0.5f;

        for (int i = 0; i < footprintPixels.Count; i++)
        {
            Vector2Int footprint = footprintPixels[i];
            float dx = Mathf.Abs(sampleX - (footprint.x + 0.5f)) / inflateX;
            float dy = Mathf.Abs(sampleY - (footprint.y + 0.5f)) / inflateY;
            float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
            if (distance < bestDistance)
                bestDistance = distance;

            if (bestDistance <= 1f)
                break;
        }

        if (bestDistance == float.MaxValue)
            return 0f;
        if (bestDistance <= 1f)
            return 1f;

        float featherNormalized = Mathf.Max(0.01f, featherPx / Mathf.Max(inflateX, inflateY));
        float t = Mathf.Clamp01((bestDistance - 1f) / featherNormalized);
        return 1f - t;
    }

    private static float ComputeFootprintAlpha(AutoEyePatchDarkCandidate candidate, float sourceX, float sourceY)
    {
        if (candidate == null || candidate.CroppedAlpha == null || candidate.CroppedAlpha.Length == 0 || candidate.CroppedAlphaWidth <= 0 || candidate.CroppedAlphaHeight <= 0)
            return 0f;

        float localX = sourceX - candidate.BoundsPx.xMin;
        float localY = sourceY - candidate.BoundsPx.yMin;
        int sampleX = Mathf.RoundToInt(localX - 0.5f);
        int sampleY = Mathf.RoundToInt(localY - 0.5f);

        if (sampleX < 0 || sampleY < 0 || sampleX >= candidate.CroppedAlphaWidth || sampleY >= candidate.CroppedAlphaHeight)
            return 0f;

        return candidate.CroppedAlpha[(sampleY * candidate.CroppedAlphaWidth) + sampleX] > 0.5f ? 1f : 0f;
    }

    private static float ComputeProtectedOutlineFeatherScale(bool[] protectedOutlineMask, int width, int height, int x, int y, int radius)
    {
        if (protectedOutlineMask == null || protectedOutlineMask.Length != width * height || radius <= 0)
            return 1f;

        float bestDistance = float.MaxValue;
        bool foundProtected = false;

        for (int dy = -radius; dy <= radius; dy++)
        {
            int py = y + dy;
            if (py < 0 || py >= height)
                continue;

            for (int dx = -radius; dx <= radius; dx++)
            {
                int px = x + dx;
                if (px < 0 || px >= width)
                    continue;
                if (!protectedOutlineMask[(py * width) + px])
                    continue;

                foundProtected = true;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                if (distance < bestDistance)
                    bestDistance = distance;
            }
        }

        if (!foundProtected)
            return 1f;

        float t = Mathf.Clamp01(bestDistance / Mathf.Max(1f, radius));
        return Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, t));
    }

    private static float ComputeOutwardFeatheredAlpha(float bestDistance, float featherNormalized)
    {
        if (bestDistance == float.MaxValue)
            return 0f;
        if (bestDistance <= 1f)
            return 1f;
        if (featherNormalized <= 0.001f)
            return 0f;

        float t = Mathf.Clamp01((bestDistance - 1f) / featherNormalized);
        const float fullOpacityUntil = 0.65f;
        if (t <= fullOpacityUntil)
            return 1f;

        float fadeT = Mathf.InverseLerp(fullOpacityUntil, 1f, t);
        float hardened = 1f - Mathf.SmoothStep(0f, 1f, fadeT);
        return Mathf.Clamp01(hardened);
    }

    private static float ComputeExpandedFootprintAlpha(AutoEyePatchDarkCandidate candidate, int sourceX, int sourceY, float inflateX, float inflateY, float featherPx)
    {
        if (candidate == null || candidate.CroppedAlpha == null || candidate.CroppedAlpha.Length == 0 || candidate.CroppedAlphaWidth <= 0 || candidate.CroppedAlphaHeight <= 0)
            return 0f;

        float baseAlpha = ComputeFootprintAlpha(candidate, sourceX, sourceY);
        if (baseAlpha > 0.001f)
            return Mathf.Clamp01(baseAlpha);

        float sampleX = (sourceX - candidate.BoundsPx.xMin) + 0.5f;
        float sampleY = (sourceY - candidate.BoundsPx.yMin) + 0.5f;
        float bestDistance = float.MaxValue;

        for (int y = 0; y < candidate.CroppedAlphaHeight; y++)
        {
            int row = y * candidate.CroppedAlphaWidth;
            for (int x = 0; x < candidate.CroppedAlphaWidth; x++)
            {
                if (candidate.CroppedAlpha[row + x] <= 0.001f)
                    continue;

                float dx = Mathf.Abs(sampleX - (x + 0.5f)) / Mathf.Max(0.01f, inflateX);
                float dy = Mathf.Abs(sampleY - (y + 0.5f)) / Mathf.Max(0.01f, inflateY);
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                if (distance < bestDistance)
                    bestDistance = distance;
            }
        }

        float featherNormalized = featherPx / Mathf.Max(0.01f, Mathf.Max(inflateX, inflateY));
        return ComputeOutwardFeatheredAlpha(bestDistance, featherNormalized);
    }

    private static void FloodTransparentPixels(Color[] pixels, Color fillColor)
    {
        if (pixels == null || pixels.Length == 0)
            return;

        Color flood = new(fillColor.r, fillColor.g, fillColor.b, 0f);
        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i].a <= 0.001f)
                pixels[i] = flood;
        }
    }

    private static void ApplyOpaqueColorBlur(Color[] pixels, int width, int height, int iterations, float blendStrength)
    {
        if (pixels == null || pixels.Length != width * height || iterations <= 0 || blendStrength <= 0.001f)
            return;

        blendStrength = Mathf.Clamp01(blendStrength);
        Color[] working = new Color[pixels.Length];
        Color[] scratch = new Color[pixels.Length];
        Array.Copy(pixels, working, pixels.Length);

        for (int pass = 0; pass < iterations; pass++)
        {
            Array.Copy(working, scratch, working.Length);
            bool changed = false;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    Color current = working[index];
                    if (current.a <= 0.001f)
                        continue;

                    float totalWeight = 0f;
                    float totalR = 0f;
                    float totalG = 0f;
                    float totalB = 0f;

                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                                continue;

                            Color neighbor = working[(ny * width) + nx];
                            if (neighbor.a <= 0.001f)
                                continue;

                            float kernelWeight = dx == 0 && dy == 0
                                ? 4f
                                : (dx == 0 || dy == 0 ? 2f : 1f);
                            float weight = kernelWeight * neighbor.a;
                            totalR += neighbor.r * weight;
                            totalG += neighbor.g * weight;
                            totalB += neighbor.b * weight;
                            totalWeight += weight;
                        }
                    }

                    if (totalWeight <= 0.0001f)
                        continue;

                    Color blurred = new(totalR / totalWeight, totalG / totalWeight, totalB / totalWeight, current.a);
                    scratch[index] = new Color(
                        Mathf.Lerp(current.r, blurred.r, blendStrength),
                        Mathf.Lerp(current.g, blurred.g, blendStrength),
                        Mathf.Lerp(current.b, blurred.b, blendStrength),
                        current.a);
                    changed = true;
                }
            }

            Array.Copy(scratch, working, working.Length);
            if (!changed)
                break;
        }

        Array.Copy(working, pixels, pixels.Length);
    }

    private static void ApplyCutoutMipChain(Texture2D texture, Color[] basePixels, int width, int height, Color fillColor)
    {
        if (texture == null || basePixels == null || basePixels.Length != width * height)
            return;

        texture.SetPixels(basePixels, 0);
        int mipCount = texture.mipmapCount;
        if (mipCount <= 1)
        {
            texture.Apply(false, false);
            return;
        }

        Color fill = new(fillColor.r, fillColor.g, fillColor.b, 1f);
        Color clearFill = new(fillColor.r, fillColor.g, fillColor.b, 0f);
        Color[] current = new Color[basePixels.Length];
        Array.Copy(basePixels, current, basePixels.Length);
        int currentWidth = width;
        int currentHeight = height;

        for (int level = 1; level < mipCount; level++)
        {
            int nextWidth = Mathf.Max(1, currentWidth >> 1);
            int nextHeight = Mathf.Max(1, currentHeight >> 1);
            Color[] next = new Color[nextWidth * nextHeight];

            for (int y = 0; y < nextHeight; y++)
            {
                int srcY0 = Mathf.Min(currentHeight - 1, y << 1);
                int srcY1 = Mathf.Min(currentHeight - 1, srcY0 + 1);
                for (int x = 0; x < nextWidth; x++)
                {
                    int srcX0 = Mathf.Min(currentWidth - 1, x << 1);
                    int srcX1 = Mathf.Min(currentWidth - 1, srcX0 + 1);

                    bool anyOpaque = false;
                    float maxAlpha = 0f;

                    Color c00 = current[(srcY0 * currentWidth) + srcX0];
                    if (c00.a > 0.001f)
                    {
                        anyOpaque = true;
                        if (c00.a > maxAlpha) maxAlpha = c00.a;
                    }

                    Color c10 = current[(srcY0 * currentWidth) + srcX1];
                    if (c10.a > 0.001f)
                    {
                        anyOpaque = true;
                        if (c10.a > maxAlpha) maxAlpha = c10.a;
                    }

                    Color c01 = current[(srcY1 * currentWidth) + srcX0];
                    if (c01.a > 0.001f)
                    {
                        anyOpaque = true;
                        if (c01.a > maxAlpha) maxAlpha = c01.a;
                    }

                    Color c11 = current[(srcY1 * currentWidth) + srcX1];
                    if (c11.a > 0.001f)
                    {
                        anyOpaque = true;
                        if (c11.a > maxAlpha) maxAlpha = c11.a;
                    }

                    next[(y * nextWidth) + x] = anyOpaque
                        ? new Color(fill.r, fill.g, fill.b, maxAlpha)
                        : clearFill;
                }
            }

            texture.SetPixels(next, level);
            current = next;
            currentWidth = nextWidth;
            currentHeight = nextHeight;
        }

        texture.Apply(false, false);
    }

    private static void BleedTransparentPixels(Color[] pixels, int width, int height, int iterations)
    {
        if (pixels == null || pixels.Length != width * height || iterations <= 0)
            return;

        Color[] working = new Color[pixels.Length];
        Color[] scratch = new Color[pixels.Length];
        Array.Copy(pixels, working, pixels.Length);

        for (int pass = 0; pass < iterations; pass++)
        {
            Array.Copy(working, scratch, working.Length);
            bool changed = false;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * width) + x;
                    if (working[index].a > 0.001f)
                        continue;

                    float totalR = 0f;
                    float totalG = 0f;
                    float totalB = 0f;
                    float totalWeight = 0f;

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

                            Color neighbor = working[(ny * width) + nx];
                            if (neighbor.a <= 0.001f)
                                continue;

                            float weight = neighbor.a * ((dx == 0 || dy == 0) ? 1f : 0.70710677f);
                            totalR += neighbor.r * weight;
                            totalG += neighbor.g * weight;
                            totalB += neighbor.b * weight;
                            totalWeight += weight;
                        }
                    }

                    if (totalWeight <= 0.0001f)
                        continue;

                    scratch[index] = new Color(totalR / totalWeight, totalG / totalWeight, totalB / totalWeight, 0f);
                    changed = true;
                }
            }

            Array.Copy(scratch, working, working.Length);
            if (!changed)
                break;
        }

        Array.Copy(working, pixels, pixels.Length);
    }

    private static RectInt ShrinkRect(RectInt rect, int inset, int maxWidth, int maxHeight)
    {
        int xMin = Mathf.Clamp(rect.xMin + inset, 0, maxWidth);
        int yMin = Mathf.Clamp(rect.yMin + inset, 0, maxHeight);
        int xMax = Mathf.Clamp(rect.xMax - inset, xMin, maxWidth);
        int yMax = Mathf.Clamp(rect.yMax - inset, yMin, maxHeight);
        return new RectInt(xMin, yMin, Mathf.Max(0, xMax - xMin), Mathf.Max(0, yMax - yMin));
    }

    private static AutoEyePatchHeadStatus ResolveHeadStatus(AutoEyePatchVariantResult south, AutoEyePatchVariantResult east, AutoEyePatchVariantResult west)
    {
        int generated = 0;
        if (south != null && south.Status == AutoEyePatchVariantStatus.Generated) generated++;
        if (east != null && east.Status == AutoEyePatchVariantStatus.Generated) generated++;
        if (west != null && west.Status == AutoEyePatchVariantStatus.Generated) generated++;
        if (generated == 3)
            return AutoEyePatchHeadStatus.Generated;
        if (generated > 0)
            return AutoEyePatchHeadStatus.Partial;
        return AutoEyePatchHeadStatus.Skipped;
    }

    private static float ResolveHeadConfidence(AutoEyePatchVariantResult south, AutoEyePatchVariantResult east, AutoEyePatchVariantResult west)
    {
        float total = 0f;
        int count = 0;
        if (south?.Primary != null) { total += south.Primary.Confidence; count++; }
        if (south?.Secondary != null) { total += south.Secondary.Confidence; count++; }
        if (east?.Primary != null) { total += east.Primary.Confidence; count++; }
        if (west?.Primary != null) { total += west.Primary.Confidence; count++; }
        return count > 0 ? total / count : 0f;
    }
}