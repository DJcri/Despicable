using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Despicable.UIFramework;

namespace Despicable.FacePartsModule.UI;

internal static class FacePreviewCache
{
    internal enum PreviewAnchor
    {
        Center = 0,
        BottomCenter = 1
    }

    private readonly struct PreviewCrop
    {
        public readonly Rect TexCoords;
        public readonly int PixelWidth;
        public readonly int PixelHeight;

        public PreviewCrop(Rect texCoords, int pixelWidth, int pixelHeight)
        {
            TexCoords = texCoords;
            PixelWidth = Mathf.Max(1, pixelWidth);
            PixelHeight = Mathf.Max(1, pixelHeight);
        }
    }

    private const byte AlphaThreshold = 12;
    private static readonly Dictionary<Texture2D, PreviewCrop> CropByTexture = new();

    public static Texture2D ResolveHeadTexture(HeadTypeDef headType)
    {
        if (headType == null || headType.graphicPath.NullOrEmpty())
            return null;

        try
        {
            Graphic graphic = GraphicDatabase.Get<Graphic_Multi>(headType.graphicPath, ShaderDatabase.CutoutSkin, Vector2.one, Color.white);
            if (graphic != null)
            {
                Material mat = graphic.MatAt(Rot4.South);
                if (mat?.mainTexture is Texture2D southTex)
                    return southTex;

                if (graphic is Graphic_Single single && single.MatSingle?.mainTexture is Texture2D singleTex)
                    return singleTex;
            }
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "FacePreviewCache.ResolveHeadTexture.Graphic",
                "Face preview cache failed to resolve a head graphic through GraphicDatabase.",
                ex);
        }

