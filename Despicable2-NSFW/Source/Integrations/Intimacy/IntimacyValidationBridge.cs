using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Despicable.NSFW.Integrations;
using Verse.AI;

namespace Despicable.NSFW.Integrations.Intimacy;
/// <summary>
/// Soft bridge that borrows Intimacy's lovin validity checks for Despicable's ordered manual lovin flow.
/// Despicable keeps its own UI/job flow; Intimacy becomes the approval oracle when installed.
/// </summary>
internal static class IntimacyValidationBridge
{
    private static readonly ReflectionCache Cache = new();

    internal static void PrimeCache()
    {
        try
        {
            EnsureCached();
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyValidationBridge.PrimeCache", "Intimacy validation cache warmup failed.", e);
        }
    }

    internal static bool PassesManualLovinCheck(Pawn orderedPawn, Pawn otherPawn, out string reason)
    {
        return !TryGetManualLovinDisabledReason(orderedPawn, otherPawn, out reason);
    }

    internal static bool TryGetManualLovinDisabledReason(Pawn orderedPawn, Pawn otherPawn, out string reason)
    {
        reason = null;

        if (!IntegrationGuards.ShouldUseIntimacyForLovinValidation())
            return false;

        if (!EnsureCached())
        {
            reason = "D2N_LovinReason_Unknown".Translate();
            return true;
        }

        reason = GetPairHardFailureReason(orderedPawn, otherPawn);
        if (!reason.NullOrEmpty())
            return true;

        // Preserve Despicable's manual ordered-pawn leniency here: Intimacy still governs approval,
        // but the ordered pawn may be drafted without the bridge vetoing the command up front.
        // Ordered manual lovin should also ignore the recent-lovin cooldown just like Despicable's
        // native manual path, for both the commanded pawn and the counterpart.
        reason = GetPawnFailureReason(orderedPawn, otherPawn, isOtherPawn: false, allowDrafted: true, allowRecentLovin: true);
        if (!reason.NullOrEmpty())
            return true;

        reason = GetPawnFailureReason(otherPawn, orderedPawn, isOtherPawn: true, allowDrafted: false, allowRecentLovin: true);
        if (!reason.NullOrEmpty())
            return true;

        reason = GetPairFailureReason(orderedPawn, otherPawn);
        if (!reason.NullOrEmpty())
            return true;

        return false;
    }

    internal static bool PassesSelfLovinCheck(Pawn pawn, out string reason)
    {
        return !TryGetSelfLovinDisabledReason(pawn, out reason);
    }

    internal static bool TryGetSelfLovinDisabledReason(Pawn pawn, out string reason)
    {
        reason = null;

        if (!IntegrationGuards.ShouldUseIntimacyForLovinValidation())
            return false;

        if (!EnsureCached())
        {
            reason = "D2N_LovinReason_Unknown".Translate();
            return true;
        }

        reason = GetSoloHardFailureReason(pawn);
        if (!reason.NullOrEmpty())
            return true;

        // Preserve Despicable's ordered-action leniency for solo manual lovin too: if the player explicitly
        // orders the action, being drafted should not make Intimacy veto it up front. Manual solo self relief
        // also intentionally ignores Intimacy's ideology / desire-to-lovin preference vetoes.
        reason = GetManualSoloFailureReason(pawn, allowDrafted: true, allowRecentLovin: true);
        if (!reason.NullOrEmpty())
            return true;

        return false;
    }

    internal static void ResetRuntimeState()
    {
        Cache.Reset();
    }

