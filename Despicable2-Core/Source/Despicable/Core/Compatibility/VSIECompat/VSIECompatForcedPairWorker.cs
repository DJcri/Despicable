using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Despicable.Core.Compatibility.VSIECompat;
internal static partial class VSIECompatUtility
{
    private static readonly Type GatheringWorkerDoublePawnType =
        AccessTools.TypeByName("VanillaSocialInteractionsExpanded.GatheringWorker_DoublePawn");

    internal static IDisposable PushForcedPairScope(Pawn organizer, Pawn companion, GatheringDef gatheringDef)
    {
        return new ForcedPairScope(organizer, companion, gatheringDef);
    }

    internal static bool TryHandleForcedPairCanExecute(object workerInstance, Map map, Pawn organizerArg, out bool result)
    {
        result = false;
        if (!TryGetForcedPairForWorker(workerInstance, out Pawn organizer, out Pawn companion, out GatheringDef gatheringDef))
            return false;

        result = CanExecuteForcedPair(workerInstance, map, organizer, companion, gatheringDef, out _);
        return true;
    }

    internal static bool TryHandleForcedPairTryExecute(object workerInstance, Map map, Pawn organizerArg, out bool result)
    {
        result = false;
        if (!TryGetForcedPairForWorker(workerInstance, out Pawn organizer, out Pawn companion, out GatheringDef gatheringDef))
            return false;

        result = TryExecuteForcedPair(workerInstance, map, organizer, companion, gatheringDef);
        return true;
    }

    private static bool TryGetForcedPairForWorker(object workerInstance, out Pawn organizer, out Pawn companion, out GatheringDef gatheringDef)
    {
        organizer = null;
        companion = null;
        gatheringDef = null;

        ForcedPairScope scope = ForcedPairScope.Current;
        if (scope == null || workerInstance == null || GatheringWorkerDoublePawnType == null)
            return false;

        if (!GatheringWorkerDoublePawnType.IsInstanceOfType(workerInstance))
            return false;

        GatheringDef workerDef = GetWorkerDef(workerInstance);
        if (workerDef == null || scope.GatheringDef == null || workerDef != scope.GatheringDef)
            return false;

        organizer = scope.Organizer;
        companion = scope.Companion;
        gatheringDef = scope.GatheringDef;
        return organizer != null && companion != null;
    }

    private static bool CanExecuteForcedPair(object workerInstance, Map map, Pawn organizer, Pawn companion, GatheringDef gatheringDef, out IntVec3 spot)
    {
        spot = IntVec3.Invalid;

        if (workerInstance == null || map == null || organizer == null || companion == null || gatheringDef == null)
            return false;

        if (organizer == companion || organizer.MapHeld != map || companion.MapHeld != map)
            return false;

        if (!BasePawnValidator(organizer, gatheringDef) || !BasePawnValidator(companion, gatheringDef))
            return false;

        if (!InvokeMemberValidator(workerInstance, organizer) || !InvokeMemberValidator(workerInstance, companion))
            return false;

        if (!InvokePawnsCanGatherTogether(workerInstance, organizer, companion))
            return false;

        if (!InvokeTryFindGatherSpot(workerInstance, organizer, out spot))
            return false;

        if (!GatheringsUtility.PawnCanStartOrContinueGathering(organizer))
            return false;

        if (!InvokeConditionsMeet(workerInstance, organizer))
            return false;

        return true;
    }

    private static bool TryExecuteForcedPair(object workerInstance, Map map, Pawn organizer, Pawn companion, GatheringDef gatheringDef)
    {
        if (!CanExecuteForcedPair(workerInstance, map, organizer, companion, gatheringDef, out IntVec3 spot))
            return false;

        if (organizer.Faction == null || organizer.MapHeld == null)
            return false;

        LordJob lordJob = InvokeCreateLordJobCustom(workerInstance, spot, organizer, companion);
        if (lordJob == null)
            return false;

        List<Pawn> pawns = new() { organizer, companion };
        LordMaker.MakeNewLord(organizer.Faction, lordJob, organizer.MapHeld, pawns);

        if (!InvokeSendLetterCustom(workerInstance, spot, organizer, companion))
        {
            Find.LetterStack.ReceiveLetter(
                gatheringDef.letterTitle,
                gatheringDef.letterText.Formatted(organizer.Named("ORGANIZER"), companion.Named("COMPANION")),
                LetterDefOf.PositiveEvent,
                pawns);
        }

        return true;
    }

