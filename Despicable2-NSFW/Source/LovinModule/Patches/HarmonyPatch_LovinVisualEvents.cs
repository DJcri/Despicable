using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Despicable;

internal static class LovinVisualEventPatchUtil
{
    // Guardrail-Allow-Static: Reflection cache for locating the owning Pawn on patched trackers/handlers; the mapping is process-stable and shared across event hooks.
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

    internal static IEnumerable<MethodBase> ExistingMethods(string typeName, params string[] methodNames)
    {
        Type type = AccessTools.TypeByName(typeName);
        if (type == null)
            yield break;

        HashSet<MethodBase> yielded = new();
        List<MethodInfo> declaredMethods = AccessTools.GetDeclaredMethods(type);
        for (int i = 0; i < methodNames.Length; i++)
        {
            for (int j = 0; j < declaredMethods.Count; j++)
            {
                MethodInfo method = declaredMethods[j];
                if (method == null || method.Name != methodNames[i])
                    continue;

                if (yielded.Add(method))
                    yield return method;
            }
        }
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
        catch
        {
        }

        return null;
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_LovinVisual_JobEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in LovinVisualEventPatchUtil.ExistingMethods("Verse.AI.Pawn_JobTracker", "StartJob", "EndCurrentJob", "CleanupCurrentJob"))
            yield return method;
    }

    private static void Postfix(object __instance)
    {
        Pawn pawn = LovinVisualEventPatchUtil.TryGetPawn(__instance);
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return;

        LovinVisualRuntime.SyncPawn(pawn);
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_LovinVisual_HealthEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in LovinVisualEventPatchUtil.ExistingMethods("Verse.HediffSet", "DirtyCache", "AddDirect", "AddHediff", "RemoveHediff", "Clear", "CullMissingPartsCommonAncestors"))
            yield return method;

        foreach (MethodBase method in LovinVisualEventPatchUtil.ExistingMethods("Verse.Pawn_HealthTracker", "Notify_HediffChanged", "MakeDowned", "NotifyPlayerOfKilled"))
            yield return method;
    }

    private static void Postfix(object __instance)
    {
        Pawn pawn = LovinVisualEventPatchUtil.TryGetPawn(__instance);
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return;

        LovinVisualRuntime.SyncPawn(pawn, force: true, refreshVisuals: false);
        LovinVisualRuntime.NotifyPotentialRenderStateChanged(pawn);
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_LovinVisual_LifeStageEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in LovinVisualEventPatchUtil.ExistingMethods("Verse.Pawn_AgeTracker", "BirthdayBiological", "BirthdayChronological", "ResetAgeReversalDemand"))
            yield return method;
    }

    private static void Postfix(object __instance)
    {
        Pawn pawn = LovinVisualEventPatchUtil.TryGetPawn(__instance);
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return;

        LovinVisualRuntime.SyncPawn(pawn, force: true, refreshVisuals: false);
        LovinVisualRuntime.NotifyPotentialRenderStateChanged(pawn);
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_LovinVisual_ApparelEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in LovinVisualEventPatchUtil.ExistingMethods("RimWorld.Pawn_ApparelTracker", "Wear", "Remove", "TryDrop", "Notify_ApparelChanged"))
            yield return method;

        foreach (MethodBase method in LovinVisualEventPatchUtil.ExistingMethods("Verse.Pawn_ApparelTracker", "Wear", "Remove", "TryDrop", "Notify_ApparelChanged"))
            yield return method;
    }

    private static void Postfix(object __instance)
    {
        Pawn pawn = LovinVisualEventPatchUtil.TryGetPawn(__instance);
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return;

        LovinVisualRuntime.NotifyPotentialRenderStateChanged(pawn);
    }
}
