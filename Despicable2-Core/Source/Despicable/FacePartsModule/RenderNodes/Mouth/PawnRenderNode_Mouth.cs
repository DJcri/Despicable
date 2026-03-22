using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Despicable;
public class PawnRenderNode_Mouth : PawnRenderNode
{
    private sealed class RuntimeFaceDetailMultiGraphic : Graphic_Single
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
    private readonly FacePartExpressionOffsetKind expressionOffsetKind;
    public CompFaceParts CompFaceParts => compFaceParts;
    public string DebugLabel => debugLabel;
    public bool IsRightCounterpartNodeCached => isRightCounterpartNodeCached;
    public bool IsLeftCounterpartNodeCached => isLeftCounterpartNodeCached;
    internal FacePartExpressionOffsetKind ExpressionOffsetKind => expressionOffsetKind;
    private readonly bool isFaceDetailNode;

    public PawnRenderNode_Mouth(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {
        compFaceParts = pawn.TryGetComp<CompFaceParts>();
        debugLabel = props?.debugLabel ?? string.Empty;
        isRightCounterpartNodeCached = debugLabel.EndsWith("_R", StringComparison.OrdinalIgnoreCase);
        isLeftCounterpartNodeCached = debugLabel.EndsWith("_L", StringComparison.OrdinalIgnoreCase);
        expressionOffsetKind = FacePartRenderNodeContextCache.ResolveOffsetKind(debugLabel);
        isFaceDetailNode = debugLabel.Equals("FacePart_FaceDetail", StringComparison.OrdinalIgnoreCase);
    }

    public override GraphicMeshSet MeshSetFor(Pawn pawn)
    {
        return HumanlikeMeshPoolUtility.GetHumanlikeHeadSetForPawn(pawn);
    }

    private bool IsFaceDetailNode() => isFaceDetailNode;

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

    private Verse.Graphic BuildRuntimeMultiGraphic(Pawn pawn, Texture2D southTexture, Texture2D eastTexture, Texture2D westTexture)
    {
        if (southTexture == null && eastTexture == null && westTexture == null)
            return null;

        Vector2 drawSize = Vector2.one;
        Color tint = pawn != null ? ColorFor(pawn) : Color.white;
        if (tint.a <= 0f)
            tint = Color.white;

        string cacheKey = string.Concat("Mouth.Multi|", BuildRuntimeGraphicKey(southTexture, eastTexture, westTexture, drawSize, tint), "|", ShaderDatabase.Cutout.name);
        return FacePartRuntimeGraphicCache.GetOrCreate(cacheKey, () =>
        {
            Shader shader = ShaderDatabase.Cutout;
            Graphic_Single template = GraphicDatabase.Get<Graphic_Single>(CompFaceParts.EMPTY_DETAIL_TEX_PATH, shader, drawSize, tint) as Graphic_Single;
            if (template == null || template.MatSingle == null)
                return ((Verse.Graphic)null, (Material[])null);

            Material southMat = new Material(template.MatSingle);
            southMat.mainTexture = southTexture ?? eastTexture ?? westTexture;

            Material eastMat = new Material(template.MatSingle);
            eastMat.mainTexture = eastTexture ?? southTexture ?? westTexture;

            Material westMat = new Material(template.MatSingle);
            westMat.mainTexture = westTexture ?? eastTexture ?? southTexture;

            RuntimeFaceDetailMultiGraphic graphic = (RuntimeFaceDetailMultiGraphic)FormatterServices.GetUninitializedObject(typeof(RuntimeFaceDetailMultiGraphic));
            graphic.Initialize(template, southMat, eastMat, westMat);

            return ((Verse.Graphic)graphic, new[] { southMat, eastMat, westMat });
        });
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

        if (IsFaceDetailNode() && compFaceParts != null)
        {
            string styleTexPath = compFaceParts.ResolveFaceDetailStyleTexturePath(texPath);
            string stateTexPath = compFaceParts.ResolveFaceDetailStateTexturePath();
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

                bool styleReady = !hasStyle || FaceDetailBoundaryRuntime.TryResolveGuardedTextures(pawn, styleTexPath, out styleSouth, out styleEast, out styleWest);
                bool stateReady = !hasState || FaceDetailBoundaryRuntime.TryResolveGuardedTextures(pawn, stateTexPath, out stateSouth, out stateEast, out stateWest);

                if (styleReady && stateReady)
                {
                    Texture2D finalSouth = null;
                    Texture2D finalEast = null;
                    Texture2D finalWest = null;

                    string cacheStem = string.Concat(pawn?.story?.headType?.defName ?? string.Empty, "|", styleTexPath ?? string.Empty, "|", stateTexPath ?? string.Empty);
                    FacePartCompositeRuntime.TryResolveCompositeTexture(styleSouth, stateSouth, cacheStem + "|south", out finalSouth);
                    FacePartCompositeRuntime.TryResolveCompositeTexture(styleEast, stateEast, cacheStem + "|east", out finalEast);
                    FacePartCompositeRuntime.TryResolveCompositeTexture(styleWest, stateWest, cacheStem + "|west", out finalWest);

                    Verse.Graphic runtimeGraphic = BuildRuntimeMultiGraphic(pawn, finalSouth, finalEast, finalWest);
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

        if (IsFaceDetailNode()
            && !string.Equals(texPath, CompFaceParts.EMPTY_DETAIL_TEX_PATH, StringComparison.OrdinalIgnoreCase)
            && FaceDetailBoundaryRuntime.TryResolveGuardedTextures(pawn, texPath, out Texture2D southTexture, out Texture2D eastTexture, out Texture2D westTexture))
        {
            Verse.Graphic runtimeGraphic = BuildRuntimeMultiGraphic(pawn, southTexture, eastTexture, westTexture);
            if (runtimeGraphic != null)
                return runtimeGraphic;
        }

        if (FacePartTextureRuntime.TryResolvePreparedMultiFacingTextures(texPath, ShaderDatabase.Cutout, out Texture2D southTexturePrepared, out Texture2D eastTexturePrepared, out Texture2D westTexturePrepared, "PawnRenderNode_Mouth.PreparedTextures", props?.debugLabel ?? texPath))
        {
            Verse.Graphic runtimeGraphic = BuildRuntimeMultiGraphic(pawn, southTexturePrepared, eastTexturePrepared, westTexturePrepared);
            if (runtimeGraphic != null)
                return runtimeGraphic;
        }

        return GraphicDatabase.Get<Graphic_Multi>(texPath, ShaderDatabase.Cutout, Vector2.one, ColorFor(pawn));
    }
}
