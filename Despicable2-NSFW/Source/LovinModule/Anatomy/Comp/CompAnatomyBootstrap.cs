using Despicable.AnimGroupStudio.Preview;

using RimWorld;

using Verse;

namespace Despicable;
public class CompAnatomyBootstrap : ThingComp, IDetachedPreviewPawnInitializer
{
    private bool anatomySeeded;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref anatomySeeded, "d2_anatomySeeded", false);
    }

    public override void PostPostMake()
    {
        base.PostPostMake();
        TrySeedNow();
    }

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        TrySeedNow();
    }


    public void InitializeForDetachedPreview()
    {
        Pawn pawn = parent as Pawn;
        if (pawn == null)
            return;

        if (!AnatomyBootstrapper.ForcePreviewSeedFromCurrentGender(pawn))
            return;

        anatomySeeded = true;

        try
        {
            pawn.Drawer?.renderer?.renderTree?.SetDirty();
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            pawn.Drawer?.renderer?.EnsureGraphicsInitialized();
            PortraitsCache.SetDirty(pawn);
        }
        catch
        {
            // Best-effort preview path. Normal gameplay state remains owned by the usual lifecycle.
        }
    }
    private void TrySeedNow()
    {
        if (anatomySeeded)
            return;

        Pawn pawn = parent as Pawn;
        if (pawn == null)
            return;

        if (AnatomyBootstrapper.TrySeed(pawn))
            anatomySeeded = true;
    }
}
