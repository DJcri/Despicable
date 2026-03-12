using Verse;

namespace Despicable;
public class CompAnatomyBootstrap : ThingComp
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