    private static bool EnsureCached()
    {
        if (Cache.HasSearched)
            return Cache.IsReady;

        Cache.HasSearched = true;

        try
        {
            Type commonChecks = AccessTools.TypeByName("LoveyDoveySexWithEuterpe.CommonChecks");
            Type sexUtilities = AccessTools.TypeByName("LoveyDoveySexWithEuterpe.SexUtilities");

            if (commonChecks != null)
            {
                Cache.IsOldEnough = CreateUnary(commonChecks, "IsOldEnough");
                Cache.HasTemporarilyPreventLovinHediff = CreateUnary(commonChecks, "HasTemporarilyPreventLovinHediff");
                Cache.PawnsTolerateEachOther = CreateBinary(commonChecks, "PawnsTolerateEachOther");
                Cache.AreMutuallyAttracted = CreateBinary(commonChecks, "AreMutuallyAttracted");
                Cache.PairingIsIncestious = CreateBinary(commonChecks, "PariringIsIncestious");
                Cache.IdeologyForbidsLovin = CreateBinary(commonChecks, "IdeologyForbidsLovin");
            }

            if (sexUtilities != null)
            {
                Cache.CanEverDoLovin = CreateUnary(sexUtilities, "CanEverDoLovin");
                Cache.CanCurrentlyDoLovin = CreateUnary(sexUtilities, "CanCurrentlyDoLovin");
                Cache.IsAlreadyDoingLovin = CreateUnary(sexUtilities, "IsAlreadyDoingLovin");
            }
        }
        catch (Exception e)
        {
            Despicable.Core.DebugLogger.WarnExceptionOnce("IntimacyValidationBridge.EnsureCached", "Intimacy validation cache setup failed; manual lovin will stay disabled instead of silently falling back.", e);
        }

        return Cache.IsReady;
    }

