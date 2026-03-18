using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

[HarmonyPatch]
internal static class HarmonyPatch_AnimalTrainingChance_LocalRep
{
    private const string PatchId = "HKPatch.AnimalTrainingChanceLocalRep";
    private static List<MethodBase> _targets;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        // Superseded by the direct TryTrain patch in HarmonyPatch_AnimalTrainingScope.
        return false;
    }

    [HarmonyTargetMethods]
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return _targets ?? (IEnumerable<MethodBase>)Array.Empty<MethodBase>();
    }

    private static IEnumerable<MethodBase> FindTargets()
    {
        Type statExtensionType = AccessTools.TypeByName("Verse.StatExtension");
        if (statExtensionType == null)
            yield break;

        foreach (MethodBase method in HKPatchTargetUtil.FindMethods(
                     statExtensionType,
                     BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                     static method =>
                     {
                         if (!string.Equals(method.Name, "GetStatValue", StringComparison.Ordinal))
                             return false;
                         if (method.ReturnType != typeof(float))
                             return false;

                         ParameterInfo[] parameters = method.GetParameters();
                         return parameters != null
                             && parameters.Length >= 2
                             && typeof(Thing).IsAssignableFrom(parameters[0].ParameterType)
                             && parameters[1].ParameterType == typeof(StatDef);
                     }))
        {
            yield return method;
        }
    }

    private static void Postfix(Thing __0, StatDef __1, ref float __result)
    {
        try
        {
            if (!HKSettingsUtil.EnableLocalRep)
                return;
            if (__0 is not Pawn trainer)
                return;
            if (__1 != StatDefOf.TrainAnimalChance)
                return;
            if (!HKHookUtilSafe.ActorIsHero(trainer))
                return;
            if (!HKAnimalInteractionContext.TryGetTrainingPair(out string trainerPawnId, out string animalPawnId))
                return;

            if (!string.Equals(trainerPawnId, trainer.GetUniqueLoadID(), StringComparison.Ordinal))
                return;
            if (!LocalReputationUtility.TryGetDirectPawnInfluenceIndex(trainerPawnId, animalPawnId, out float directInfluenceIndex))
                return;

            float multiplier = HKBalanceTuning.GetAnimalTrainingChanceMultiplier(directInfluenceIndex);
            if (Math.Abs(multiplier - 1f) < 0.001f)
                return;

            __result *= multiplier;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_AnimalTrainingChance_LocalRep:1", "Animal training chance patch suppressed an exception.", ex);
        }
    }
}
