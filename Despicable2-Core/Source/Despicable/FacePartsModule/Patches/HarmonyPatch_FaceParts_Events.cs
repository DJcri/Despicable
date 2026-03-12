using System.Collections.Generic;
using HarmonyLib;
using Verse;
using System;
using System.Reflection;

namespace Despicable;

internal static class FacePartsEventPatchUtil
{
    internal static Pawn TryGetPawn(object instance)
    {
        return PawnOwnerReflectionUtil.TryGetPawn(
            instance,
            warnKeyPrefix: "FacePartsEventPatchUtil.ReadPawnMember",
            warnMessagePrefix: "FaceParts event patch could not read pawn member");
    }

    internal static void QueueFromInstance(object instance, FacePartsEventMask mask)
    {
        Pawn pawn = TryGetPawn(instance);
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return;

        FacePartsEventRuntime.Queue(pawn, mask);
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
        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("Verse.AI.Pawn_JobTracker", "StartJob", "EndCurrentJob", "CleanupCurrentJob"))
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
        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("Verse.AI.MentalStateHandler", "TryStartMentalState", "RecoverFromState", "ClearMentalStateDirect", "Reset"))
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
        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("Verse.HediffSet", "DirtyCache", "AddDirect", "AddHediff", "RemoveHediff", "Clear", "CullMissingPartsCommonAncestors"))
            yield return method;

        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("Verse.Pawn_HealthTracker", "Notify_HediffChanged", "MakeDowned", "NotifyPlayerOfKilled"))
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
        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("RimWorld.Need_Rest", "NeedInterval", "SetInitialLevel"))
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
        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("Verse.Pawn_AgeTracker", "BirthdayBiological", "BirthdayChronological", "ResetAgeReversalDemand"))
            yield return method;
    }

    private static void Postfix(object __instance)
    {
        FacePartsEventPatchUtil.QueueFromInstance(__instance, FacePartsEventMask.LifeStage);
    }
}
