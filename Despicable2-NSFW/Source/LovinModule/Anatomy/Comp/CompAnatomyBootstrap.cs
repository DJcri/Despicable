using Despicable.AnimGroupStudio.Preview;
using RimWorld;
using Verse;

namespace Despicable;
public class CompAnatomyBootstrap : ThingComp, IDetachedPreviewPawnInitializer
{
    private const int HiddenTrackerVersion = 1;

    private bool anatomySeeded;
    private int seedRetryCount;
    private bool sawGenderWorksChangeSignal;
    private bool syncInProgress;

    private bool hiddenStateResolved;
    private bool hiddenHasPenis;
    private bool hiddenHasVagina;
    private int hiddenTrackerVersion;

    internal bool HasResolvedAnatomy => hiddenStateResolved;

    internal bool TryGetResolvedAnatomy(out bool hasPenis, out bool hasVagina)
    {
        hasPenis = false;
        hasVagina = false;

        if (!hiddenStateResolved)
            return false;

        hasPenis = hiddenHasPenis;
        hasVagina = hiddenHasVagina;
        return true;
    }

    internal void SetResolvedAnatomy(bool hasPenis, bool hasVagina)
    {
        hiddenHasPenis = hasPenis;
        hiddenHasVagina = hasVagina;
        hiddenStateResolved = true;
        hiddenTrackerVersion = HiddenTrackerVersion;
    }

    internal void ClearResolvedAnatomy()
    {
        hiddenHasPenis = false;
        hiddenHasVagina = false;
        hiddenStateResolved = false;
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref anatomySeeded, "d2_anatomySeeded", false);
        Scribe_Values.Look(ref seedRetryCount, "d2_anatomySeedRetryCount", 0);
        Scribe_Values.Look(ref sawGenderWorksChangeSignal, "d2_sawGenderWorksChangeSignal", false);
        Scribe_Values.Look(ref hiddenStateResolved, "d2_hiddenAnatomyResolved", false);
        Scribe_Values.Look(ref hiddenHasPenis, "d2_hiddenHasPenis", false);
        Scribe_Values.Look(ref hiddenHasVagina, "d2_hiddenHasVagina", false);
        Scribe_Values.Look(ref hiddenTrackerVersion, "d2_hiddenAnatomyVersion", 0);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (hiddenTrackerVersion < HiddenTrackerVersion)
                hiddenStateResolved = false;

            anatomySeeded = false;
            TrySeedNow(forceResync: true);
        }
    }

    public override void PostPostMake()
    {
        base.PostPostMake();
        TrySeedNow();
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        TrySeedNow(forceResync: respawningAfterLoad && !hiddenStateResolved);
    }

    public void InitializeForDetachedPreview()
    {
        Pawn pawn = parent as Pawn;
        if (pawn == null)
            return;

        if (!AnatomyBootstrapper.ForcePreviewSeedFromCurrentGender(pawn))
            return;

        anatomySeeded = true;
        MarkGraphicsDirty(pawn);
    }

    public override void CompTickRare()
    {
        base.CompTickRare();
        TrySeedNow();
    }

    internal void NotifyPotentialAnatomyChange()
    {
        if (syncInProgress)
            return;

        anatomySeeded = false;
        seedRetryCount = 0;
        sawGenderWorksChangeSignal = true;
        ClearResolvedAnatomy();
        TrySeedNow(forceResync: true);
    }

    private void TrySeedNow(bool forceResync = false)
    {
        if (anatomySeeded && !forceResync && hiddenStateResolved)
            return;

        Pawn pawn = parent as Pawn;
        if (pawn == null)
            return;

        syncInProgress = true;
        AnatomyBootstrapResult result;
        try
        {
            result = AnatomyBootstrapper.TryResolveAndApply(pawn, forceResync: forceResync);
        }
        finally
        {
            syncInProgress = false;
        }

        switch (result)
        {
            case AnatomyBootstrapResult.Resolved:
            case AnatomyBootstrapResult.Skipped:
                anatomySeeded = true;
                seedRetryCount = 0;
                sawGenderWorksChangeSignal = false;
                MarkGraphicsDirty(pawn);
                return;
            case AnatomyBootstrapResult.Pending:
                anatomySeeded = false;
                return;
        }
    }

    private static void MarkGraphicsDirty(Pawn pawn)
    {
        if (pawn == null)
            return;

        LongEventHandler.ExecuteWhenFinished(delegate
        {
            try
            {
                if (pawn.DestroyedOrNull())
                    return;

                pawn.Drawer?.renderer?.renderTree?.SetDirty();
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(pawn);
            }
            catch
            {
            }
        });
    }
}
