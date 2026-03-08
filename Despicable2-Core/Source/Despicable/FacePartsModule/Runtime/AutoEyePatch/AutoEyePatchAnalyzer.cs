using System;
using System.Collections.Generic;
using UnityEngine;

// Guardrail-Reason: Auto eye patch texture analysis stays together because pixel probing, symmetry checks, and feature extraction share one cached pass.
namespace Despicable;

internal static class AutoEyePatchAnalyzer
{
    private static readonly Dictionary<Texture2D, Color[]> _pixelCache = new();

    public static void ResetRuntimeState() => _pixelCache.Clear();

    public static void InvalidateCachedPixels(Texture2D texture)
    {
        if (texture != null)
            _pixelCache.Remove(texture);
    }

    public static AutoEyePatchTextureAnalysis Analyze(string textureKey, string texturePath, Texture2D texture, bool sideMode)
    {
        AutoEyePatchTextureAnalysis analysis = new()
        {
            TextureKey = textureKey,
            TexturePath = texturePath,
            Width = texture != null ? texture.width : 0,
            Height = texture != null ? texture.height : 0,
            Version = AutoEyePatchRuntime.GenerationVersion,
        };

        if (texture == null || !TryReadPixels(texture, out Color[] pixels))
        {
            analysis.AnalysisReasons = texture == null ? AutoEyePatchSkipReason.TextureMissing : AutoEyePatchSkipReason.TextureUnreadable;
            return analysis;
        }

        analysis.OpaqueBounds = ComputeOpaqueBounds(pixels, texture.width, texture.height);
        if (analysis.OpaqueBounds.width <= 1 || analysis.OpaqueBounds.height <= 1)
        {
            analysis.AnalysisReasons = AutoEyePatchSkipReason.TextureUnreadable;
            return analysis;
        }

        analysis.SafeInteriorBounds = ComputeSafeInteriorBounds(texture.width, texture.height, analysis.OpaqueBounds);
        RectInt scanRect = sideMode ? GetSideScanRect(analysis.OpaqueBounds) : GetSouthScanRect(analysis.OpaqueBounds);
        analysis.Candidates = FindDarkCandidates(pixels, texture.width, texture.height, scanRect, sideMode);
        if (analysis.Candidates.Count == 0)
            analysis.AnalysisReasons |= AutoEyePatchSkipReason.NoDarkCandidate;

        return analysis;
    }

    internal static bool TryReadPixels(Texture2D texture, out Color[] pixels)
    {
        pixels = null;
        if (texture == null)
            return false;

        if (_pixelCache.TryGetValue(texture, out Color[] cached) && cached != null)
        {
            if (cached.Length == texture.width * texture.height)
            {
                pixels = cached;
                return true;
            }

            _pixelCache.Remove(texture);
        }

        try
        {
            pixels = texture.GetPixels();
            if (pixels != null && pixels.Length == texture.width * texture.height)
            {
                _pixelCache[texture] = pixels;
                return true;
            }
        }
        catch (Exception ex)
        {
            string textureName = texture != null ? texture.name : "<null>";
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "AutoEyePatchAnalyzer.TryGetReadablePixels.GetPixels:" + textureName,
                $"Auto eye patch analyzer could not read texture pixels directly for '{textureName}'. Falling back to RenderTexture copy.",
                ex);
        }

        RenderTexture previous = RenderTexture.active;
        RenderTexture rt = null;
        Texture2D readable = null;
        try
        {
            rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, false);
            readable.ReadPixels(new Rect(0f, 0f, texture.width, texture.height), 0, 0, false);
            readable.Apply(false, false);
            pixels = readable.GetPixels();
            if (pixels != null && pixels.Length == texture.width * texture.height)
            {
                _pixelCache[texture] = pixels;
                return true;
            }

