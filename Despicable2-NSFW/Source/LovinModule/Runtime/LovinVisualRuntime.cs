using RimWorld;
using Verse;
using Verse.AI;

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
            || jobDef == LovinModule_JobDefOf.Job_GetBedLovin
            || jobDef == LovinModule_JobDefOf.Job_SelfLovin;
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
        return CanShowLovinNudity(pawn)
            && VisualActivityTracker.IsLovinVisualActive(pawn);
    }

    internal static bool SetLovinVisualActive(Pawn pawn, bool active, bool refreshVisuals = true)
    {
        if (!HasLovinPartsComp(pawn))
            return false;

        bool shouldBeActive = active && CanShowLovinNudity(pawn);
        bool wasActive = VisualActivityTracker.IsLovinVisualActive(pawn);
        if (wasActive == shouldBeActive)
            return false;

        VisualActivityTracker.SetLovinVisualActive(pawn, shouldBeActive);
        if (refreshVisuals)
            RefreshPawnVisuals(pawn);

        return true;
    }

    internal static bool SyncPawn(Pawn pawn, bool force = false, bool refreshVisuals = true)
    {
        if (!HasLovinPartsComp(pawn))
            return false;

        bool wasActive = VisualActivityTracker.IsLovinVisualActive(pawn);
        bool shouldBeActive = (wasActive || ShouldLovinVisualBeActiveFromSavedState(pawn)) && CanShowLovinNudity(pawn);
        if (!force && wasActive == shouldBeActive)
            return false;

        VisualActivityTracker.SetLovinVisualActive(pawn, shouldBeActive);
        if (refreshVisuals)
            RefreshPawnVisuals(pawn);

        return force || wasActive != shouldBeActive;
    }

    internal static bool ClearPawn(Pawn pawn, bool refreshVisuals = true)
    {
        return SetLovinVisualActive(pawn, false, refreshVisuals);
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

                ReapplySavedLovinPlaybackIfNeeded(pawn, refreshVisuals: false);
                SyncPawn(pawn, force: true, refreshVisuals: false);
                RefreshPawnVisuals(pawn);
            }
            catch (System.Exception ex)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce("LovinVisualRuntime.RefreshAllLovinParts", "Lovin visual refresh skipped one pawn after a non-fatal exception.", ex);
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

                ReapplySavedLovinPlaybackIfNeeded(pawn, refreshVisuals: false);
                SyncPawn(pawn, force: true, refreshVisuals: false);
                RefreshPawnVisuals(pawn);
            }
            catch (System.Exception ex)
            {
                Despicable.Core.DebugLogger.WarnExceptionOnce("LovinVisualRuntime.RefreshMapLovinParts", "Lovin visual refresh skipped one pawn after a non-fatal exception.", ex);
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

    private static bool ShouldLovinVisualBeActiveFromSavedState(Pawn pawn)
    {
        if (!CanShowLovinNudity(pawn))
            return false;

        JobDriver currentDriver = pawn?.jobs?.curDriver;
        if (currentDriver is JobDriver_LovinBase lovinDriver && lovinDriver.IsLovinPlaybackActive)
            return true;

        if (currentDriver is JobDriver_SelfLovin selfLovinDriver && selfLovinDriver.IsSelfLovinPlaybackActive)
            return true;

        return false;
    }

    private static void ReapplySavedLovinPlaybackIfNeeded(Pawn pawn, bool refreshVisuals)
    {
        JobDriver currentDriver = pawn?.jobs?.curDriver;
        if (currentDriver is JobDriver_LovinBase lovinDriver && lovinDriver.IsLovinPlaybackActive)
        {
            lovinDriver.ReapplyLovinRuntimeState(refreshVisuals);
            return;
        }

        if (currentDriver is JobDriver_SelfLovin selfLovinDriver && selfLovinDriver.IsSelfLovinPlaybackActive)
            selfLovinDriver.ReapplySelfLovinRuntimeState(refreshVisuals);
    }

    private static bool HasLovinPartsComp(Pawn pawn)
    {
        return pawn?.TryGetComp<CompLovinParts>() != null;
    }
}
