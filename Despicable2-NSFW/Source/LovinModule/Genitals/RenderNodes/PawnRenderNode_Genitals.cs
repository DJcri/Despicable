using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderNode_Genitals : PawnRenderNode
{
    private sealed class RuntimeGenitalMultiGraphic : Graphic_Single
    {
        private Material _southMaterial;
        private Material _eastMaterial;
        private Material _westMaterial;

        public void Initialize(Graphic_Single template, Material southMaterial, Material eastMaterial, Material westMaterial)
        {
            CopyInstanceFields(template, this);
            _southMaterial = southMaterial;
            _eastMaterial = eastMaterial;
            _westMaterial = westMaterial;
        }

        public override Material MatSingle => _southMaterial ?? _eastMaterial ?? _westMaterial ?? base.MatSingle;

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            if (rot == Rot4.East)
                return _eastMaterial ?? _southMaterial ?? _westMaterial ?? base.MatSingle;

            if (rot == Rot4.West)
                return _westMaterial ?? _eastMaterial ?? _southMaterial ?? base.MatSingle;

            return _southMaterial ?? _eastMaterial ?? _westMaterial ?? base.MatSingle;
        }
    }

    public PawnRenderNode_Genitals(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {
    }

    public override GraphicMeshSet MeshSetFor(Pawn pawn)
    {
        return HumanlikeMeshPoolUtility.GetHumanlikeBodySetForPawn(pawn);
    }

    public override Verse.Graphic GraphicFor(Pawn pawn)
    {
        if (pawn?.Drawer?.renderer?.CurRotDrawMode == RotDrawMode.Dessicated)
            return null;

        string texPath = this.TexPathFor(pawn);
        if (texPath.NullOrEmpty())
            return null;

        Color tint = pawn != null ? ColorFor(pawn) : Color.white;
        if (tint.a <= 0f)
            tint = Color.white;

        if (GenitalTextureRuntime.TryResolvePreparedMultiFacingTextures(texPath, ShaderDatabase.Cutout, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture))
        {
            Verse.Graphic runtimeGraphic = BuildRuntimeMultiGraphic(texPath, tint, southTexture, eastTexture, westTexture);
            if (runtimeGraphic != null)
                return runtimeGraphic;
        }

        return GraphicDatabase.Get<Graphic_Multi>(texPath, ShaderDatabase.Cutout, Vector2.one, tint);
    }

    private Verse.Graphic BuildRuntimeMultiGraphic(string texPath, Color tint, Texture2D southTexture, Texture2D eastTexture, Texture2D westTexture)
    {
        if (southTexture == null && eastTexture == null && westTexture == null)
            return null;

        Vector2 drawSize = Vector2.one;
        string cacheKey = string.Concat(
            "Genitals.Multi|",
            BuildRuntimeGraphicKey(southTexture, eastTexture, westTexture, drawSize, tint),
            "|",
            ShaderDatabase.Cutout.name,
            "|",
            texPath ?? string.Empty);

        return GenitalTextureRuntime.GetOrCreateGraphic(cacheKey, () =>
        {
            string templatePath = ResolveTemplatePath(texPath, southTexture, eastTexture, westTexture);
            Graphic_Single template = GraphicDatabase.Get<Graphic_Single>(templatePath, ShaderDatabase.Cutout, drawSize, tint) as Graphic_Single;
            if (template == null || template.MatSingle == null)
                return ((Verse.Graphic)null, (Material[])null);

            Material southMat = new Material(template.MatSingle);
            southMat.mainTexture = southTexture ?? eastTexture ?? westTexture;

            Material eastMat = new Material(template.MatSingle);
            eastMat.mainTexture = eastTexture ?? southTexture ?? westTexture;

            Material westMat = new Material(template.MatSingle);
            westMat.mainTexture = westTexture ?? eastTexture ?? southTexture;

            RuntimeGenitalMultiGraphic graphic = (RuntimeGenitalMultiGraphic)FormatterServices.GetUninitializedObject(typeof(RuntimeGenitalMultiGraphic));
            graphic.Initialize(template, southMat, eastMat, westMat);

            return ((Verse.Graphic)graphic, new[] { southMat, eastMat, westMat });
        });
    }

    private static string ResolveTemplatePath(string texPath, Texture2D southTexture, Texture2D eastTexture, Texture2D westTexture)
    {
        if (!texPath.NullOrEmpty())
        {
            if (southTexture != null)
                return texPath + "_south";

            if (eastTexture != null)
                return texPath + "_east";

            if (westTexture != null)
                return texPath + "_west";
        }

        return texPath;
    }

    private static void CopyInstanceFields(object source, object destination)
    {
        if (source == null || destination == null)
            return;

        Type sourceType = source.GetType();
        Type destinationType = destination.GetType();
        for (Type type = sourceType; type != null && type != typeof(object); type = type.BaseType)
        {
            if (!type.IsAssignableFrom(destinationType))
                continue;

            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic)
                    continue;

                field.SetValue(destination, field.GetValue(source));
            }
        }
    }

    private static string BuildRuntimeGraphicKey(Texture2D southTexture, Texture2D eastTexture, Texture2D westTexture, Vector2 drawSize, Color tint)
    {
        return string.Concat(
            southTexture != null ? southTexture.GetInstanceID().ToString() : "0",
            "|",
            eastTexture != null ? eastTexture.GetInstanceID().ToString() : "0",
            "|",
            westTexture != null ? westTexture.GetInstanceID().ToString() : "0",
            "|",
            drawSize.x.ToString("R"),
            "x",
            drawSize.y.ToString("R"),
            "|",
            tint.GetHashCode().ToString());
    }
}

