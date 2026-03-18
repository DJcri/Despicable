using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Despicable.HeroKarma.Patches.HeroKarma;

[HarmonyPatch]
internal static class HarmonyPatch_AnimalBondChance_LocalRep
{
    private const string PatchId = "HKPatch.AnimalBondChanceLocalRep";
    private static MethodBase _target;

    [HarmonyPrepare]
    private static bool Prepare()
    {
        return HKPatchGuard.PrepareSingle(
            PatchId,
            "Animal bond chance from direct local reputation",
            HKPatchGuard.FeatureCoreKarma,
            required: true,
            target: FindTarget(),
            cached: out _target);
    }

    private static MethodBase TargetMethod()
    {
        return _target;
    }

    private static MethodBase FindTarget()
    {
        Type relationsUtilityType = AccessTools.TypeByName("RimWorld.RelationsUtility");
        if (relationsUtilityType == null)
            return null;

        foreach (MethodInfo method in relationsUtilityType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (method == null || !string.Equals(method.Name, "TryDevelopBondRelation", StringComparison.Ordinal))
                continue;

            ParameterInfo[] parameters = method.GetParameters();
            if (parameters == null || parameters.Length < 3)
                continue;
            if (!typeof(Pawn).IsAssignableFrom(parameters[0].ParameterType) || !typeof(Pawn).IsAssignableFrom(parameters[1].ParameterType))
                continue;
            if (parameters[2].ParameterType != typeof(float))
                continue;

            return method;
        }

        return null;
    }

    private static void Prefix(Pawn __0, Pawn __1, ref float __2)
    {
        try
        {
            if (!HKSettingsUtil.EnableLocalRep)
                return;

            ResolveHeroAndAnimal(__0, __1, out Pawn hero, out Pawn animal);
            if (hero == null || animal == null)
                return;

            string heroId = hero.GetUniqueLoadID();
            string animalId = animal.GetUniqueLoadID();
            if (heroId.NullOrEmpty() || animalId.NullOrEmpty())
                return;
            if (!LocalReputationUtility.TryGetDirectPawnInfluenceIndex(heroId, animalId, out float directInfluenceIndex))
                return;

            float multiplier = HKBalanceTuning.GetAnimalBondChanceMultiplier(directInfluenceIndex);
            if (Math.Abs(multiplier - 1f) < 0.001f)
                return;

            __2 *= multiplier;
        }
        catch (Exception ex)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_AnimalBondChance_LocalRep:1", "Animal bond chance patch suppressed an exception.", ex);
        }
    }

    private static void ResolveHeroAndAnimal(Pawn first, Pawn second, out Pawn hero, out Pawn animal)
    {
        hero = null;
        animal = null;

        if (first != null && second != null)
        {
            if (HKHookUtilSafe.ActorIsHero(first) && HKHookUtil.IsAnimal(second))
            {
                hero = first;
                animal = second;
                return;
            }

            if (HKHookUtilSafe.ActorIsHero(second) && HKHookUtil.IsAnimal(first))
            {
                hero = second;
                animal = first;
            }
        }
    }
}
