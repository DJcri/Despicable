using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Despicable.Core.Compatibility.VSIECompat;
internal static partial class VSIECompatUtility
{
    private static readonly string[] directedActivityDefNames =
    {
        "VSIE_MealTogether",
        "VSIE_MovieNight",
        "VSIE_GoingForAWalk",
        "VSIE_Skygazing",
        "VSIE_ViewingArtTogether",
        "VSIE_BuildingSnowmen",
        "VSIE_GrabbingBeer"
    };

    private static readonly System.Reflection.PropertyInfo GatheringWorkerProperty =
        AccessTools.Property(typeof(GatheringDef), "Worker");

    private static readonly System.Reflection.FieldInfo GatheringWorkerField =
        AccessTools.Field(typeof(GatheringDef), "workerInt")
        ?? AccessTools.Field(typeof(GatheringDef), "worker");

    private static readonly System.Reflection.FieldInfo GatheringWorkerClassField =
        AccessTools.Field(typeof(GatheringDef), "workerClass");

    internal static bool CanOpenDirectedActivityMenu(Pawn pawn, Pawn targetPawn, out string disabledReason)
    {
        disabledReason = null;

        if (!IsLoaded())
        {
            disabledReason = "InteractionReason_VSIE_NotInstalled".Translate();
            return false;
        }

        if (!ArePawnsAvailableForDirectedSocialJob(pawn, targetPawn, out disabledReason))
            return false;

        return GetAvailableDirectedActivities(pawn, targetPawn).Count > 0
            ? true
            : SetDisabledReason("InteractionReason_VSIE_NoActivity".Translate(), out disabledReason);
    }

    internal static List<GatheringDef> GetAvailableDirectedActivities(Pawn pawn, Pawn targetPawn)
    {
        var activities = new List<GatheringDef>();
        foreach (GatheringDef gatheringDef in GetDirectedActivityDefs())
        {
            if (CanLaunchDirectedActivity(pawn, targetPawn, gatheringDef))
                activities.Add(gatheringDef);
        }

        return activities;
    }

    internal static IEnumerable<GatheringDef> GetDirectedActivityDefs()
    {
        if (!IsLoaded())
            yield break;

        for (int i = 0; i < directedActivityDefNames.Length; i++)
        {
            GatheringDef def = DefDatabase<GatheringDef>.GetNamedSilentFail(directedActivityDefNames[i]);
            if (def != null)
                yield return def;
        }
    }

    internal static bool CanLaunchDirectedActivity(Pawn pawn, Pawn targetPawn, GatheringDef gatheringDef)
    {
        if (gatheringDef == null)
            return false;

        if (!ArePawnsAvailableForDirectedSocialJob(pawn, targetPawn, out _))
            return false;

        if (!TryGetGatheringWorker(gatheringDef, out object worker))
            return false;

        using (PushForcedPairScope(pawn, targetPawn, gatheringDef))
        {
            if (TryHandleForcedPairCanExecute(worker, pawn.MapHeld, pawn, out bool forcedResult))
                return forcedResult;

            return InvokeWorkerCanExecute(worker, pawn.MapHeld, pawn);
        }
    }

    internal static bool TryExecuteDirectedActivity(Pawn pawn, Pawn targetPawn, GatheringDef gatheringDef)
    {
        if (pawn == null || targetPawn == null || gatheringDef == null)
            return false;

        if (!CanLaunchDirectedActivity(pawn, targetPawn, gatheringDef))
            return false;

        var resolution = Despicable.Core.InteractionEntry.ResolveManual(
            pawn,
            targetPawn,
            Despicable.Core.Channels.ManualSocial,
            req => req.RequestedCommand = ActivityCommandPrefix + gatheringDef.defName);

        if (!resolution.Allowed || resolution.ChosenCommand != ActivityCommandPrefix + gatheringDef.defName)
            return false;

        if (!TryGetGatheringWorker(gatheringDef, out object worker))
            return false;

        using (PushForcedPairScope(pawn, targetPawn, gatheringDef))
        {
            if (TryHandleForcedPairTryExecute(worker, pawn.MapHeld, pawn, out bool forcedResult))
                return forcedResult;

            return InvokeWorkerTryExecute(worker, pawn.MapHeld, pawn);
        }
    }

    private static bool SetDisabledReason(string value, out string disabledReason)
    {
        disabledReason = value;
        return false;
    }

    private static bool TryGetGatheringWorker(GatheringDef gatheringDef, out object worker)
    {
        worker = null;
        if (gatheringDef == null)
            return false;

        try
        {
            if (GatheringWorkerProperty != null)
            {
                worker = GatheringWorkerProperty.GetValue(gatheringDef, null);
                if (worker != null)
                    return true;
            }

            if (GatheringWorkerField != null)
            {
                worker = GatheringWorkerField.GetValue(gatheringDef);
                if (worker != null)
                    return true;
            }

            if (GatheringWorkerClassField?.GetValue(gatheringDef) is Type workerType)
            {
                worker = Activator.CreateInstance(workerType);
                if (worker != null)
                {
                    var defField = AccessTools.Field(workerType, "def");
                    defField?.SetValue(worker, gatheringDef);
                    return true;
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE worker lookup failed for {gatheringDef.defName}: {e}");
        }

        return worker != null;
    }

    private static GatheringDef GetWorkerDef(object workerInstance)
    {
        if (workerInstance == null)
            return null;

        try
        {
            var field = AccessTools.Field(workerInstance.GetType(), "def");
            if (field?.GetValue(workerInstance) is GatheringDef gatheringDef)
                return gatheringDef;

            var prop = AccessTools.Property(workerInstance.GetType(), "def");
            if (prop?.CanRead == true && prop.GetValue(workerInstance, null) is GatheringDef propDef)
                return propDef;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE worker def lookup failed: {e}");
        }

        return null;
    }

    private static bool InvokeWorkerCanExecute(object worker, Map map, Pawn organizer)
    {
        try
        {
            var method = AccessTools.Method(worker.GetType(), "CanExecute", new[] { typeof(Map), typeof(Pawn) });
            return method != null && method.Invoke(worker, new object[] { map, organizer }) is bool result && result;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE CanExecute invocation failed: {e}");
            return false;
        }
    }

    private static bool InvokeWorkerTryExecute(object worker, Map map, Pawn organizer)
    {
        try
        {
            var method = AccessTools.Method(worker.GetType(), "TryExecute", new[] { typeof(Map), typeof(Pawn) });
            return method != null && method.Invoke(worker, new object[] { map, organizer }) is bool result && result;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE TryExecute invocation failed: {e}");
            return false;
        }
    }
}
