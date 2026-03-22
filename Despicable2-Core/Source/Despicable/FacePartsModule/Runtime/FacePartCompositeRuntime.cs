using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable;

internal static class FacePartCompositeRuntime
{
    private static readonly Dictionary<string, Texture2D> _runtimeTexturesByKey = new();

    public static void ResetRuntimeState()
    {
        foreach (Texture2D texture in _runtimeTexturesByKey.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        _runtimeTexturesByKey.Clear();
    }

    public static bool TryResolveCompositeTexture(string baseTexPath, string overlayTexPath, out Texture2D runtimeTexture)
    {
        runtimeTexture = null;

        Texture2D baseTexture = ResolveTexture(baseTexPath);
        Texture2D overlayTexture = ResolveTexture(overlayTexPath);
        if (baseTexture == null && overlayTexture == null)
            return false;

        if (baseTexture == null)
        {
            runtimeTexture = overlayTexture;
            return runtimeTexture != null;
        }

        if (overlayTexture == null)
        {
            runtimeTexture = baseTexture;
            return runtimeTexture != null;
        }

        string cacheKey = string.Concat("paths|", baseTexPath ?? string.Empty, "|", overlayTexPath ?? string.Empty);
        return TryResolveCompositeTexture(baseTexture, overlayTexture, cacheKey, out runtimeTexture);
    }

    public static bool TryResolveCompositeTexture(Texture2D baseTexture, Texture2D overlayTexture, string cacheKey, out Texture2D runtimeTexture)
    {
        runtimeTexture = null;
        if (baseTexture == null && overlayTexture == null)
            return false;

        if (baseTexture == null)
        {
            runtimeTexture = overlayTexture;
            return runtimeTexture != null;
        }

        if (overlayTexture == null)
        {
            runtimeTexture = baseTexture;
            return runtimeTexture != null;
        }

        if (cacheKey.NullOrEmpty())
            cacheKey = string.Concat("tex|", baseTexture.GetInstanceID().ToString(), "|", overlayTexture.GetInstanceID().ToString());

        if (_runtimeTexturesByKey.TryGetValue(cacheKey, out runtimeTexture) && runtimeTexture != null)
            return true;

        if (!AutoEyePatchAnalyzer.TryReadPixels(baseTexture, out Color[] basePixels) || !AutoEyePatchAnalyzer.TryReadPixels(overlayTexture, out Color[] overlayPixels))
            return false;

        int width = Mathf.Max(baseTexture.width, overlayTexture.width);
        int height = Mathf.Max(baseTexture.height, overlayTexture.height);
        Color[] result = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            float v = height > 1 ? (float)y / (height - 1) : 0f;
            for (int x = 0; x < width; x++)
            {
                float u = width > 1 ? (float)x / (width - 1) : 0f;
                Color baseColor = Sample(basePixels, baseTexture.width, baseTexture.height, u, v);
                Color overlayColor = Sample(overlayPixels, overlayTexture.width, overlayTexture.height, u, v);
                result[(y * width) + x] = AlphaComposite(baseColor, overlayColor);
            }
        }

        runtimeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
        {
            name = string.Concat("D2Composite_", Mathf.Abs(cacheKey.GetHashCode()).ToString())
        };
        FacePartTextureRuntime.FinalizeRuntimeTexture(runtimeTexture, result, width, height);
        _runtimeTexturesByKey[cacheKey] = runtimeTexture;
        return true;
    }

    private static Texture2D ResolveTexture(string texPath)
    {
        if (texPath.NullOrEmpty() || string.Equals(texPath, CompFaceParts.EMPTY_DETAIL_TEX_PATH, StringComparison.OrdinalIgnoreCase))
            return null;

        return FacePartTextureRuntime.PrepareTexture(ContentFinder<Texture2D>.Get(texPath, false));
    }

    private static Color Sample(Color[] pixels, int width, int height, float u, float v)
    {
        if (pixels == null || width <= 0 || height <= 0)
            return Color.clear;

        int x = Mathf.Clamp(Mathf.RoundToInt(u * (width - 1)), 0, width - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(v * (height - 1)), 0, height - 1);
        return pixels[(y * width) + x];
    }

    private static Color AlphaComposite(Color baseColor, Color overlayColor)
    {
        float overlayAlpha = Mathf.Clamp01(overlayColor.a);
        float baseAlpha = Mathf.Clamp01(baseColor.a);
        float oneMinusOverlayAlpha = 1f - overlayAlpha;
        float outAlpha = overlayAlpha + (baseAlpha * oneMinusOverlayAlpha);
        if (outAlpha <= 0.0001f)
            return Color.clear;

        float baseContribution = baseAlpha * oneMinusOverlayAlpha;
        float inverseOutAlpha = 1f / outAlpha;
        float outRed = ((overlayColor.r * overlayAlpha) + (baseColor.r * baseContribution)) * inverseOutAlpha;
        float outGreen = ((overlayColor.g * overlayAlpha) + (baseColor.g * baseContribution)) * inverseOutAlpha;
        float outBlue = ((overlayColor.b * overlayAlpha) + (baseColor.b * baseContribution)) * inverseOutAlpha;
        return new Color(outRed, outGreen, outBlue, outAlpha);
    }
}