    private static bool BasePawnValidator(Pawn pawn, GatheringDef gatheringDef)
    {
        return pawn != null
            && pawn.Spawned
            && !pawn.Downed
            && pawn.RaceProps.Humanlike
            && !pawn.InBed()
            && !pawn.InMentalState
            && pawn.GetLord() == null
            && GatheringsUtility.ShouldPawnKeepGathering(pawn, gatheringDef)
            && !pawn.Drafted
            && (gatheringDef.requiredTitleAny == null
                || gatheringDef.requiredTitleAny.Count == 0
                || (pawn.royalty != null && pawn.royalty.AllTitlesInEffectForReading.Any(title => gatheringDef.requiredTitleAny.Contains(title.def))));
    }

    private static bool InvokeMemberValidator(object workerInstance, Pawn pawn)
    {
        try
        {
            var method = AccessTools.Method(workerInstance.GetType(), "MemberValidator", new[] { typeof(Pawn) });
            return method != null && method.Invoke(workerInstance, new object[] { pawn }) is bool result && result;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE MemberValidator invocation failed: {e}");
            return false;
        }
    }

    private static bool InvokePawnsCanGatherTogether(object workerInstance, Pawn organizer, Pawn companion)
    {
        try
        {
            var method = AccessTools.Method(workerInstance.GetType(), "PawnsCanGatherTogether", new[] { typeof(Pawn), typeof(Pawn) });
            return method != null && method.Invoke(workerInstance, new object[] { organizer, companion }) is bool result && result;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE PawnsCanGatherTogether invocation failed: {e}");
            return false;
        }
    }

    private static bool InvokeConditionsMeet(object workerInstance, Pawn organizer)
    {
        try
        {
            var method = AccessTools.Method(workerInstance.GetType(), "ConditionsMeet", new[] { typeof(Pawn) });
            return method != null && method.Invoke(workerInstance, new object[] { organizer }) is bool result && result;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE ConditionsMeet invocation failed: {e}");
            return false;
        }
    }

    private static bool InvokeTryFindGatherSpot(object workerInstance, Pawn organizer, out IntVec3 spot)
    {
        spot = IntVec3.Invalid;

        try
        {
            var method = AccessTools.Method(workerInstance.GetType(), "TryFindGatherSpot", new[] { typeof(Pawn), typeof(IntVec3).MakeByRefType() });
            if (method == null)
                return false;

            object[] args = { organizer, IntVec3.Invalid };
            if (method.Invoke(workerInstance, args) is not bool result || !result)
                return false;

            if (args[1] is IntVec3 value)
                spot = value;
            return true;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE TryFindGatherSpot invocation failed: {e}");
            return false;
        }
    }

    private static LordJob InvokeCreateLordJobCustom(object workerInstance, IntVec3 spot, Pawn organizer, Pawn companion)
    {
        try
        {
            var method = AccessTools.Method(workerInstance.GetType(), "CreateLordJobCustom", new[] { typeof(IntVec3), typeof(Pawn), typeof(Pawn) });
            return method?.Invoke(workerInstance, new object[] { spot, organizer, companion }) as LordJob;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE CreateLordJobCustom invocation failed: {e}");
            return null;
        }
    }

    private static bool InvokeSendLetterCustom(object workerInstance, IntVec3 spot, Pawn organizer, Pawn companion)
    {
        try
        {
            var method = AccessTools.Method(workerInstance.GetType(), "SendLetterCustom", new[] { typeof(IntVec3), typeof(Pawn), typeof(Pawn) });
            if (method == null)
                return false;

            method.Invoke(workerInstance, new object[] { spot, organizer, companion });
            return true;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE SendLetterCustom invocation failed: {e}");
            return false;
        }
    }

    private sealed class ForcedPairScope : IDisposable
    {
        [ThreadStatic]
        private static ForcedPairScope current;

        private readonly ForcedPairScope previous;

        internal static ForcedPairScope Current => current;
        internal Pawn Organizer { get; }
        internal Pawn Companion { get; }
        internal GatheringDef GatheringDef { get; }

        internal ForcedPairScope(Pawn organizer, Pawn companion, GatheringDef gatheringDef)
        {
            previous = current;
            Organizer = organizer;
            Companion = companion;
            GatheringDef = gatheringDef;
            current = this;
        }

        public void Dispose()
        {
            current = previous;
        }
    }
}
