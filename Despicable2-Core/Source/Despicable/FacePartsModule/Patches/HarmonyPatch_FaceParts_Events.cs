using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Despicable;

internal static class FacePartsEventPatchUtil
{
    private static readonly Dictionary<Type, MemberInfo> PawnMemberByType = new();

    internal static Pawn TryGetPawn(object instance)
    {
        if (instance == null)
            return null;

        if (instance is Pawn pawn)
            return pawn;

        Type type = instance.GetType();
        if (PawnMemberByType.TryGetValue(type, out MemberInfo cached))
            return ReadPawnMember(instance, cached);

        MemberInfo member = ResolvePawnMember(type);
        PawnMemberByType[type] = member;
        return ReadPawnMember(instance, member);
    }

    private static MemberInfo ResolvePawnMember(Type type)
    {
        if (type == null)
            return null;

        string[] candidateFields = { "pawn", "Pawn", "_pawn", "pawnInt" };
        for (int i = 0; i < candidateFields.Length; i++)
        {
            FieldInfo field = AccessTools.Field(type, candidateFields[i]);
            if (field != null && typeof(Pawn).IsAssignableFrom(field.FieldType))
                return field;
        }

        string[] candidateProperties = { "pawn", "Pawn" };
        for (int i = 0; i < candidateProperties.Length; i++)
        {
            PropertyInfo property = AccessTools.Property(type, candidateProperties[i]);
            if (property != null && typeof(Pawn).IsAssignableFrom(property.PropertyType))
                return property;
        }

        FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo field = fields[i];
            if (field != null && typeof(Pawn).IsAssignableFrom(field.FieldType))
                return field;
        }

        PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < properties.Length; i++)
        {
            PropertyInfo property = properties[i];
            if (property != null && typeof(Pawn).IsAssignableFrom(property.PropertyType) && property.GetIndexParameters().Length == 0)
                return property;
        }

        return null;
    }

    private static Pawn ReadPawnMember(object instance, MemberInfo member)
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
            string memberName = member?.Name ?? "<unknown>";
            string typeName = instance?.GetType().FullName ?? "<null>";
            Despicable.Core.DebugLogger.WarnExceptionOnce(
                "FacePartsEventPatchUtil.ReadPawnMember:" + typeName + ":" + memberName,
                $"FaceParts event patch could not read pawn member '{memberName}' from '{typeName}'.",
                ex);
        }

        return null;
    }

    internal static void QueueFromInstance(object instance, FacePartsEventMask mask)
    {
        Pawn pawn = TryGetPawn(instance);
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return;

        FacePartsEventRuntime.Queue(pawn, mask);
    }

    internal static IEnumerable<MethodBase> ExistingMethods(string typeName, params string[] methodNames)
    {
        Type type = AccessTools.TypeByName(typeName);
        if (type == null)
            yield break;

        HashSet<MethodBase> yielded = new();
        for (int i = 0; i < methodNames.Length; i++)
        {
            List<MethodInfo> methods = AccessTools.GetDeclaredMethods(type);
            for (int j = 0; j < methods.Count; j++)
            {
                MethodInfo method = methods[j];
                if (method == null || method.Name != methodNames[i])
                    continue;

                if (yielded.Add(method))
                    yield return method;
            }
        }
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_FaceParts_DraftedEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        Type drafterType = AccessTools.TypeByName("RimWorld.Pawn_DraftController") ?? AccessTools.TypeByName("Pawn_DraftController");
        if (drafterType != null)
        {
            MethodInfo setter = AccessTools.PropertySetter(drafterType, "Drafted");
            if (setter != null)
                yield return setter;
        }
    }

    private static void Postfix(object __instance)
    {
        FacePartsEventPatchUtil.QueueFromInstance(__instance, FacePartsEventMask.Drafted);
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_FaceParts_JobEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in FacePartsEventPatchUtil.ExistingMethods("Verse.AI.Pawn_JobTracker", "StartJob", "EndCurrentJob", "CleanupCurrentJob"))
            yield return method;
    }

    private static void Postfix(object __instance)
    {
        FacePartsEventPatchUtil.QueueFromInstance(__instance, FacePartsEventMask.Job);
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_FaceParts_MentalEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in FacePartsEventPatchUtil.ExistingMethods("Verse.AI.MentalStateHandler", "TryStartMentalState", "RecoverFromState", "ClearMentalStateDirect", "Reset"))
            yield return method;
    }

    private static void Postfix(object __instance)
    {
        FacePartsEventPatchUtil.QueueFromInstance(__instance, FacePartsEventMask.Mental);
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_FaceParts_HealthEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in FacePartsEventPatchUtil.ExistingMethods("Verse.HediffSet", "DirtyCache", "AddDirect", "AddHediff", "RemoveHediff", "Clear", "CullMissingPartsCommonAncestors"))
            yield return method;

        foreach (MethodBase method in FacePartsEventPatchUtil.ExistingMethods("Verse.Pawn_HealthTracker", "Notify_HediffChanged", "MakeDowned", "NotifyPlayerOfKilled"))
            yield return method;
    }

    private static void Postfix(object __instance)
    {
        FacePartsEventPatchUtil.QueueFromInstance(__instance, FacePartsEventMask.Health);
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_FaceParts_RestEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in FacePartsEventPatchUtil.ExistingMethods("RimWorld.Need_Rest", "NeedInterval", "SetInitialLevel"))
            yield return method;
    }

    private static void Postfix(object __instance)
    {
        FacePartsEventPatchUtil.QueueFromInstance(__instance, FacePartsEventMask.Rest);
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_FaceParts_LifeStageEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in FacePartsEventPatchUtil.ExistingMethods("Verse.Pawn_AgeTracker", "BirthdayBiological", "BirthdayChronological", "ResetAgeReversalDemand"))
            yield return method;
    }

    private static void Postfix(object __instance)
    {
        FacePartsEventPatchUtil.QueueFromInstance(__instance, FacePartsEventMask.LifeStage);
    }
}