        try
        {
            return ContentFinder<Texture2D>.Get(headType.graphicPath, false);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "FacePreviewCache.ResolveHeadTexture.ContentFinder",
                "Face preview cache failed to resolve a head texture through ContentFinder.",
                ex);
        }

        return null;
    }

    public static Texture2D ResolveFacePartTexture(Pawn pawn, FacePartStyleDef style)
    {
        if (style == null || style.texPath.NullOrEmpty())
            return null;

        string path = style.texPath;
        if (path.StartsWith("Gendered/", StringComparison.Ordinal))
            path = FacePartsUtil.GetEyePath(pawn, path);

        try
        {
            return ContentFinder<Texture2D>.Get(path, false);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "FacePreviewCache.ResolveFacePartTexture",
                "Face preview cache failed to resolve a face-part texture.",
                ex);
        }

        return null;
    }

    public static Texture2D ResolveTexture(string path, string warnOnceKey = "FacePreviewCache.ResolveTexture")
    {
        if (path.NullOrEmpty())
            return null;

        try
        {
            return ContentFinder<Texture2D>.Get(path, false);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                warnOnceKey,
                "Face preview cache failed to resolve a texture.",
                ex);
        }

        return null;
    }

    public static void DrawAlignedTextureStack(UIContext ctx, Rect rect, string label, PreviewAnchor anchor = PreviewAnchor.Center, float padding = 0f, params Texture2D[] textures)
    {
        Rect recordedRect = rect;
        ctx?.RecordRect(recordedRect, UIRectTag.Icon, label, null);
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return;

        Texture2D reference = null;
        if (textures != null)
        {
            for (int i = 0; i < textures.Length; i++)
            {
                if (textures[i] != null)
                {
                    reference = textures[i];
                    break;
                }
            }
        }

        reference ??= BaseContent.BadTex;
        if (reference == null)
            return;

        Rect targetRect = padding > 0f ? rect.ContractedBy(padding) : rect;
        if (targetRect.width <= 1f || targetRect.height <= 1f)
            return;

        Rect drawRect = FitRect(targetRect, reference.width, reference.height, anchor);
        if (drawRect.width <= 1f || drawRect.height <= 1f)
            return;

        if (textures == null || textures.Length == 0)
        {
            GUI.DrawTexture(drawRect, reference, ScaleMode.StretchToFill, true);
            return;
        }

        bool drewAny = false;
        for (int i = 0; i < textures.Length; i++)
        {
            Texture2D texture = textures[i];
            if (texture == null)
                continue;

            GUI.DrawTexture(drawRect, texture, ScaleMode.StretchToFill, true);
            drewAny = true;
        }

        if (!drewAny)
            GUI.DrawTexture(drawRect, reference, ScaleMode.StretchToFill, true);
    }

    public static void DrawCroppedTexture(UIContext ctx, Rect rect, Texture2D texture, string label, float padding = 0f, PreviewAnchor anchor = PreviewAnchor.Center)
    {
        Rect recordedRect = rect;
        ctx?.RecordRect(recordedRect, UIRectTag.Icon, label, null);
        if (ctx != null && ctx.Pass == UIPass.Measure)
            return;

        Texture2D source = texture ?? BaseContent.BadTex;
        if (source == null)
            return;

        Rect targetRect = padding > 0f ? rect.ContractedBy(padding) : rect;
        if (targetRect.width <= 1f || targetRect.height <= 1f)
            return;

        PreviewCrop crop = GetOrCreateCrop(source);
        Rect drawRect = FitRect(targetRect, crop.PixelWidth, crop.PixelHeight, anchor);
        if (drawRect.width <= 1f || drawRect.height <= 1f)
            return;

        GUI.DrawTextureWithTexCoords(drawRect, source, crop.TexCoords, alphaBlend: true);
    }

    public static int ComputeGridColumns(float width, float tileSize, float gap)
    {
        if (tileSize <= 0f)
            return 1;

        return Mathf.Max(1, Mathf.FloorToInt((Mathf.Max(0f, width) + gap) / (tileSize + gap)));
    }

    public static float MeasureGridHeight(float width, int itemCount, float tileSize, float gap)
    {
        if (itemCount <= 0)
            return tileSize;

        int columns = ComputeGridColumns(width, tileSize, gap);
        int rows = Mathf.CeilToInt(itemCount / (float)columns);
        return (rows * tileSize) + (Mathf.Max(0, rows - 1) * gap);
    }

    private static PreviewCrop GetOrCreateCrop(Texture2D texture)
    {
        if (CropByTexture.TryGetValue(texture, out PreviewCrop cached))
            return cached;

        PreviewCrop crop = BuildCrop(texture);
        CropByTexture[texture] = crop;
        return crop;
    }

    private static PreviewCrop BuildCrop(Texture2D texture)
    {
        if (texture == null)
            return new PreviewCrop(new Rect(0f, 0f, 1f, 1f), 1, 1);

        if (TryBuildCrop(texture, out PreviewCrop crop))
            return crop;

        if (TryBuildCropFromReadableCopy(texture, out crop))
            return crop;

        Despicable.Core.DebugLogger.WarnExceptionOnce(
            "FacePreviewCache.BuildCrop.Fallback",
            "Face preview cache fell back to uncropped drawing because a texture could not be read.",
            null);
        return new PreviewCrop(new Rect(0f, 0f, 1f, 1f), texture.width, texture.height);
    }

    private static bool TryBuildCrop(Texture2D texture, out PreviewCrop crop)
    {
        crop = default;

        try
        {
            Color32[] pixels = texture.GetPixels32();
            return TryBuildCropFromPixels(pixels, texture.width, texture.height, out crop);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryBuildCropFromReadableCopy(Texture2D texture, out PreviewCrop crop)
    {
        crop = default;
        RenderTexture temp = null;
        RenderTexture previous = null;
        Texture2D readable = null;

        try
        {
            int width = Mathf.Max(1, texture.width);
            int height = Mathf.Max(1, texture.height);
            temp = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            Graphics.Blit(texture, temp);

            previous = RenderTexture.active;
            RenderTexture.active = temp;

            readable = new Texture2D(width, height, TextureFormat.ARGB32, false, false);
            readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            readable.Apply(false, false);

            Color32[] pixels = readable.GetPixels32();
            return TryBuildCropFromPixels(pixels, width, height, out crop);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "FacePreviewCache.BuildCrop.ReadableCopy",
                "Face preview cache could not build a readable copy for alpha cropping.",
                ex);
            return false;
        }
        finally
        {
            if (RenderTexture.active == temp)
                RenderTexture.active = previous;
            else if (previous != null)
                RenderTexture.active = previous;

            if (temp != null)
                RenderTexture.ReleaseTemporary(temp);

            if (readable != null)
                UnityEngine.Object.Destroy(readable);
        }
    }

    private static bool TryBuildCropFromPixels(Color32[] pixels, int width, int height, out PreviewCrop crop)
    {
        crop = default;
        if (pixels == null || pixels.Length != width * height)
            return false;

        int minX = width;
        int minY = height;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < height; y++)
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                if (pixels[row + x].a <= AlphaThreshold)
                    continue;

                if (x < minX)
                    minX = x;
                if (x > maxX)
                    maxX = x;
                if (y < minY)
                    minY = y;
                if (y > maxY)
                    maxY = y;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            crop = new PreviewCrop(new Rect(0f, 0f, 1f, 1f), width, height);
            return true;
        }

        int cropWidth = Mathf.Max(1, (maxX - minX) + 1);
        int cropHeight = Mathf.Max(1, (maxY - minY) + 1);
        Rect uv = new Rect(
            minX / (float)width,
            minY / (float)height,
            cropWidth / (float)width,
            cropHeight / (float)height);
        crop = new PreviewCrop(uv, cropWidth, cropHeight);
        return true;
    }

    private static Rect FitRect(Rect outer, float sourceWidth, float sourceHeight, PreviewAnchor anchor)
    {
        if (outer.width <= 0f || outer.height <= 0f)
            return Rect.zero;

        float width = Mathf.Max(1f, sourceWidth);
        float height = Mathf.Max(1f, sourceHeight);
        float scale = Mathf.Min(outer.width / width, outer.height / height);
        float drawWidth = width * scale;
        float drawHeight = height * scale;
        float x = outer.x + ((outer.width - drawWidth) * 0.5f);
        float y = anchor == PreviewAnchor.BottomCenter
            ? outer.yMax - drawHeight
            : outer.y + ((outer.height - drawHeight) * 0.5f);
        return new Rect(x, y, drawWidth, drawHeight);
    }
}
