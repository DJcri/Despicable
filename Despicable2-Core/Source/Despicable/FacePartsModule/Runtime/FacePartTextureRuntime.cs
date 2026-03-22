using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable;

internal static class FacePartTextureRuntime
{
    private const float BleedTargetAlphaMax = 0.40f;
    private const float BleedSourceAlphaMin = 0.40f;
    private const int BleedPasses = 2;

    private static readonly Dictionary<int, Texture2D> _preparedTexturesByKey = new();

    public static void ResetRuntimeState()
    {
        foreach (Texture2D texture in _preparedTexturesByKey.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        _preparedTexturesByKey.Clear();
    }

    public static Texture2D PrepareTexture(Texture2D source, bool bleedColor = true)
    {
        if (source == null)
            return null;

        ConfigureSampler(source);

        int key = unchecked((source.GetInstanceID() * 397) ^ (bleedColor ? 1 : 0));
        if (_preparedTexturesByKey.TryGetValue(key, out Texture2D cached) && cached != null)
            return cached;

        if (!AutoEyePatchAnalyzer.TryReadPixels(source, out Color[] pixels) || pixels == null || pixels.Length != source.width * source.height)
            return source;

        Color[] working = new Color[pixels.Length];
        Array.Copy(pixels, working, pixels.Length);
        if (bleedColor)
            BleedRgbIntoTransparentPixels(working, source.width, source.height);

        Texture2D prepared = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false)
        {
            name = string.Concat(source.name ?? "FacePart", "_D2Prepared"),
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0,
        };
        prepared.SetPixels(working);
        prepared.Apply(false, false);
        _preparedTexturesByKey[key] = prepared;
        return prepared;
    }

    public static void ConfigureSampler(Texture2D texture)
    {
        if (texture == null)
            return;

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.anisoLevel = 0;
    }

    public static void FinalizeRuntimeTexture(Texture2D texture, Color[] pixels, int width, int height, bool bleedColor = true)
    {
        if (texture == null || pixels == null || pixels.Length != width * height)
            return;

        if (bleedColor)
            BleedRgbIntoTransparentPixels(pixels, width, height);

        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.anisoLevel = 0;
        texture.SetPixels(pixels);
        texture.Apply(false, false);
    }

    public static bool TryResolvePreparedMultiFacingTextures(string graphicPath, Shader shader, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture, string warnKeyStem, string debugName)
    {
        southTexture = null;
        eastTexture = null;
        westTexture = null;

        if (graphicPath.NullOrEmpty())
            return false;

        try
        {
            Graphic graphic = GraphicDatabase.Get<Graphic_Multi>(graphicPath, shader, Vector2.one, Color.white);
            if (graphic != null)
            {
                southTexture = PrepareTexture(graphic.MatAt(Rot4.South)?.mainTexture as Texture2D);
                eastTexture = PrepareTexture(graphic.MatAt(Rot4.East)?.mainTexture as Texture2D);
                westTexture = PrepareTexture(graphic.MatAt(Rot4.West)?.mainTexture as Texture2D);
            }
        }
        catch (Exception ex)
        {
            string name = debugName ?? "<null>";
            string pathName = graphicPath ?? "<null>";
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                warnKeyStem + ".Graphic:" + name,
                $"Face-part texture runtime could not resolve GraphicDatabase textures for '{name}' at '{pathName}'.",
                ex);
        }

        try
        {
            southTexture ??= PrepareTexture(ContentFinder<Texture2D>.Get(graphicPath, false));
        }
        catch (Exception ex)
        {
            string name = debugName ?? "<null>";
            string pathName = graphicPath ?? "<null>";
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                warnKeyStem + ".ContentFinder:" + name,
                $"Face-part texture runtime could not resolve ContentFinder texture for '{name}' at '{pathName}'.",
                ex);
        }

        eastTexture ??= southTexture ?? westTexture;
        westTexture ??= eastTexture ?? southTexture;
        southTexture ??= eastTexture ?? westTexture;
        return southTexture != null || eastTexture != null || westTexture != null;
    }

    public static void BleedRgbIntoTransparentPixels(Color[] pixels, int width, int height)
    {
        if (pixels == null || pixels.Length != width * height || width <= 0 || height <= 0)
            return;

        Color[] src = new Color[pixels.Length];
        Color[] dst = new Color[pixels.Length];
        Array.Copy(pixels, src, pixels.Length);
        Array.Copy(pixels, dst, pixels.Length);

        for (int pass = 0; pass < BleedPasses; pass++)
        {
            bool changedAny = false;
            for (int y = 0; y < height; y++)
            {
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int index = row + x;
                    Color current = src[index];
                    if (current.a > BleedTargetAlphaMax)
                    {
                        dst[index] = current;
                        continue;
                    }

                    Vector3 rgbSum = Vector3.zero;
                    float alphaWeight = 0f;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        int ny = y + oy;
                        if (ny < 0 || ny >= height)
                            continue;

                        int neighborRow = ny * width;
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            if (ox == 0 && oy == 0)
                                continue;

                            int nx = x + ox;
                            if (nx < 0 || nx >= width)
                                continue;

                            Color neighbor = src[neighborRow + nx];
                            if (neighbor.a < BleedSourceAlphaMin)
                                continue;

                            rgbSum += new Vector3(neighbor.r, neighbor.g, neighbor.b) * neighbor.a;
                            alphaWeight += neighbor.a;
                        }
                    }

                    if (alphaWeight <= 0.0001f)
                    {
                        dst[index] = current;
                        continue;
                    }

                    Vector3 avg = rgbSum / alphaWeight;
                    float blend = 1f - Mathf.Clamp01(current.a / BleedTargetAlphaMax);
                    Color result = current;
                    result.r = Mathf.Lerp(current.r, avg.x, blend);
                    result.g = Mathf.Lerp(current.g, avg.y, blend);
                    result.b = Mathf.Lerp(current.b, avg.z, blend);
                    dst[index] = result;
                    changedAny = true;
                }
            }

            if (!changedAny)
                break;

            Color[] swap = src;
            src = dst;
            dst = swap;
        }

        Array.Copy(src, pixels, pixels.Length);
    }
}
