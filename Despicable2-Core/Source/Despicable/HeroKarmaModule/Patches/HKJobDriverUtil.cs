using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;
using Verse.AI;

namespace Despicable.HeroKarma.Patches.HeroKarma;
internal static class HKJobDriverUtil
{
    private static readonly Dictionary<Type, FieldInfo> PawnFieldByType = new();
    private static readonly Dictionary<Type, FieldInfo> JobFieldByType = new();

    public static bool TryGetActorAndJob(object jobDriver, out Pawn actor, out Job job)
    {
        actor = null;
        job = null;
        if (jobDriver == null)
            return false;

        if (jobDriver is JobDriver typedDriver)
        {
            actor = typedDriver.pawn;
            job = typedDriver.job;
            return actor != null && job != null;
        }

        Type type = jobDriver.GetType();
        FieldInfo pawnField = GetOrResolveField(PawnFieldByType, type, "pawn");
        FieldInfo jobField = GetOrResolveField(JobFieldByType, type, "job");

        actor = pawnField?.GetValue(jobDriver) as Pawn;
        job = jobField?.GetValue(jobDriver) as Job;
        return actor != null && job != null;
    }

    public static Pawn TryGetPawnTarget(Job job, params TargetIndex[] indexes)
    {
        if (job == null || indexes == null)
            return null;

        for (int i = 0; i < indexes.Length; i++)
        {
            Pawn pawn = job.GetTarget(indexes[i]).Thing as Pawn;
            if (pawn != null)
                return pawn;
        }

        return null;
    }

    public static Thing TryGetNonPawnThingTarget(Job job, params TargetIndex[] indexes)
    {
        if (job == null || indexes == null)
            return null;

        for (int i = 0; i < indexes.Length; i++)
        {
            Thing thing = job.GetTarget(indexes[i]).Thing;
            if (thing != null && thing is not Pawn)
                return thing;
        }

        return null;
    }

    private static FieldInfo GetOrResolveField(Dictionary<Type, FieldInfo> cache, Type type, string fieldName)
    {
        if (cache.TryGetValue(type, out FieldInfo cached))
            return cached;

        FieldInfo resolved = AccessTools.Field(type, fieldName);
        cache[type] = resolved;
        return resolved;
    }
}
