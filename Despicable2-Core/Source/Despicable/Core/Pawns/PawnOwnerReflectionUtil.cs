using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Despicable;
public static class PawnOwnerReflectionUtil
{
    private static readonly Dictionary<Type, MemberInfo> PawnMemberByType = new();
    private static readonly Dictionary<Type, FieldInfo> PawnFieldByType = new();

    public static void ResetRuntimeState()
    {
        PawnMemberByType.Clear();
        PawnFieldByType.Clear();
    }

    public static Pawn TryGetPawn(object instance, string warnKeyPrefix = null, string warnMessagePrefix = null)
    {
        if (instance == null)
            return null;

        if (instance is Pawn pawn)
            return pawn;

        Type type = instance.GetType();
        if (!PawnMemberByType.TryGetValue(type, out MemberInfo member))
        {
            member = ResolvePawnMember(type);
            PawnMemberByType[type] = member;
        }

        return ReadPawnMember(instance, member, warnKeyPrefix, warnMessagePrefix);
    }

    public static FieldInfo ResolvePawnField(Type type)
    {
        if (type == null)
            return null;

        if (PawnFieldByType.TryGetValue(type, out FieldInfo cached))
            return cached;

        string[] candidateFields = { "pawn", "Pawn", "_pawn", "pawnInt" };
        for (int i = 0; i < candidateFields.Length; i++)
        {
            FieldInfo field = AccessTools.Field(type, candidateFields[i]);
            if (field != null && !field.IsStatic && typeof(Pawn).IsAssignableFrom(field.FieldType))
            {
                PawnFieldByType[type] = field;
                return field;
            }
        }

        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (field != null && !field.IsStatic && typeof(Pawn).IsAssignableFrom(field.FieldType))
            {
                PawnFieldByType[type] = field;
                return field;
            }
        }

        PawnFieldByType[type] = null;
        return null;
    }

    public static MemberInfo ResolvePawnMember(Type type)
    {
        if (type == null)
            return null;

        if (PawnMemberByType.TryGetValue(type, out MemberInfo cached))
            return cached;

        FieldInfo field = ResolvePawnField(type);
        if (field != null)
        {
            PawnMemberByType[type] = field;
            return field;
        }

        string[] candidateProperties = { "pawn", "Pawn" };
        for (int i = 0; i < candidateProperties.Length; i++)
        {
            PropertyInfo property = AccessTools.Property(type, candidateProperties[i]);
            if (property != null && property.GetIndexParameters().Length == 0 && typeof(Pawn).IsAssignableFrom(property.PropertyType))
            {
                PawnMemberByType[type] = property;
                return property;
            }
        }

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < properties.Length; i++)
        {
            PropertyInfo property = properties[i];
            if (property != null && property.GetIndexParameters().Length == 0 && typeof(Pawn).IsAssignableFrom(property.PropertyType))
            {
                PawnMemberByType[type] = property;
                return property;
            }
        }

        PawnMemberByType[type] = null;
        return null;
    }

    public static Pawn ReadPawnMember(object instance, MemberInfo member, string warnKeyPrefix = null, string warnMessagePrefix = null)
    {
        if (instance == null || member == null)
            return null;

        try
        {
            if (member is FieldInfo field)
                return field.GetValue(instance) as Pawn;

            if (member is PropertyInfo property)
                return property.GetValue(instance, null) as Pawn;
        }
        catch (Exception ex)
        {
            if (!warnKeyPrefix.NullOrEmpty() && !warnMessagePrefix.NullOrEmpty())
            {
                string memberName = member.Name ?? "<unknown>";
                string typeName = instance.GetType().FullName ?? "<null>";
                Despicable.Core.DebugLogger.WarnExceptionOnce(
                    warnKeyPrefix + ":" + typeName + ":" + memberName,
                    warnMessagePrefix + $" '{memberName}' from '{typeName}'.",
                    ex);
            }
        }

        return null;
    }
}
