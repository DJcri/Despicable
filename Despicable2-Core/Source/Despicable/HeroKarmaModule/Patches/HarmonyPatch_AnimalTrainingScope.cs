using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace Despicable.HeroKarma.Patches.HeroKarma;

[HarmonyPatch(typeof(Toils_Interpersonal), nameof(Toils_Interpersonal.TryTrain))]
internal static class HarmonyPatch_AnimalTrainingScope
{
    private static void Postfix(TargetIndex traineeInd, ref Toil __result)
    {
        if (__result == null || __result.initAction == null)
            return;

        Toil toil = __result;
        toil.initAction = delegate
        {
            try
            {
                Pawn actor = toil.actor;
                Pawn pawn = actor?.jobs?.curJob?.GetTarget(traineeInd).Thing as Pawn;
                if (actor == null || pawn == null)
                    return;

                if (pawn.Spawned && pawn.Awake() && actor.interactions.TryInteractWith(pawn, InteractionDefOf.TrainAttempt))
                {
                    float statValue = actor.GetStatValue(StatDefOf.TrainAnimalChance);
                    statValue *= GenMath.LerpDouble(0f, 1f, 1.5f, 0.5f, pawn.GetStatValue(StatDefOf.Wildness));
                    if (actor.relations.DirectRelationExists(PawnRelationDefOf.Bond, pawn))
                    {
                        statValue *= 5f;
                    }

                    if (HKSettingsUtil.EnableLocalRep && HKHookUtilSafe.ActorIsHero(actor) && global::Despicable.PawnQuery.IsAnimal(pawn))
                    {
                        string trainerPawnId = actor.GetUniqueLoadID();
                        string animalPawnId = pawn.GetUniqueLoadID();
                        if (!trainerPawnId.NullOrEmpty() && !animalPawnId.NullOrEmpty()
                            && LocalReputationUtility.TryGetDirectPawnInfluenceIndex(trainerPawnId, animalPawnId, out float directInfluenceIndex))
                        {
                            float multiplier = HKBalanceTuning.GetAnimalTrainingChanceMultiplier(directInfluenceIndex);
                            if (Math.Abs(multiplier - 1f) >= 0.001f)
                                statValue *= multiplier;
                        }
                    }

                    statValue = Mathf.Clamp01(statValue);
                    TrainableDef trainableDef = pawn.training.NextTrainableToTrain();
                    if (trainableDef == null)
                    {
                        Log.ErrorOnce("Attempted to train untrainable animal", 7842936);
                    }
                    else
                    {
                        string text;
                        if (Rand.Value < statValue)
                        {
                            pawn.training.Train(trainableDef, actor);
                            if (pawn.caller != null)
                            {
                                pawn.caller.DoCall();
                            }
                            text = "D2HK_UI_AnimalTrainSuccess".Translate(trainableDef.LabelCap, statValue.ToStringPercent());
                            RelationsUtility.TryDevelopBondRelation(actor, pawn, 0.007f);
                            TaleRecorder.RecordTale(TaleDefOf.TrainedAnimal, actor, pawn, trainableDef);
                        }
                        else
                        {
                            text = "D2HK_UI_AnimalTrainFail".Translate(trainableDef.LabelCap, statValue.ToStringPercent());
                        }

                        int currentSteps = TryGetTrainingSteps(pawn.training, trainableDef);
                        text = text + "\n" + currentSteps + " / " + trainableDef.steps;
                        MoteMaker.ThrowText((actor.DrawPos + pawn.DrawPos) / 2f, actor.Map, text, 5f);
                    }
                }
            }
            catch (Exception ex)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce("HarmonyPatch_AnimalTrainingScope:1", "Animal training patch suppressed an exception.", ex);
            }
        };

        __result = toil;
    }

    private static readonly System.Reflection.MethodInfo _getStepsMethod = AccessTools.Method(typeof(Pawn_TrainingTracker), "GetSteps", new[] { typeof(TrainableDef) });

    private static int TryGetTrainingSteps(Pawn_TrainingTracker tracker, TrainableDef trainableDef)
    {
        try
        {
            if (tracker == null || trainableDef == null || _getStepsMethod == null)
                return 0;

            object value = _getStepsMethod.Invoke(tracker, new object[] { trainableDef });
            return value is int steps ? steps : 0;
        }
        catch
        {
            return 0;
        }
    }

}