internal static class GenitalTextureRuntime
{
    private const float BleedTargetAlphaMax = 0.40f;
    private const float BleedSourceAlphaMin = 0.40f;
    private const int BleedPasses = 2;

    private sealed class CachedGraphicEntry
    {
        public Verse.Graphic Graphic;
        public Material[] Materials;
    }

    private static readonly Dictionary<int, Texture2D> _preparedTexturesByKey = new();
    private static readonly Dictionary<string, CachedGraphicEntry> _graphicCache = new();
    private static Game _lastCachedGame;

    private static void EnsureCachesMatchCurrentGame()
    {
        Game currentGame = Current.Game;
        if (ReferenceEquals(_lastCachedGame, currentGame))
            return;

        ResetRuntimeState();
        _lastCachedGame = currentGame;
    }

    public static void ResetRuntimeState()
    {
        foreach (CachedGraphicEntry entry in _graphicCache.Values)
        {
            if (entry?.Materials == null)
                continue;

            for (int i = 0; i < entry.Materials.Length; i++)
            {
                if (entry.Materials[i] != null)
                    UnityEngine.Object.Destroy(entry.Materials[i]);
            }
        }

        _graphicCache.Clear();

        foreach (Texture2D texture in _preparedTexturesByKey.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        _preparedTexturesByKey.Clear();
    }

    public static Verse.Graphic GetOrCreateGraphic(string key, Func<(Verse.Graphic graphic, Material[] materials)> factory)
    {
        EnsureCachesMatchCurrentGame();

        if (key.NullOrEmpty() || factory == null)
            return null;

        if (_graphicCache.TryGetValue(key, out CachedGraphicEntry cached) && cached?.Graphic != null)
            return cached.Graphic;

        (Verse.Graphic graphic, Material[] materials) = factory();
        if (graphic == null)
            return null;

        _graphicCache[key] = new CachedGraphicEntry
        {
            Graphic = graphic,
            Materials = materials,
        };

        return graphic;
    }

    public static bool TryResolvePreparedMultiFacingTextures(string graphicPath, Shader shader, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture)
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
            string name = graphicPath ?? "<null>";
            Log.Warning($"[Despicable NSFW] Genital texture runtime could not resolve GraphicDatabase textures for '{name}'.\n{ex}");
        }

        try
        {
            southTexture ??= PrepareTexture(ContentFinder<Texture2D>.Get(graphicPath, false));
        }
        catch (Exception ex)
        {
            string name = graphicPath ?? "<null>";
            Log.Warning($"[Despicable NSFW] Genital texture runtime could not resolve ContentFinder texture for '{name}'.\n{ex}");
        }

        eastTexture ??= southTexture ?? westTexture;
        westTexture ??= eastTexture ?? southTexture;
        southTexture ??= eastTexture ?? westTexture;
        return southTexture != null || eastTexture != null || westTexture != null;
    }

    private static Texture2D PrepareTexture(Texture2D source)
    {
        EnsureCachesMatchCurrentGame();

        if (source == null)
            return null;

        ConfigureSampler(source);

        int key = source.GetInstanceID();
        if (_preparedTexturesByKey.TryGetValue(key, out Texture2D cached) && cached != null)
            return cached;

        if (!TryReadPixels(source, out Color[] pixels) || pixels == null || pixels.Length != source.width * source.height)
            return source;

        Color[] working = new Color[pixels.Length];
        Array.Copy(pixels, working, pixels.Length);
        BleedRgbIntoTransparentPixels(working, source.width, source.height);

        Texture2D prepared = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false)
        {
            name = string.Concat(source.name ?? "Genital", "_D2Prepared"),
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 0,
        };
        prepared.SetPixels(working);
        prepared.Apply(false, false);
        _preparedTexturesByKey[key] = prepared;
        return prepared;
    }

    private static void ConfigureSampler(Texture2D texture)
    {
        if (texture == null)
            return;

        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.anisoLevel = 0;
    }

    private static bool TryReadPixels(Texture2D texture, out Color[] pixels)
    {
        pixels = null;
        if (texture == null)
            return false;

        try
        {
            pixels = texture.GetPixels();
            if (pixels != null && pixels.Length == texture.width * texture.height)
                return true;
        }
        catch
        {
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
            return pixels != null && pixels.Length == texture.width * texture.height;
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

    private static void BleedRgbIntoTransparentPixels(Color[] pixels, int width, int height)
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
