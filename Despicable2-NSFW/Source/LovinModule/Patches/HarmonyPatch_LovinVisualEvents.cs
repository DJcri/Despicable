using System.Collections.Generic;
using HarmonyLib;
using Verse;
using System;
using System.Reflection;
using Despicable.NSFW.Integrations;

namespace Despicable;

internal static class LovinVisualEventPatchUtil
{
    internal static Pawn TryGetPawn(object instance)
    {
        return PawnOwnerReflectionUtil.TryGetPawn(instance);
    }

}

[HarmonyPatch]
internal static class HarmonyPatch_LovinVisual_JobEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("Verse.AI.Pawn_JobTracker", "StartJob", "EndCurrentJob", "CleanupCurrentJob"))
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
internal static class HarmonyPatch_LovinVisual_HealthEvents
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
        Pawn pawn = LovinVisualEventPatchUtil.TryGetPawn(__instance);
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return;

        LovinVisualRuntime.SyncPawn(pawn, force: true, refreshVisuals: false);
        LovinVisualRuntime.NotifyPotentialRenderStateChanged(pawn);

        if (IntegrationGuards.IsGenderWorksLoaded())
            pawn.TryGetComp<CompAnatomyBootstrap>()?.NotifyPotentialAnatomyChange();
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_LovinVisual_LifeStageEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("Verse.Pawn_AgeTracker", "BirthdayBiological", "BirthdayChronological", "ResetAgeReversalDemand"))
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
internal static class HarmonyPatch_LovinVisual_GeneEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        HashSet<MethodBase> yielded = new();

        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("RimWorld.Pawn_GeneTracker", "AddGene", "RemoveGene", "Notify_GenesChanged", "SetXenotype", "SetXenotypeDirect", "SetXenotypeRaw", "Notify_GeneRemoved"))
        {
            if (yielded.Add(method))
                yield return method;
        }

        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("Pawn_GeneTracker", "AddGene", "RemoveGene", "Notify_GenesChanged", "SetXenotype", "SetXenotypeDirect", "SetXenotypeRaw", "Notify_GeneRemoved"))
        {
            if (yielded.Add(method))
                yield return method;
        }
    }

    private static void Postfix(object __instance)
    {
        Pawn pawn = LovinVisualEventPatchUtil.TryGetPawn(__instance);
        if (pawn == null || pawn.RaceProps?.Humanlike != true)
            return;

        LovinVisualRuntime.SyncPawn(pawn, force: true, refreshVisuals: false);
        LovinVisualRuntime.NotifyPotentialRenderStateChanged(pawn);
        pawn.TryGetComp<CompAnatomyBootstrap>()?.NotifyPotentialAnatomyChange();
    }
}

[HarmonyPatch]
internal static class HarmonyPatch_LovinVisual_ApparelEvents
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("RimWorld.Pawn_ApparelTracker", "Wear", "Remove", "TryDrop", "Notify_ApparelChanged"))
            yield return method;

        foreach (MethodBase method in PatchMethodDiscoveryUtil.ExistingMethods("Verse.Pawn_ApparelTracker", "Wear", "Remove", "TryDrop", "Notify_ApparelChanged"))
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
