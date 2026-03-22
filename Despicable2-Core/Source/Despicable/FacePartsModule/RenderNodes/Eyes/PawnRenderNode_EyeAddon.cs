using Despicable;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using Verse;

// Guardrail-Reason: Eye addon render-node helpers stay together because runtime graphic construction and draw-size probing share one render seam.
namespace Despicable;
public class PawnRenderNode_EyeAddon : PawnRenderNode
{
    private sealed class RuntimeEyeBaseMultiGraphic : Graphic_Single
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

    private readonly CompFaceParts compFaceParts;
    private readonly string debugLabel;
    private readonly bool isRightCounterpartNodeCached;
    private readonly bool isLeftCounterpartNodeCached;
    private readonly bool isBrowNodeCached;
    private readonly bool isEyeFacingNodeCached;
    private readonly FacePartExpressionOffsetKind expressionOffsetKind;
    public CompFaceParts CompFaceParts => compFaceParts;
    public string DebugLabel => debugLabel;
    public bool IsRightCounterpartNodeCached => isRightCounterpartNodeCached;
    public bool IsLeftCounterpartNodeCached => isLeftCounterpartNodeCached;
    public bool IsBrowNodeCached => isBrowNodeCached;
    public bool IsEyeFacingNodeCached => isEyeFacingNodeCached;
    internal FacePartExpressionOffsetKind ExpressionOffsetKind => expressionOffsetKind;
    private readonly bool isEyeBaseNode;
    private readonly bool isAutoEyePatchNode;
    private readonly bool isRightEyeBaseNode;
    private readonly bool isEyeDetailNode;
    private readonly bool isEyeGraphicNode;

    public PawnRenderNode_EyeAddon(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {
        compFaceParts = pawn.TryGetComp<CompFaceParts>();
        debugLabel = props?.debugLabel ?? string.Empty;
        isRightCounterpartNodeCached = debugLabel.EndsWith("_R", StringComparison.OrdinalIgnoreCase);
        isLeftCounterpartNodeCached = debugLabel.EndsWith("_L", StringComparison.OrdinalIgnoreCase);
        isBrowNodeCached = FacePartRenderNodeContextCache.IsBrowDebugLabel(debugLabel);
        isEyeFacingNodeCached = FacePartRenderNodeContextCache.MatchesEyeFacingDebugLabel(debugLabel);
        expressionOffsetKind = FacePartRenderNodeContextCache.ResolveOffsetKind(debugLabel);
        isEyeBaseNode = debugLabel.StartsWith("FacePart_EyeBase", StringComparison.OrdinalIgnoreCase)
            || debugLabel.StartsWith("FacePart_AutoEyePatch", StringComparison.OrdinalIgnoreCase);
        isAutoEyePatchNode = debugLabel.StartsWith("FacePart_AutoEyePatch", StringComparison.OrdinalIgnoreCase);
        isRightEyeBaseNode = isRightCounterpartNodeCached;
        isEyeDetailNode = debugLabel.Equals("FacePart_EyeDetail_L", StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals("FacePart_EyeDetail_R", StringComparison.OrdinalIgnoreCase);
        isEyeGraphicNode = debugLabel.Equals("FacePart_Eye_L", StringComparison.OrdinalIgnoreCase)
            || debugLabel.Equals("FacePart_Eye_R", StringComparison.OrdinalIgnoreCase);
    }

    public override GraphicMeshSet MeshSetFor(Pawn pawn)
    {
        return HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn);
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

    private static void AssignMaterialFields(object target, Material material)
    {
        if (target == null || material == null)
            return;

        for (Type type = target.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic)
                    continue;

                if (field.FieldType == typeof(Material))
                {
                    field.SetValue(target, material);
                }
                else if (field.FieldType == typeof(Material[]))
                {
                    Material[] existing = field.GetValue(target) as Material[];
                    if (existing == null || existing.Length == 0)
                        field.SetValue(target, new Material[] { material });
                    else
                    {
                        Material[] replacement = new Material[existing.Length];
                        for (int j = 0; j < replacement.Length; j++)
                            replacement[j] = material;
                        field.SetValue(target, replacement);
                    }
                }
            }
        }
    }


    private bool IsEyeBaseNode() => isEyeBaseNode;


    private bool IsAutoEyePatchNode() => isAutoEyePatchNode;

