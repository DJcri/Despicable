using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace Despicable;
public partial class JobDriver_LovinBase : JobDriver
{
    public readonly TargetIndex iTarget = TargetIndex.A;
    public readonly TargetIndex iBed = TargetIndex.B;
    public readonly TargetIndex iCell = TargetIndex.C;

    public Pawn partnerPawn = null;
    public Building_Bed partnerBed = null;
    public int durationTicks = LovinUtil.DefaultDurationTicks;
    protected readonly List<Pawn> participants = new();

    public Pawn Partner
    {
        get
        {
            if (partnerPawn != null)
            {
                partnerBed = partnerPawn.CurrentBed();
                return partnerPawn;
            }

            if (Target is Pawn)
            {
                LocalTargetInfo localTargetInfo = job.GetTarget(TargetIndex.A).Pawn;
                partnerBed = localTargetInfo.Pawn.CurrentBed();
                return job.GetTarget(TargetIndex.A).Pawn;
            }

            if (Target is Corpse corpse)
                return corpse.InnerPawn;

            return null;
        }
    }

    public Thing Target
    {
        get
        {
            if (job == null)
                return null;

            if (job.GetTarget(TargetIndex.A).Pawn != null)
                return job.GetTarget(TargetIndex.A).Pawn;

            return job.GetTarget(TargetIndex.A).Thing;
        }
    }

    public Building_Bed Bed
    {
        get
        {
            if (partnerBed != null)
                return partnerBed;

            if (job.GetTarget(TargetIndex.B).Thing is Building_Bed bed)
                return bed;

            return null;
        }
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return true;
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        return null;
    }
}