    private static Func<Pawn, bool> CreateUnary(Type owner, string methodName)
    {
        var method = AccessTools.Method(owner, methodName, new[] { typeof(Pawn) });
        if (method == null || method.ReturnType != typeof(bool))
            return null;

        return (Func<Pawn, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, bool>), method);
    }

    private static Func<Pawn, Pawn, bool> CreateBinary(Type owner, string methodName)
    {
        var method = AccessTools.Method(owner, methodName, new[] { typeof(Pawn), typeof(Pawn) });
        if (method == null || method.ReturnType != typeof(bool))
            return null;

        return (Func<Pawn, Pawn, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, Pawn, bool>), method);
    }

    private static string GetSoloHardFailureReason(Pawn pawn)
    {
        if (pawn == null)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsMissing".Translate());

        if (pawn.RaceProps?.Humanlike != true)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsNotHumanlike".Translate());

        if (!pawn.Spawned)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsMissing".Translate());

        if (pawn.Dead)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsDead".Translate());

        if (Cache.IsOldEnough != null && !Cache.IsOldEnough(pawn))
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsUnderage".Translate());

        return null;
    }

    private static string GetPairHardFailureReason(Pawn orderedPawn, Pawn otherPawn)
    {
        if (orderedPawn == null || otherPawn == null)
            return "D2N_LovinReason_PairMissing".Translate();

        if (orderedPawn == otherPawn)
            return "D2N_LovinReason_SamePawn".Translate();

        if (orderedPawn.RaceProps?.Humanlike != true)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsNotHumanlike".Translate());

        if (otherPawn.RaceProps?.Humanlike != true)
            return BuildRoleReason(isOtherPawn: true, "D2N_LovinReason_IsNotHumanlike".Translate());

        if (orderedPawn.Dead)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsDead".Translate());

        if (otherPawn.Dead)
            return BuildRoleReason(isOtherPawn: true, "D2N_LovinReason_IsDead".Translate());

        if (Cache.IsOldEnough != null)
        {
            if (!Cache.IsOldEnough(orderedPawn))
                return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsUnderage".Translate());

            if (!Cache.IsOldEnough(otherPawn))
                return BuildRoleReason(isOtherPawn: true, "D2N_LovinReason_IsUnderage".Translate());
        }

        return null;
    }

    private static string GetManualSoloFailureReason(Pawn pawn, bool allowDrafted, bool allowRecentLovin)
    {
        if (pawn == null)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsMissing".Translate());

        if (!pawn.Spawned)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsMissing".Translate());

        if (!allowDrafted && pawn.Drafted)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsDrafted".Translate());

        if (pawn.Downed)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsBusy".Translate());

        if (!pawn.Awake())
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_CantStayAwake".Translate());

        if (pawn.InAggroMentalState)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsAggressive".Translate());

        if (pawn.health?.hediffSet?.BleedRateTotal > 0f)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsBleeding".Translate());

        if (pawn.CurJob?.workGiverDef?.workType == WorkTypeDefOf.Doctor)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsBusy".Translate());

        if (pawn.CurJob?.workGiverDef?.workType == WorkTypeDefOf.Firefighter)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsBusy".Translate());

        if (pawn.CurJob?.playerForced == true)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsBusy".Translate());

        if (!allowRecentLovin && pawn.mindState?.canLovinTick > Find.TickManager.TicksGame)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_HadLovinRecently".Translate());

        if (Cache.HasTemporarilyPreventLovinHediff != null && Cache.HasTemporarilyPreventLovinHediff(pawn))
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsBusy".Translate());

        if (!LovinUtil.IsActiveSelfLovinJob(pawn)
            && Cache.IsAlreadyDoingLovin != null
            && Cache.IsAlreadyDoingLovin(pawn))
        {
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsBusy".Translate());
        }

        if (Cache.CanCurrentlyDoLovin != null && !Cache.CanCurrentlyDoLovin(pawn))
        {
            if (!(allowDrafted && pawn.Drafted))
                return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsBusy".Translate());
        }

        return null;
    }

    private static string GetPawnFailureReason(Pawn pawn, Pawn counterpart, bool isOtherPawn, bool allowDrafted, bool allowRecentLovin)
    {
        if (pawn == null)
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsMissing".Translate());

        if (!pawn.Spawned)
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsMissing".Translate());

        if (!allowDrafted && pawn.Drafted)
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsDrafted".Translate());

        if (pawn.Downed)
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsBusy".Translate());

        if (!pawn.Awake())
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_CantStayAwake".Translate());

        if (pawn.InAggroMentalState)
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsAggressive".Translate());

        if (pawn.health?.hediffSet?.BleedRateTotal > 0f)
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsBleeding".Translate());

        if (pawn.CurJob?.workGiverDef?.workType == WorkTypeDefOf.Doctor)
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsBusy".Translate());

        if (pawn.CurJob?.workGiverDef?.workType == WorkTypeDefOf.Firefighter)
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsBusy".Translate());

        if (pawn.CurJob?.playerForced == true)
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsBusy".Translate());

        if (!allowRecentLovin && pawn.mindState?.canLovinTick > Find.TickManager.TicksGame)
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_HadLovinRecently".Translate());

        if (Cache.HasTemporarilyPreventLovinHediff != null && Cache.HasTemporarilyPreventLovinHediff(pawn))
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsBusy".Translate());

        if (Cache.IsAlreadyDoingLovin != null && Cache.IsAlreadyDoingLovin(pawn))
            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsBusy".Translate());

        if (Cache.CanCurrentlyDoLovin != null && !Cache.CanCurrentlyDoLovin(pawn))
        {
            if (!(allowDrafted && pawn.Drafted))
                return BuildRoleReason(isOtherPawn, "D2N_LovinReason_IsBusy".Translate());
        }

        if (Cache.CanEverDoLovin != null && !Cache.CanEverDoLovin(pawn))
        {
            if (counterpart != null && Cache.IdeologyForbidsLovin != null && Cache.IdeologyForbidsLovin(pawn, counterpart))
                return "D2N_LovinReason_IdeologyForbids".Translate();

            return BuildRoleReason(isOtherPawn, "D2N_LovinReason_DoesntWantLovin".Translate());
        }

        if (isOtherPawn)
        {
            if (pawn.IsForbidden(counterpart))
                return BuildRoleReason(isOtherPawn: true, "D2N_LovinReason_IsBusy".Translate());

            if (counterpart != null && !counterpart.CanReserve(pawn))
                return BuildRoleReason(isOtherPawn: true, "D2N_LovinReason_IsBusy".Translate());
        }

        return null;
    }

    private static string GetPairFailureReason(Pawn orderedPawn, Pawn otherPawn)
    {
        if (orderedPawn == null || otherPawn == null)
            return null;

        if (Cache.AreMutuallyAttracted != null && !Cache.AreMutuallyAttracted(orderedPawn, otherPawn))
            return "D2N_LovinReason_OrientationMismatch".Translate();

        if (LovinUtil.ShouldBlockRelatedLovin() && Cache.PairingIsIncestious != null && Cache.PairingIsIncestious(orderedPawn, otherPawn))
            return "D2N_LovinReason_RelatedByBlood".Translate();

        if (Cache.IdeologyForbidsLovin != null)
        {
            if (Cache.IdeologyForbidsLovin(orderedPawn, otherPawn) || Cache.IdeologyForbidsLovin(otherPawn, orderedPawn))
                return "D2N_LovinReason_IdeologyForbids".Translate();
        }

        if (Cache.PawnsTolerateEachOther != null && !Cache.PawnsTolerateEachOther(orderedPawn, otherPawn))
        {
            int theirOpinion = otherPawn.relations?.OpinionOf(orderedPawn) ?? 0;
            if (theirOpinion < -5)
                return "D2N_LovinReason_TheirOpinionTooLow".Translate();

            int yourOpinion = orderedPawn.relations?.OpinionOf(otherPawn) ?? 0;
            if (yourOpinion < -5)
                return "D2N_LovinReason_YourOpinionTooLow".Translate();

            return "D2N_LovinReason_TheirOpinionTooLow".Translate();
        }

        return null;
    }

    private static string PrefixFor(bool isOtherPawn)
    {
        return isOtherPawn
            ? "D2N_LovinReason_TargetPrefix".Translate()
            : "D2N_LovinReason_YouPrefix".Translate();
    }

    private static string BuildRoleReason(bool isOtherPawn, string fragment)
    {
        if (fragment.NullOrEmpty())
            return null;

        return $"{PrefixFor(isOtherPawn)} {fragment}";
    }

    private sealed class ReflectionCache
    {
        internal bool HasSearched;
        internal Func<Pawn, bool> IsOldEnough;
        internal Func<Pawn, bool> CanEverDoLovin;
        internal Func<Pawn, bool> CanCurrentlyDoLovin;
        internal Func<Pawn, bool> IsAlreadyDoingLovin;
        internal Func<Pawn, bool> HasTemporarilyPreventLovinHediff;
        internal Func<Pawn, Pawn, bool> PawnsTolerateEachOther;
        internal Func<Pawn, Pawn, bool> AreMutuallyAttracted;
        internal Func<Pawn, Pawn, bool> PairingIsIncestious;
        internal Func<Pawn, Pawn, bool> IdeologyForbidsLovin;

        internal bool IsReady
        {
            get
            {
                return IsOldEnough != null
                    && CanEverDoLovin != null
                    && CanCurrentlyDoLovin != null
                    && IsAlreadyDoingLovin != null
                    && HasTemporarilyPreventLovinHediff != null
                    && PawnsTolerateEachOther != null
                    && AreMutuallyAttracted != null
                    && PairingIsIncestious != null
                    && IdeologyForbidsLovin != null;
            }
        }

        internal void Reset()
        {
            HasSearched = false;
            IsOldEnough = null;
            CanEverDoLovin = null;
            CanCurrentlyDoLovin = null;
            IsAlreadyDoingLovin = null;
            HasTemporarilyPreventLovinHediff = null;
            PawnsTolerateEachOther = null;
            AreMutuallyAttracted = null;
            PairingIsIncestious = null;
            IdeologyForbidsLovin = null;
        }
    }
}
