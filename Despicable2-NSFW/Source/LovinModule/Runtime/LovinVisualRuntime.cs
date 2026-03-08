using RimWorld;
using Verse;

namespace Despicable;
/// <summary>
/// Shared runtime owner for lovin-visual state. Clothing suppression lives here so the render comp
/// can stay render-only and become responsive without waiting for a per-pawn tick.
/// </summary>
internal static class LovinVisualRuntime
{
    internal static bool IsLovinJob(JobDef jobDef)
    {
        return jobDef == JobDefOf.Lovin
            || jobDef == LovinModule_JobDefOf.Job_GiveLovin
            || jobDef == LovinModule_JobDefOf.Job_GetLovin
            || jobDef == LovinModule_JobDefOf.Job_GetBedLovin;
    }

    internal static bool CanShowLovinNudity(Pawn pawn)
    {
        Settings settings = CommonUtil.GetSettings();
        return (settings?.nudityEnabled ?? true)
            && pawn != null
            && pawn.RaceProps?.Humanlike == true
            && pawn.ageTracker?.Adult == true;
    }

    internal static bool IsLovinVisualActiveForRender(Pawn pawn)
    {
        if (!CanShowLovinNudity(pawn))
            return false;

        if (VisualActivityTracker.IsLovinVisualActive(pawn))
            return true;

        JobDef currentJobDef = pawn?.jobs?.curJob?.def ?? pawn?.CurJobDef;
        return IsLovinJob(currentJobDef);
    }

    internal static bool SyncPawn(Pawn pawn, bool force = false, bool refreshVisuals = true)
    {
        if (!HasLovinPartsComp(pawn))
            return false;

        JobDef currentJobDef = pawn?.jobs?.curJob?.def ?? pawn?.CurJobDef;
        bool shouldBeActive = CanShowLovinNudity(pawn) && IsLovinJob(currentJobDef);
        bool wasActive = VisualActivityTracker.IsLovinVisualActive(pawn);
        if (!force && wasActive == shouldBeActive)
            return false;

        VisualActivityTracker.SetLovinVisualActive(pawn, shouldBeActive);
        if (refreshVisuals)
            RefreshPawnVisuals(pawn);

        return force || wasActive != shouldBeActive;
    }

    internal static bool ClearPawn(Pawn pawn, bool refreshVisuals = true)
    {
        if (!HasLovinPartsComp(pawn) || !VisualActivityTracker.IsLovinVisualActive(pawn))
            return false;

        VisualActivityTracker.SetLovinVisualActive(pawn, false);
        if (refreshVisuals)
            RefreshPawnVisuals(pawn);

        return true;
    }

    internal static void NotifyPotentialRenderStateChanged(Pawn pawn)
    {
        if (!HasLovinPartsComp(pawn))
            return;

        RefreshPawnVisuals(pawn);
    }

    internal static void RehydrateAllAfterRuntimeReset()
    {
        foreach (Pawn pawn in PawnsFinder.AllMapsAndWorld_Alive)
        {
            try
            {
                if (!HasLovinPartsComp(pawn))
                    continue;

                SyncPawn(pawn, force: true, refreshVisuals: false);
                RefreshPawnVisuals(pawn);
            }
            catch
            {
            }
        }
    }

    internal static void RefreshAllForSettingsChange()
    {
        foreach (Pawn pawn in PawnsFinder.AllMapsAndWorld_Alive)
        {
            try
            {
                if (!HasLovinPartsComp(pawn))
                    continue;

                SyncPawn(pawn, force: true, refreshVisuals: false);
                RefreshPawnVisuals(pawn);
            }
            catch
            {
            }
        }
    }

    internal static void RefreshPawnVisuals(Pawn pawn, bool markPortraitDirty = true)
    {
        if (pawn == null)
            return;

        pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        if (markPortraitDirty)
            PortraitsCache.SetDirty(pawn);
    }

    private static bool HasLovinPartsComp(Pawn pawn)
    {
        return pawn?.TryGetComp<CompLovinParts>() != null;
    }
}
