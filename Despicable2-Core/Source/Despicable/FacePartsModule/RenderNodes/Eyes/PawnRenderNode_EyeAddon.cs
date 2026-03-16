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
        private Material _sideMaterial;

        public void Initialize(Graphic_Single template, Material southMaterial, Material sideMaterial)
        {
            CopyInstanceFields(template, this);
            _southMaterial = southMaterial;
            _sideMaterial = sideMaterial;
        }

        public override Material MatSingle => _southMaterial ?? base.MatSingle;

        public override Material MatAt(Rot4 rot, Thing thing = null)
        {
            if (rot == Rot4.East || rot == Rot4.West)
                return _sideMaterial ?? _southMaterial ?? base.MatSingle;

            return _southMaterial ?? base.MatSingle;
        }
    }

    CompFaceParts compFaceParts;
    private readonly Dictionary<string, Verse.Graphic> _runtimeGraphicsByKey = new();
    private Verse.Graphic _cachedEyeBaseMultiGraphic;
    private int _cachedEyeBaseMultiKey = int.MinValue;

    public PawnRenderNode_EyeAddon(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree) : base(pawn, props, tree)
    {
        compFaceParts = pawn.TryGetComp<CompFaceParts>();
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


    private bool IsEyeBaseNode()
    {
        string label = props?.debugLabel;
        return !label.NullOrEmpty() && (label.StartsWith("FacePart_EyeBase", StringComparison.OrdinalIgnoreCase) || label.StartsWith("FacePart_AutoEyePatch", StringComparison.OrdinalIgnoreCase));
    }


    private bool IsAutoEyePatchNode()
    {
        string label = props?.debugLabel;
        return !label.NullOrEmpty() && label.StartsWith("FacePart_AutoEyePatch", StringComparison.OrdinalIgnoreCase);
    }

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

    private bool IsRightEyeBaseNode()
    {
        string label = props?.debugLabel;
        return !label.NullOrEmpty() && label.EndsWith("_R", StringComparison.OrdinalIgnoreCase);
    }

    private Verse.Graphic BuildRuntimeGraphic(Pawn pawn, Texture2D runtimeTexture)
    {
        if (runtimeTexture == null)
            return null;

        Vector2 drawSize = ResolveRuntimeDrawSize(pawn);
        Color tint = pawn != null ? ColorFor(pawn) : Color.white;
        if (tint.a <= 0f)
            tint = Color.white;

        string cacheKey = BuildRuntimeGraphicKey(runtimeTexture, drawSize, tint);
        if (_runtimeGraphicsByKey.TryGetValue(cacheKey, out Verse.Graphic cachedGraphic))
            return cachedGraphic;

        Shader shader = ShaderDatabase.CutoutSkinOverlay;

        Graphic_Single template = GraphicDatabase.Get<Graphic_Single>("FaceParts/Details/detail_empty", shader, drawSize, tint) as Graphic_Single;
        if (template == null || template.MatSingle == null)
            return null;

        Graphic_Single clone = (Graphic_Single)FormatterServices.GetUninitializedObject(typeof(Graphic_Single));
        CopyInstanceFields(template, clone);

        Material runtimeMat = new Material(template.MatSingle);
        runtimeMat.mainTexture = runtimeTexture;
        AssignMaterialFields(clone, runtimeMat);

        _runtimeGraphicsByKey[cacheKey] = clone;
        return clone;
    }

    private Verse.Graphic BuildRuntimeMultiGraphic(Pawn pawn, Texture2D southTexture, Texture2D sideTexture)
    {
        if (southTexture == null || sideTexture == null)
            return null;

        Color tint = pawn != null ? ColorFor(pawn) : Color.white;
        if (tint.a <= 0f)
            tint = Color.white;

        unchecked
        {
            Vector2 drawSize = ResolveRuntimeDrawSize(pawn);

            int key = 17;
            key = (key * 31) + southTexture.GetInstanceID();
            key = (key * 31) + sideTexture.GetInstanceID();
            key = (key * 31) + tint.GetHashCode();
            key = (key * 31) + drawSize.GetHashCode();

            if (_cachedEyeBaseMultiGraphic != null && _cachedEyeBaseMultiKey == key)
                return _cachedEyeBaseMultiGraphic;

            Shader shader = ShaderDatabase.CutoutSkinOverlay;
            Graphic_Single template = GraphicDatabase.Get<Graphic_Single>("FaceParts/Details/detail_empty", shader, drawSize, tint) as Graphic_Single;
            if (template == null || template.MatSingle == null)
                return null;

            Material southMat = new Material(template.MatSingle);
            southMat.mainTexture = southTexture;

            Material sideMat = new Material(template.MatSingle);
            sideMat.mainTexture = sideTexture;

            RuntimeEyeBaseMultiGraphic graphic = new();
            graphic.Initialize(template, southMat, sideMat);

            _cachedEyeBaseMultiGraphic = graphic;
            _cachedEyeBaseMultiKey = key;
            return graphic;
        }
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

        return BuildRuntimeMultiGraphic(pawn, southSelection.RuntimeTexture, sideSelection.RuntimeTexture);
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

        if (compFaceParts != null)
        {
            ExpressionDef baseExpression = compFaceParts?.baseExpression;
            ExpressionDef animExpression = compFaceParts?.animExpression;

            switch (this.props.debugLabel)
            {
                case "FacePart_Eye_L":
                case "FacePart_Eye_R":
                    if (animExpression?.texPathEyes != null)
                        texPath = animExpression.texPathEyes;
                    else if (baseExpression?.texPathEyes != null)
                        texPath = baseExpression.texPathEyes;
                    else if (compFaceParts.eyeStyleDef != null && !compFaceParts.eyeStyleDef.texPath.NullOrEmpty())
                        texPath = compFaceParts.eyeStyleDef.texPath;
                    break;
                case "FacePart_Detail_L":
                case "FacePart_Detail_R":
                    texPath = compFaceParts.GetBaseDetailTexPath();
                    break;
                case "FacePart_SecondaryDetail_L":
                case "FacePart_SecondaryDetail_R":
                    if (compFaceParts.animExpression != null)
                        texPath = animExpression?.texPathDetail ?? "FaceParts/Details/detail_empty";
                    else
                        texPath = "FaceParts/Details/detail_empty";
                    break;
            }
        }

        if (texPath.NullOrEmpty())
            texPath = "FaceParts/Details/detail_empty";

        texPath = FacePartsUtil.GetEyePath(pawn, texPath); // Ensures it's a valid path
        return GraphicDatabase.Get<Graphic_Multi>(texPath, ShaderDatabase.CutoutSkinOverlay, ResolveRuntimeDrawSize(pawn), ColorFor(pawn));
    }
}
