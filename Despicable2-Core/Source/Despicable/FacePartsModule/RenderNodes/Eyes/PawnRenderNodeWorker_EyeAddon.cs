using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace Despicable;
public class PawnRenderNodeWorker_EyeAddon : PawnRenderNodeWorker_FacePart
{
    private sealed class BaseLayerSetter
    {
        public readonly FieldInfo Field;
        public readonly PropertyInfo Property;
        public readonly bool UsesFloat;

        public BaseLayerSetter(FieldInfo field, PropertyInfo property, bool usesFloat)
        {
            Field = field;
            Property = property;
            UsesFloat = usesFloat;
        }

        public void SetValue(object target, int baseLayer)
        {
            if (Field != null)
            {
                Field.SetValue(target, UsesFloat ? (object)(float)baseLayer : baseLayer);
                return;
            }

            if (Property != null && Property.CanWrite)
                Property.SetValue(target, UsesFloat ? (object)(float)baseLayer : baseLayer, null);
        }
    }

    private static readonly Dictionary<Type, BaseLayerSetter> BaseLayerSetterByPropsType = new();
    private static readonly object BaseLayerSetterCacheLock = new();

    public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms)
    {
        if (base.CanDrawNow(node, parms))
        {
            // Don't render expression texPath if portrait, use face style instead
            Pawn pawn = node.tree.pawn;
            CompFaceParts compFaceParts = ResolveCompFaceParts(node, pawn);

            // Don't render right-counterpart when facing west, as the mirrored textures already flip automatically!
            if (IsRightCounterpartNode(node)
                && parms.facing != Rot4.South)
            {
                return false;
            }

            string debugLabel = GetDebugLabel(node);
            bool isEyeDetailNode = !debugLabel.NullOrEmpty()
                && (debugLabel.Equals("FacePart_EyeDetail_L", StringComparison.OrdinalIgnoreCase)
                    || debugLabel.Equals("FacePart_EyeDetail_R", StringComparison.OrdinalIgnoreCase));
            if (isEyeDetailNode && parms.facing == Rot4.South)
            {
                FacePartSideMode effectiveSideMode = compFaceParts?.GetResolvedEyeDetailSideMode() ?? FacePartSideMode.Both;
                if (effectiveSideMode == FacePartSideMode.LeftOnly && IsRightCounterpartNode(node))
                    return false;

                if (effectiveSideMode == FacePartSideMode.RightOnly
                    && debugLabel.EndsWith("_L", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Don't render face parts if facing north
            if (parms.facing == Rot4.North)
            {
                return false;
            }

            // Check for eye-shaping genes when the gene tracker exists.
            // Without Biotech, or on pawns that simply do not have genes initialized,
            // treat this as "no interfering genes" rather than a hard failure.
            if (compFaceParts?.HasBlockingEyeGeneThisTick() == true)
                return false;

            if (compFaceParts?.IsForeignEyeVisualBlockedForNodeThisTick(node, parms.facing) == true)
                return false;

            ApplyFacingSpecificBaseLayer(node, parms.facing);
            return true;
        }
        return false;
    }

    private static void ApplyFacingSpecificBaseLayer(PawnRenderNode node, Rot4 facing)
    {
        if (!FacePartRenderNodeContextCache.IsBrowNode(node))
            return;

        object props = node?.Props;
        if (props == null)
            return;

        BaseLayerSetter setter = GetBaseLayerSetter(props.GetType());
        if (setter == null)
            return;

        int baseLayer = facing == Rot4.South ? 57 : 59;
        setter.SetValue(props, baseLayer);
    }

    private static BaseLayerSetter GetBaseLayerSetter(Type propsType)
    {
        if (propsType == null)
            return null;

        lock (BaseLayerSetterCacheLock)
        {
            if (BaseLayerSetterByPropsType.TryGetValue(propsType, out BaseLayerSetter cached))
                return cached;

            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo field = propsType.GetField("baseLayer", Flags);
            if (field != null && (field.FieldType == typeof(float) || field.FieldType == typeof(int)))
            {
                cached = new BaseLayerSetter(field, null, field.FieldType == typeof(float));
                BaseLayerSetterByPropsType[propsType] = cached;
                return cached;
            }

            PropertyInfo property = propsType.GetProperty("baseLayer", Flags);
            if (property != null && property.CanWrite && (property.PropertyType == typeof(float) || property.PropertyType == typeof(int)))
            {
                cached = new BaseLayerSetter(null, property, property.PropertyType == typeof(float));
                BaseLayerSetterByPropsType[propsType] = cached;
                return cached;
            }

            BaseLayerSetterByPropsType[propsType] = null;
            return null;
        }
    }

    public override Vector3 OffsetFor(PawnRenderNode node, PawnDrawParms parms, out Vector3 pivot)
    {
        Pawn pawn = parms.pawn;
        HeadTypeDef headType = pawn.story.headType;
        Vector3 vector = base.OffsetFor(node, parms, out pivot);

        float eyeOffset = 0.13f;
        if (headType.eyeOffsetEastWest.HasValue)
            eyeOffset = headType.eyeOffsetEastWest.Value.x;

        float eyeSizeFactor = pawn.ageTracker.CurLifeStage.eyeSizeFactor ?? 1f;
        Rot4 facing = parms.facing;
        Vector3 side = Vector3.zero;
        if (facing == Rot4.East)
        {
            side = Vector3.right;
        }
        else if (facing == Rot4.West)
        {
            side = Vector3.left;
        }

        if (facing == Rot4.South)
        {
            float southDelta = 0.09f * eyeSizeFactor;
            if (node.Props.flipGraphic)
            {
                vector.x += southDelta;
            }
            else
            {
                vector.x -= southDelta;
            }
        }
        else
        {
            vector += side * (eyeOffset * eyeSizeFactor);
        }

        vector *= eyeSizeFactor;
        return vector;
    }

    public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
    {
        return base.ScaleFor(node, parms);
    }
}
