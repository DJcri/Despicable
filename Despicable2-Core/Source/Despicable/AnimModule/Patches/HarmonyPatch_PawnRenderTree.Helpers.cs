using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Despicable;
public static partial class HarmonyPatch_PawnRenderTree_TryGetMatrix
{
    private static Func<Verse.Keyframe, Vector3> ResolveKeyframeScaleGetter(Type keyframeType)
    {
        if (keyframeType == null)
        {
            return null;
        }

        const System.Reflection.BindingFlags BindingFlags =
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.NonPublic;

        string[] candidateNames = { "scale", "Scale", "drawScale", "DrawScale" };

        foreach (string candidateName in candidateNames)
        {
            var fieldInfo = keyframeType.GetField(candidateName, BindingFlags);
            if (fieldInfo != null)
            {
                Type fieldType = fieldInfo.FieldType;
                if (fieldType == typeof(Vector3))
                {
                    return (Verse.Keyframe keyframe) => (Vector3)fieldInfo.GetValue(keyframe);
                }

                if (Nullable.GetUnderlyingType(fieldType) == typeof(Vector3))
                {
                    return (Verse.Keyframe keyframe) =>
                    {
                        object value = fieldInfo.GetValue(keyframe);
                        return value is Vector3 vector ? vector : Vector3.one;
                    };
                }
            }

            var propertyInfo = keyframeType.GetProperty(candidateName, BindingFlags);
            if (propertyInfo != null && propertyInfo.GetIndexParameters().Length == 0)
            {
                Type propertyType = propertyInfo.PropertyType;
                if (propertyType == typeof(Vector3))
                {
                    return (Verse.Keyframe keyframe) => (Vector3)propertyInfo.GetValue(keyframe, null);
                }

                if (Nullable.GetUnderlyingType(propertyType) == typeof(Vector3))
                {
                    return (Verse.Keyframe keyframe) =>
                    {
                        object value = propertyInfo.GetValue(keyframe, null);
                        return value is Vector3 vector ? vector : Vector3.one;
                    };
                }
            }
        }

        // Some builds store scale inside another struct; we do not chase that here.
        return null;
    }

    private static bool TrySampleScaleInterpolated(KeyframeAnimationPart kap, int tick, out Vector3 scale)
    {
        scale = Vector3.one;
        if (kap?.keyframes.NullOrEmpty() != false)
        {
            return false;
        }

        Verse.Keyframe first = kap.keyframes[0];
        Verse.Keyframe last = kap.keyframes[kap.keyframes.Count - 1];

        if (tick <= first.tick)
        {
            scale = ReadScaleFromVerseKeyframe(first);
            return true;
        }

        if (tick >= last.tick)
        {
            scale = ReadScaleFromVerseKeyframe(last);
            return true;
        }

        Verse.Keyframe a = first;
        Verse.Keyframe b = last;

        for (int i = 1; i < kap.keyframes.Count; i++)
        {
            Verse.Keyframe cur = kap.keyframes[i];
            if (cur.tick >= tick)
            {
                b = cur;
                a = kap.keyframes[i - 1];
                break;
            }
        }

        if (a.tick == b.tick)
        {
            scale = ReadScaleFromVerseKeyframe(a);
            return true;
        }

        float t = Mathf.InverseLerp(a.tick, b.tick, tick);
        scale = Vector3.Lerp(ReadScaleFromVerseKeyframe(a), ReadScaleFromVerseKeyframe(b), t);
        return true;
    }

    private static Vector3 ReadScaleFromVerseKeyframe(Verse.Keyframe kf)
    {
        if (kf == null)
        {
            return Vector3.one;
        }

        try
        {
            Type t = kf.GetType();
            if (!keyframeScaleGettersByType.TryGetValue(t, out Func<Verse.Keyframe, Vector3> getter))
            {
                getter = ResolveKeyframeScaleGetter(t) ?? ResolveKeyframeScaleGetter(t.BaseType);
                keyframeScaleGettersByType[t] = getter; // may be null
            }

            if (getter != null)
            {
                return getter(kf);
            }
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_PawnRenderTree.EmptyCatch:1",
                "PawnRenderTree reflection fallback failed.",
                e);
        }

        return Vector3.one;
    }

    private static bool TryGetVanillaMatrix(PawnRenderTree tree, PawnRenderNode n, ref PawnDrawParms parms, out Matrix4x4 m)
    {
        m = default;
        if (tree == null || n == null)
        {
            return false;
        }

        bool prev = skipWorkshopDeltas;
        skipWorkshopDeltas = true;
        try
        {
            return tree.TryGetMatrix(n, parms, out m);
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "HarmonyPatch_PawnRenderTree.EmptyCatch:2",
                "PawnRenderTree vanilla matrix fallback failed.",
                e);
            return false;
        }
        finally
        {
            skipWorkshopDeltas = prev;
        }
    }

    private static bool TrySampleOffsetAngleInterpolated(KeyframeAnimationPart kap, int tick, out Vector3 offset, out float angle)
    {
        offset = Vector3.zero;
        angle = 0f;
        if (kap?.keyframes.NullOrEmpty() != false)
        {
            return false;
        }

        // Keyframes are expected to be sorted, but handle edge cases defensively.
        Verse.Keyframe first = kap.keyframes[0];
        Verse.Keyframe last = kap.keyframes[kap.keyframes.Count - 1];

        if (tick <= first.tick)
        {
            offset = first.offset;
            angle = first.angle;
            return true;
        }

        if (tick >= last.tick)
        {
            offset = last.offset;
            angle = last.angle;
            return true;
        }

        Verse.Keyframe a = first;
        Verse.Keyframe b = last;

        for (int i = 1; i < kap.keyframes.Count; i++)
        {
            Verse.Keyframe cur = kap.keyframes[i];
            if (cur.tick >= tick)
            {
                b = cur;
                a = kap.keyframes[i - 1];
                break;
            }
        }

        if (a.tick == b.tick)
        {
            offset = a.offset;
            angle = a.angle;
            return true;
        }

        float t = Mathf.InverseLerp(a.tick, b.tick, tick);
        offset = Vector3.Lerp(a.offset, b.offset, t);
        angle = Mathf.LerpAngle(a.angle, b.angle, t);
        return true;
    }
}

public static partial class HarmonyPatch_PawnRenderTree_AdjustParms
{
    private static bool TryMarkRecacheRecursive(PawnRenderTree tree, PawnRenderNode node, int tick)
    {
        if (node?.children == null)
        {
            return false;
        }

        foreach (PawnRenderNode child in node.children)
        {
            var childWorker = child?.AnimationWorker as AnimationWorker_ExtendedKeyframes;
            if (childWorker != null
                && tree.TryGetAnimationPartForNode(child, out AnimationPart childPart)
                && childPart != null
                && childWorker.ShouldRecache(tick, childPart))
            {
                child.requestRecache = true;
                return true;
            }

            if (TryMarkRecacheRecursive(tree, child, tick))
            {
                return true;
            }
        }

        return false;
    }
}