            pixels = null;
            return false;
        }
        catch
        {
            pixels = null;
            return false;
        }
        finally
        {
            RenderTexture.active = previous;
            if (rt != null)
                RenderTexture.ReleaseTemporary(rt);
            if (readable != null)
                UnityEngine.Object.Destroy(readable);
        }
    }

    private static RectInt ComputeOpaqueBounds(Color[] pixels, int width, int height)
    {
        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c = pixels[(y * width) + x];
                if (!IsPixelOpaque(c))
                    continue;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
            return new RectInt(0, 0, width, height);

        return new RectInt(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
    }

    private static RectInt ComputeSafeInteriorBounds(int width, int height, RectInt opaqueBounds)
    {
        int inset = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(opaqueBounds.width, opaqueBounds.height) * 0.06f));
        int xMin = Mathf.Clamp(opaqueBounds.xMin + inset, 0, width - 1);
        int yMin = Mathf.Clamp(opaqueBounds.yMin + inset, 0, height - 1);
        int xMax = Mathf.Clamp(opaqueBounds.xMax - inset, xMin + 1, width);
        int yMax = Mathf.Clamp(opaqueBounds.yMax - inset, yMin + 1, height);
        return new RectInt(xMin, yMin, Mathf.Max(1, xMax - xMin), Mathf.Max(1, yMax - yMin));
    }

    private static List<AutoEyePatchDarkCandidate> FindDarkCandidates(Color[] pixels, int width, int height, RectInt scanRect, bool sideMode)
    {
        bool[] visited = new bool[width * height];
        List<AutoEyePatchDarkCandidate> results = new();
        Queue<Vector2Int> queue = new();
        int minPixelCount = Mathf.Max(2, Mathf.RoundToInt((scanRect.width * scanRect.height) * 0.0015f));
        int maxPixelCount = Mathf.Max(minPixelCount + 1, Mathf.RoundToInt((scanRect.width * scanRect.height) * 0.16f));

        for (int y = scanRect.yMin; y < scanRect.yMax; y++)
        {
            for (int x = scanRect.xMin; x < scanRect.xMax; x++)
            {
                int startIndex = (y * width) + x;
                if (visited[startIndex] || !IsPixelDark(pixels[startIndex]))
                    continue;

                visited[startIndex] = true;
                queue.Clear();
                queue.Enqueue(new Vector2Int(x, y));

                int count = 0;
                int minX = x;
                int minY = y;
                int maxX = x;
                int maxY = y;
                float darknessTotal = 0f;
                List<Vector2Int> footprintPixels = new();

                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    int idx = (current.y * width) + current.x;
                    Color c = pixels[idx];
                    count++;
                    darknessTotal += ComputeDarknessScore(c);
                    footprintPixels.Add(current);

                    if (current.x < minX) minX = current.x;
                    if (current.y < minY) minY = current.y;
                    if (current.x > maxX) maxX = current.x;
                    if (current.y > maxY) maxY = current.y;

                    for (int ny = current.y - 1; ny <= current.y + 1; ny++)
                    {
                        if (ny < scanRect.yMin || ny >= scanRect.yMax)
                            continue;
                        for (int nx = current.x - 1; nx <= current.x + 1; nx++)
                        {
                            if (nx < scanRect.xMin || nx >= scanRect.xMax)
                                continue;
                            if (nx == current.x && ny == current.y)
                                continue;
                            int nidx = (ny * width) + nx;
                            if (visited[nidx] || !IsPixelDark(pixels[nidx]))
                                continue;

                            visited[nidx] = true;
                            queue.Enqueue(new Vector2Int(nx, ny));
                        }
                    }
                }

                if (count < minPixelCount || count > maxPixelCount)
                    continue;

                RectInt bounds = new(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);

                // Eyes tend to be blob-like. Very elongated shapes are usually edge shadows, wrinkles, or other noise.
                float w = Mathf.Max(1f, bounds.width);
                float h = Mathf.Max(1f, bounds.height);
                float elongation = Mathf.Max(w / h, h / w);
                if (elongation > 4f)
                    continue;

                float compactness = ComputeCompactnessScore(bounds, count);
                float outlineSafety = ComputeSurroundingLightnessScore(pixels, width, height, footprintPixels);
                if (compactness < 0.12f || outlineSafety < 0.05f)
                    continue;

                BuildCroppedAlphaFromFootprint(width, height, bounds, footprintPixels, out float[] croppedAlpha, out int croppedWidth, out int croppedHeight);

                results.Add(new AutoEyePatchDarkCandidate
                {
                    BoundsPx = bounds,
                    CenterUV = new Vector2((bounds.center.x) / width, (bounds.center.y) / height),
                    RadiusUV = new Vector2(Mathf.Max(1f, bounds.width * 0.5f) / width, Mathf.Max(1f, bounds.height * 0.5f) / height),
                    DarknessScore = darknessTotal / Mathf.Max(1, count),
                    CompactnessScore = compactness,
                    OutlineSafetyScore = outlineSafety,
                    FootprintPixels = footprintPixels,
                    CroppedAlpha = croppedAlpha,
                    CroppedAlphaWidth = croppedWidth,
                    CroppedAlphaHeight = croppedHeight,
                });
            }
        }

        results.Sort((a, b) => ((b.DarknessScore * b.CompactnessScore * b.OutlineSafetyScore).CompareTo(a.DarknessScore * a.CompactnessScore * a.OutlineSafetyScore)));
        if (results.Count > 8)
            results.RemoveRange(8, results.Count - 8);

        if (!sideMode)
            results.Sort((a, b) => a.CenterUV.x.CompareTo(b.CenterUV.x));

        return results;
    }

    private static void BuildCroppedAlphaFromFootprint(int width, int height, RectInt bounds, List<Vector2Int> footprintPixels, out float[] croppedAlpha, out int croppedWidth, out int croppedHeight)
    {
        croppedWidth = Mathf.Max(1, bounds.width);
        croppedHeight = Mathf.Max(1, bounds.height);
        croppedAlpha = new float[croppedWidth * croppedHeight];

        if (footprintPixels == null || footprintPixels.Count == 0)
            return;

        for (int i = 0; i < footprintPixels.Count; i++)
        {
            Vector2Int pixel = footprintPixels[i];
            if (pixel.x < 0 || pixel.x >= width || pixel.y < 0 || pixel.y >= height)
                continue;

            int localX = pixel.x - bounds.xMin;
            int localY = pixel.y - bounds.yMin;
            if (localX < 0 || localX >= croppedWidth || localY < 0 || localY >= croppedHeight)
                continue;

            croppedAlpha[(localY * croppedWidth) + localX] = 1f;
        }

        MorphologicalClose(croppedAlpha, croppedWidth, croppedHeight, 1);
        FillEnclosedInterior(croppedAlpha, croppedWidth, croppedHeight);
    }

    private static void MorphologicalClose(float[] croppedAlpha, int width, int height, int radius)
    {
        if (croppedAlpha == null || croppedAlpha.Length != width * height || width <= 0 || height <= 0 || radius <= 0)
            return;

        float[] dilated = new float[croppedAlpha.Length];
        float[] closed = new float[croppedAlpha.Length];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool anyFilled = false;
                for (int ny = Mathf.Max(0, y - radius); ny <= Mathf.Min(height - 1, y + radius) && !anyFilled; ny++)
                {
                    int dy = ny - y;
                    int maxDx = radius - Mathf.Abs(dy);
                    for (int nx = Mathf.Max(0, x - maxDx); nx <= Mathf.Min(width - 1, x + maxDx); nx++)
                    {
                        if (croppedAlpha[(ny * width) + nx] > 0.5f)
                        {
                            anyFilled = true;
                            break;
                        }
                    }
                }

                if (anyFilled)
                    dilated[(y * width) + x] = 1f;
            }
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool allFilled = true;
                for (int ny = Mathf.Max(0, y - radius); ny <= Mathf.Min(height - 1, y + radius) && allFilled; ny++)
                {
                    int dy = ny - y;
                    int maxDx = radius - Mathf.Abs(dy);
                    for (int nx = Mathf.Max(0, x - maxDx); nx <= Mathf.Min(width - 1, x + maxDx); nx++)
                    {
                        if (dilated[(ny * width) + nx] <= 0.5f)
                        {
                            allFilled = false;
                            break;
                        }
                    }
                }

                if (allFilled)
                    closed[(y * width) + x] = 1f;
            }
        }

        for (int i = 0; i < croppedAlpha.Length; i++)
            croppedAlpha[i] = closed[i];
    }

    private static void FillEnclosedInterior(float[] croppedAlpha, int width, int height)
    {
        if (croppedAlpha == null || croppedAlpha.Length != width * height || width <= 0 || height <= 0)
            return;

        bool[] outside = new bool[croppedAlpha.Length];
        Queue<int> queue = new();

        void EnqueueIfOutsideSeed(int x, int y)
        {
            int index = (y * width) + x;
            if (outside[index] || croppedAlpha[index] > 0.5f)
                return;

            outside[index] = true;
            queue.Enqueue(index);
        }

        for (int x = 0; x < width; x++)
        {
            EnqueueIfOutsideSeed(x, 0);
            if (height > 1)
                EnqueueIfOutsideSeed(x, height - 1);
        }

        for (int y = 1; y < height - 1; y++)
        {
            EnqueueIfOutsideSeed(0, y);
            if (width > 1)
                EnqueueIfOutsideSeed(width - 1, y);
        }

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            int x = index % width;
            int y = index / width;

            for (int ny = Mathf.Max(0, y - 1); ny <= Mathf.Min(height - 1, y + 1); ny++)
            {
                for (int nx = Mathf.Max(0, x - 1); nx <= Mathf.Min(width - 1, x + 1); nx++)
                {
                    if (nx == x && ny == y)
                        continue;

                    int neighbor = (ny * width) + nx;
                    if (outside[neighbor] || croppedAlpha[neighbor] > 0.5f)
                        continue;

                    outside[neighbor] = true;
                    queue.Enqueue(neighbor);
                }
            }
        }

        for (int i = 0; i < croppedAlpha.Length; i++)
        {
            croppedAlpha[i] = outside[i] ? 0f : 1f;
        }
    }

    private static bool IsPixelOpaque(Color c) => c.a > 0.10f;
    private static bool IsPixelDark(Color c) => c.a > 0.10f && ComputeLuma(c) < 0.23f;
    private static float ComputeLuma(Color c) => (0.2126f * c.r) + (0.7152f * c.g) + (0.0722f * c.b);
    private static float ComputeDarknessScore(Color c) => Mathf.Clamp01(1f - ComputeLuma(c));

    private static float ComputeCompactnessScore(RectInt boundsPx, int filledPixels)
    {
        float area = Mathf.Max(1f, boundsPx.width * boundsPx.height);
        return Mathf.Clamp01(filledPixels / area);
    }

    private static float ComputeSurroundingLightnessScore(Color[] pixels, int width, int height, List<Vector2Int> footprintPixels)
    {
        if (pixels == null || footprintPixels == null || footprintPixels.Count == 0)
            return 0f;

        HashSet<int> footprint = new();
        for (int i = 0; i < footprintPixels.Count; i++)
            footprint.Add((footprintPixels[i].y * width) + footprintPixels[i].x);

        HashSet<int> border = new();
        float totalLuma = 0f;
        int samples = 0;

        for (int i = 0; i < footprintPixels.Count; i++)
        {
            Vector2Int pixel = footprintPixels[i];
            for (int ny = pixel.y - 1; ny <= pixel.y + 1; ny++)
            {
                if (ny < 0 || ny >= height)
                    continue;

                for (int nx = pixel.x - 1; nx <= pixel.x + 1; nx++)
                {
                    if (nx < 0 || nx >= width)
                        continue;
                    if (nx == pixel.x && ny == pixel.y)
                        continue;

                    int index = (ny * width) + nx;
                    if (footprint.Contains(index) || !border.Add(index))
                        continue;

                    Color c = pixels[index];
                    if (!IsPixelOpaque(c))
                        continue;

                    totalLuma += ComputeLuma(c);
                    samples++;
                }
            }
        }

        // Border samples scale roughly with perimeter while footprintPixels scales with area.
        // Using sqrt(area) prevents large, solid eye blobs (common in stylized heads) from always failing.
        int minSamples = Mathf.Max(6, Mathf.RoundToInt(Mathf.Sqrt(footprintPixels.Count) * 2.5f));
        minSamples = Mathf.Min(minSamples, 96);
        if (samples < minSamples)
            return 0f;

        float averageLuma = totalLuma / samples;
        return Mathf.Clamp01((averageLuma - 0.22f) / 0.45f);
    }

    private static RectInt GetSouthScanRect(RectInt opaqueBounds)
    {
        int xMin = opaqueBounds.xMin + Mathf.RoundToInt(opaqueBounds.width * 0.10f);
        int yMin = opaqueBounds.yMin + Mathf.RoundToInt(opaqueBounds.height * 0.28f);
        int width = Mathf.RoundToInt(opaqueBounds.width * 0.80f);
        // Slightly taller scan band to catch lower-set eyes on stylized/non-human heads.
        int height = Mathf.RoundToInt(opaqueBounds.height * 0.45f);
        return new RectInt(xMin, yMin, Mathf.Max(1, width), Mathf.Max(1, height));
    }

    private static RectInt GetSideScanRect(RectInt opaqueBounds)
    {
        int width = Mathf.RoundToInt(opaqueBounds.width * 0.36f);
        int xMax = opaqueBounds.xMax - Mathf.RoundToInt(opaqueBounds.width * 0.08f);
        int xMin = xMax - width;
        int yMin = opaqueBounds.yMin + Mathf.RoundToInt(opaqueBounds.height * 0.28f);
        // Match the south scan band height so side-view heads with lower-set eyes still generate correctly.
        int height = Mathf.RoundToInt(opaqueBounds.height * 0.45f);

        xMin = Mathf.Clamp(xMin, opaqueBounds.xMin, opaqueBounds.xMax - 1);
        if (xMin + width > opaqueBounds.xMax)
            width = Mathf.Max(1, opaqueBounds.xMax - xMin);

        return new RectInt(xMin, yMin, Mathf.Max(1, width), Mathf.Max(1, height));
    }
}
