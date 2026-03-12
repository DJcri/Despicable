using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace Despicable;
public static class VerseKeyframeCompat
{
    private sealed class Accessor
    {
        public Func<Verse.Keyframe, Vector3> Getter;
        public Action<Verse.Keyframe, Vector3> Setter;
    }

    private static readonly Dictionary<Type, Accessor> AccessorsByType = new();

    public static void ResetRuntimeState()
    {
        AccessorsByType.Clear();
    }

    public static Vector3 GetScaleOrDefault(Verse.Keyframe keyframe, Vector3? fallback = null)
    {
        Vector3 fallbackValue = fallback ?? Vector3.one;
        if (keyframe == null)
            return fallbackValue;

        try
        {
            Accessor accessor = GetOrCreateAccessor(keyframe.GetType());
            if (accessor?.Getter != null)
                return accessor.Getter(keyframe);
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "VerseKeyframeCompat.GetScaleOrDefault",
                "VerseKeyframeCompat failed to read keyframe scale.",
                ex);
        }

        return fallbackValue;
    }

    public static bool TrySetScale(Verse.Keyframe keyframe, Vector3 scale)
    {
        if (keyframe == null)
            return false;

        try
        {
            Accessor accessor = GetOrCreateAccessor(keyframe.GetType());
            if (accessor?.Setter == null)
                return false;

            accessor.Setter(keyframe, scale);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Accessor GetOrCreateAccessor(Type keyframeType)
    {
        if (keyframeType == null)
            return null;

        if (AccessorsByType.TryGetValue(keyframeType, out Accessor cached))
            return cached;

        Accessor created = CreateAccessor(keyframeType);
        AccessorsByType[keyframeType] = created;
        return created;
    }

    private static Accessor CreateAccessor(Type keyframeType)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        string[] candidateNames = { "scale", "Scale", "drawScale", "DrawScale" };

        for (Type current = keyframeType; current != null; current = current.BaseType)
        {
            for (int i = 0; i < candidateNames.Length; i++)
            {
                string candidateName = candidateNames[i];

                FieldInfo field = current.GetField(candidateName, Flags);
                if (field != null)
                {
                    Accessor fieldAccessor = CreateFieldAccessor(field);
                    if (fieldAccessor != null)
                        return fieldAccessor;
                }

                PropertyInfo property = current.GetProperty(candidateName, Flags);
                if (property != null && property.GetIndexParameters().Length == 0)
                {
                    Accessor propertyAccessor = CreatePropertyAccessor(property);
                    if (propertyAccessor != null)
                        return propertyAccessor;
                }
            }
        }

        return new Accessor();
    }

    private static Accessor CreateFieldAccessor(FieldInfo field)
    {
        if (field == null)
            return null;

        Type fieldType = field.FieldType;
        if (fieldType == typeof(Vector3))
        {
            return new Accessor
            {
                Getter = (Verse.Keyframe keyframe) => (Vector3)field.GetValue(keyframe),
                Setter = (Verse.Keyframe keyframe, Vector3 scale) => field.SetValue(keyframe, scale)
            };
        }

        if (Nullable.GetUnderlyingType(fieldType) == typeof(Vector3))
        {
            return new Accessor
            {
                Getter = (Verse.Keyframe keyframe) =>
                {
                    object value = field.GetValue(keyframe);
                    return value is Vector3 vector ? vector : Vector3.one;
                },
                Setter = (Verse.Keyframe keyframe, Vector3 scale) => field.SetValue(keyframe, (Vector3?)scale)
            };
        }

        return null;
    }

    private static Accessor CreatePropertyAccessor(PropertyInfo property)
    {
        if (property == null)
            return null;

        Type propertyType = property.PropertyType;
        if (propertyType == typeof(Vector3))
        {
            return new Accessor
            {
                Getter = (Verse.Keyframe keyframe) => (Vector3)property.GetValue(keyframe, null),
                Setter = property.CanWrite ? (Verse.Keyframe keyframe, Vector3 scale) => property.SetValue(keyframe, scale, null) : null
            };
        }

        if (Nullable.GetUnderlyingType(propertyType) == typeof(Vector3))
        {
            return new Accessor
            {
                Getter = (Verse.Keyframe keyframe) =>
                {
                    object value = property.GetValue(keyframe, null);
                    return value is Vector3 vector ? vector : Vector3.one;
                },
                Setter = property.CanWrite ? (Verse.Keyframe keyframe, Vector3 scale) => property.SetValue(keyframe, (Vector3?)scale, null) : null
            };
        }

        return null;
    }
}