    public static Vector2 ResolveHeadOverlayDrawSize(Pawn pawn, Vector2 fallback)
    {
        if (fallback.x > 0.001f && fallback.y > 0.001f)
        {
            // For auto eye patch nodes we prefer to match the actual head graphic draw size when possible,
            // but keep a valid fallback for legacy paths and unusual renderers.
        }

        try
        {
            object renderer = pawn?.Drawer?.renderer;
            if (renderer != null)
            {
                BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                Type rendererType = renderer.GetType();

                object graphics = rendererType.GetField("graphics", flags)?.GetValue(renderer)
                    ?? rendererType.GetProperty("graphics", flags)?.GetValue(renderer, null);

                if (graphics != null)
                {
                    Type graphicsType = graphics.GetType();

                    object headGraphicObj = graphicsType.GetField("headGraphic", flags)?.GetValue(graphics)
                        ?? graphicsType.GetProperty("headGraphic", flags)?.GetValue(graphics, null);

                    if (headGraphicObj is Verse.Graphic headGraphic)
                    {
                        Vector2 drawSize = headGraphic.drawSize;
                        if (drawSize.x > 0.001f && drawSize.y > 0.001f)
                            return drawSize;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            string pawnName = pawn?.ThingID ?? pawn?.LabelShortCap ?? "<null>";
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "PawnRenderNode_EyeAddon.ResolveHeadOverlayDrawSize",
                $"Eye addon render node could not probe head overlay draw size for '{pawnName}'. Using fallback draw size.",
                ex);
        }

        if (fallback.x > 0.001f && fallback.y > 0.001f)
            return fallback;

        return new Vector2(1f, 1f);
    }

    private Vector2 ResolveRuntimeDrawSize(Pawn pawn)
    {
        if (IsAutoEyePatchNode())
            return ResolveHeadOverlayDrawSize(pawn, props.drawSize);

        return props.drawSize;
    }

    private static string BuildRuntimeGraphicKey(Texture2D runtimeTexture, Vector2 drawSize, Color tint)
    {
        return string.Concat(runtimeTexture.GetInstanceID().ToString(), "|", drawSize.x.ToString("R"), "x", drawSize.y.ToString("R"), "|", tint.GetHashCode().ToString());
    }

    private bool IsRightEyeBaseNode() => isRightEyeBaseNode;

    private bool IsEyeDetailNode() => isEyeDetailNode;

    private bool IsEyeGraphicNode() => isEyeGraphicNode;

    private Color ResolveTintForNode(Pawn pawn)
    {
        if (pawn == null)
            return Color.white;

        if (IsEyeGraphicNode())
        {
            Color overrideTint = compFaceParts?.GetResolvedEyeTintThisTick() ?? Color.black;
            if (overrideTint.a <= 0f)
                overrideTint.a = 1f;
            return overrideTint;
        }

        Color tint = ColorFor(pawn);
        if (tint.a <= 0f)
            tint = Color.white;
        return tint;
    }

    private Verse.Graphic BuildRuntimeGraphic(Pawn pawn, Texture2D runtimeTexture, Shader shader = null)
    {
        if (runtimeTexture == null)
            return null;

        Vector2 drawSize = ResolveRuntimeDrawSize(pawn);
        Color tint = ResolveTintForNode(pawn);

        string cacheKey = string.Concat("EyeAddon.Single|", BuildRuntimeGraphicKey(runtimeTexture, drawSize, tint), "|", (shader ?? ShaderDatabase.CutoutSkinOverlay).name);
        return FacePartRuntimeGraphicCache.GetOrCreate(cacheKey, () =>
        {
            shader ??= ShaderDatabase.CutoutSkinOverlay;

            Graphic_Single template = GraphicDatabase.Get<Graphic_Single>("FaceParts/Details/detail_empty", shader, drawSize, tint) as Graphic_Single;
            if (template == null || template.MatSingle == null)
                return ((Verse.Graphic)null, (Material[])null);

            Graphic_Single clone = (Graphic_Single)FormatterServices.GetUninitializedObject(typeof(Graphic_Single));
            CopyInstanceFields(template, clone);

            Material runtimeMat = new Material(template.MatSingle);
            runtimeMat.mainTexture = runtimeTexture;
            AssignMaterialFields(clone, runtimeMat);

            return ((Verse.Graphic)clone, new[] { runtimeMat });
        });
    }

    private static void ResolveBlankFacingTextures(out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture)
    {
        if (!FacePartTextureRuntime.TryResolvePreparedMultiFacingTextures(CompFaceParts.EMPTY_DETAIL_TEX_PATH, ShaderDatabase.CutoutSkinOverlay, out southTexture, out eastTexture, out westTexture, "PawnRenderNode_EyeAddon.BlankTextures", CompFaceParts.EMPTY_DETAIL_TEX_PATH))
        {
            southTexture = null;
            eastTexture = null;
            westTexture = null;
        }
    }

    private Verse.Graphic BuildRuntimeMultiGraphic(Pawn pawn, Texture2D southTexture, Texture2D eastTexture, Texture2D westTexture, bool allowFacingFallback = true)
    {
        if (southTexture == null && eastTexture == null && westTexture == null)
            return null;

        Color tint = ResolveTintForNode(pawn);

        Vector2 drawSize = ResolveRuntimeDrawSize(pawn);
        string cacheKey = string.Concat(
            "EyeAddon.Multi|",
            allowFacingFallback ? "fallback" : "strict",
            "|",
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
            tint.GetHashCode().ToString(),
            "|",
            ShaderDatabase.CutoutSkinOverlay.name);

        return FacePartRuntimeGraphicCache.GetOrCreate(cacheKey, () =>
        {
            Shader shader = ShaderDatabase.CutoutSkinOverlay;
            Graphic_Single template = GraphicDatabase.Get<Graphic_Single>("FaceParts/Details/detail_empty", shader, drawSize, tint) as Graphic_Single;
            if (template == null || template.MatSingle == null)
                return ((Verse.Graphic)null, (Material[])null);

            Texture2D blankSouth = null;
            Texture2D blankEast = null;
            Texture2D blankWest = null;
            if (!allowFacingFallback)
                ResolveBlankFacingTextures(out blankSouth, out blankEast, out blankWest);

            Material southMat = new Material(template.MatSingle);
            southMat.mainTexture = allowFacingFallback
                ? southTexture ?? eastTexture ?? westTexture
                : southTexture ?? blankSouth ?? blankEast ?? blankWest;

            Material eastMat = new Material(template.MatSingle);
            eastMat.mainTexture = allowFacingFallback
                ? eastTexture ?? southTexture ?? westTexture
                : eastTexture ?? blankEast ?? blankSouth ?? blankWest;

            Material westMat = new Material(template.MatSingle);
            westMat.mainTexture = allowFacingFallback
                ? westTexture ?? eastTexture ?? southTexture
                : westTexture ?? blankWest ?? blankEast ?? blankSouth;

            RuntimeEyeBaseMultiGraphic graphic = new();
            graphic.Initialize(template, southMat, eastMat, westMat);

            return ((Verse.Graphic)graphic, new[] { southMat, eastMat, westMat });
        });
    }

    private Verse.Graphic TryBuildRuntimeEyeBaseGraphic(Pawn pawn)
    {
        if (!IsEyeBaseNode() || pawn == null)
            return null;

        if (IsRightEyeBaseNode())
        {
            if (AutoEyePatchRuntime.TryResolveEyeBaseReplacement(pawn, this, Rot4.South, out AutoEyePatchRenderSelection rightSelection)
                && rightSelection.SuppressLegacy
                && rightSelection.RuntimeTexture != null)
            {
                return BuildRuntimeGraphic(pawn, rightSelection.RuntimeTexture);
            }

            return null;
        }

        if (!AutoEyePatchRuntime.TryResolveEyeBaseReplacement(pawn, this, Rot4.South, out AutoEyePatchRenderSelection southSelection)
            || !southSelection.SuppressLegacy
            || southSelection.RuntimeTexture == null)
        {
            return null;
        }

        if (!AutoEyePatchRuntime.TryResolveEyeBaseReplacement(pawn, this, Rot4.East, out AutoEyePatchRenderSelection sideSelection)
            || !sideSelection.SuppressLegacy
            || sideSelection.RuntimeTexture == null)
        {
            return null;
        }

        return BuildRuntimeMultiGraphic(pawn, southSelection.RuntimeTexture, sideSelection.RuntimeTexture, sideSelection.RuntimeTexture);
    }

    public override Verse.Graphic GraphicFor(Pawn pawn)
    {
        if (compFaceParts?.IsRenderActiveNow() != true)
        {
            return null;
        }

        if (pawn?.health?.hediffSet == null)
        {
            return null;
        }
        if (!pawn.health.hediffSet.HasHead)
        {
            return null;
        }
        if (pawn?.Drawer?.renderer == null)
        {
            return null;
        }
        if (pawn.Drawer.renderer.CurRotDrawMode == RotDrawMode.Dessicated)
        {
            return null;
        }

        string texPath = this.props.texPath;

        if (IsEyeBaseNode())
        {
            Verse.Graphic runtimeEyeBaseGraphic = TryBuildRuntimeEyeBaseGraphic(pawn);
            if (runtimeEyeBaseGraphic != null)
                return runtimeEyeBaseGraphic;
        }

        if (IsEyeDetailNode() && compFaceParts != null)
        {
            string styleTexPath = compFaceParts.ResolveEyeDetailBaseStyleTexturePath(texPath);
            string stateTexPath = compFaceParts.ResolveEyeDetailStateTexturePath();
            string activeTexPath = !stateTexPath.NullOrEmpty() && !string.Equals(stateTexPath, CompFaceParts.EMPTY_DETAIL_TEX_PATH, StringComparison.OrdinalIgnoreCase)
                ? stateTexPath
                : styleTexPath;

            bool hasStyle = !styleTexPath.NullOrEmpty() && !string.Equals(styleTexPath, CompFaceParts.EMPTY_DETAIL_TEX_PATH, StringComparison.OrdinalIgnoreCase);
            bool hasState = !stateTexPath.NullOrEmpty() && !string.Equals(stateTexPath, CompFaceParts.EMPTY_DETAIL_TEX_PATH, StringComparison.OrdinalIgnoreCase);

            if (hasStyle || hasState)
            {
                Texture2D styleSouth = null;
                Texture2D styleEast = null;
                Texture2D styleWest = null;
                Texture2D stateSouth = null;
                Texture2D stateEast = null;
                Texture2D stateWest = null;

                bool styleReady = !hasStyle || FacePartTextureRuntime.TryResolvePreparedMultiFacingTextures(styleTexPath, ShaderDatabase.CutoutSkinOverlay, out styleSouth, out styleEast, out styleWest, "PawnRenderNode_EyeAddon.EyeDetailStyle", props?.debugLabel ?? styleTexPath);
                bool stateReady = !hasState || FacePartTextureRuntime.TryResolvePreparedMultiFacingTextures(stateTexPath, ShaderDatabase.CutoutSkinOverlay, out stateSouth, out stateEast, out stateWest, "PawnRenderNode_EyeAddon.EyeDetailState", props?.debugLabel ?? stateTexPath);

                if (styleReady)
                {
                    if (!compFaceParts.ShouldRenderEyeDetailStyleForFacing(this.props.debugLabel, Rot4.South))
                        styleSouth = null;
                    if (!compFaceParts.ShouldRenderEyeDetailStyleForFacing(this.props.debugLabel, Rot4.East))
                        styleEast = null;
                    if (!compFaceParts.ShouldRenderEyeDetailStyleForFacing(this.props.debugLabel, Rot4.West))
                        styleWest = null;
                }

                if (styleReady && stateReady)
                {
                    Texture2D finalSouth = null;
                    Texture2D finalEast = null;
                    Texture2D finalWest = null;

                    string cacheStem = string.Concat(
                        pawn?.story?.headType?.defName ?? string.Empty,
                        "|",
                        props?.debugLabel ?? string.Empty,
                        "|",
                        styleTexPath ?? string.Empty,
                        "|",
                        compFaceParts.GetResolvedEyeDetailSideMode().ToString(),
                        "|",
                        stateTexPath ?? string.Empty);
                    FacePartCompositeRuntime.TryResolveCompositeTexture(styleSouth, stateSouth, cacheStem + "|south", out finalSouth);
                    FacePartCompositeRuntime.TryResolveCompositeTexture(styleEast, stateEast, cacheStem + "|east", out finalEast);
                    FacePartCompositeRuntime.TryResolveCompositeTexture(styleWest, stateWest, cacheStem + "|west", out finalWest);

                    Verse.Graphic runtimeGraphic = BuildRuntimeMultiGraphic(pawn, finalSouth, finalEast, finalWest, allowFacingFallback: false);
                    if (runtimeGraphic != null)
                        return runtimeGraphic;
                }
            }

            texPath = activeTexPath;
        }
        else if (compFaceParts != null)
        {
            texPath = compFaceParts.ResolveTexturePathForDebugLabel(this.props.debugLabel, texPath);
        }

        if (texPath.NullOrEmpty())
            texPath = CompFaceParts.EMPTY_DETAIL_TEX_PATH;

        texPath = FacePartsUtil.GetEyePath(pawn, texPath);
        if (FacePartTextureRuntime.TryResolvePreparedMultiFacingTextures(texPath, ShaderDatabase.CutoutSkinOverlay, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture, "PawnRenderNode_EyeAddon.PreparedTextures", props?.debugLabel ?? texPath))
        {
            Verse.Graphic runtimeGraphic = BuildRuntimeMultiGraphic(pawn, southTexture, eastTexture, westTexture);
            if (runtimeGraphic != null)
                return runtimeGraphic;
        }

        return GraphicDatabase.Get<Graphic_Multi>(texPath, ShaderDatabase.CutoutSkinOverlay, ResolveRuntimeDrawSize(pawn), ResolveTintForNode(pawn));
    }
}
