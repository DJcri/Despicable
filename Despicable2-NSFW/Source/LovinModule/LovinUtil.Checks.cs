using RimWorld;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Despicable.NSFW.Integrations;

namespace Despicable;
public static partial class LovinUtil
{
    internal struct LovinHealthCheckSnapshot
    {
        public bool IsAdult;
        public int AgeChronologicalYears;
        public int AgeBiologicalYears;
        public bool CanBeAwake;
        public float BleedRateTotal;
        public float PainTotal;
        public bool IsDead;
        public bool IsStarving;
    }

    internal enum ManualLovinBoundaryCause
    {
        None = 0,
        Ideology = 1,
        Orientation = 2,
        Relation = 3
    }

    private static LovinHealthCheckSnapshot CreateHealthCheckSnapshot(Pawn pawn)
    {
        return new LovinHealthCheckSnapshot
        {
            IsAdult = pawn?.ageTracker?.Adult == true,
            AgeChronologicalYears = pawn?.ageTracker?.AgeChronologicalYears ?? 0,
            AgeBiologicalYears = pawn?.ageTracker?.AgeBiologicalYears ?? 0,
            CanBeAwake = pawn?.health?.capacities?.CanBeAwake == true,
            BleedRateTotal = pawn?.health?.hediffSet?.BleedRateTotal ?? 0f,
            PainTotal = pawn?.health?.hediffSet?.PainTotal ?? 0f,
            IsDead = pawn?.Dead == true,
            IsStarving = pawn?.needs?.food?.Starving == true
        };
    }

    internal static string GetHealthCheckFailureReason(LovinHealthCheckSnapshot snapshot)
    {
        if (!snapshot.IsAdult ||
            snapshot.AgeChronologicalYears < 18 ||
            snapshot.AgeBiologicalYears < 18)
        {
            return "D2N_LovinReason_IsUnderage".Translate();
        }

        if (!snapshot.CanBeAwake)
        {
            return "D2N_LovinReason_CantStayAwake".Translate();
        }

        if (snapshot.BleedRateTotal > 0.0f)
        {
            return "D2N_LovinReason_IsBleeding".Translate();
        }

        if (snapshot.PainTotal > LovinMaxPainThreshold)
        {
            return "D2N_LovinReason_IsInPain".Translate();
        }

        if (snapshot.IsDead)
        {
            return "D2N_LovinReason_IsDead".Translate();
        }

        if (snapshot.IsStarving)
        {
            return "D2N_LovinReason_IsStarving".Translate();
        }

        return null;
    }

    private static string GetCouldUseSomeLovinFailureReason(Pawn pawn, bool orderedLovin)
    {
        if (pawn == null)
        {
            return "D2N_LovinReason_IsMissing".Translate();
        }

        if (pawn.RaceProps?.Humanlike != true)
        {
            return "D2N_LovinReason_IsNotHumanlike".Translate();
        }

        if (pawn.Drafted)
        {
            return "D2N_LovinReason_IsDrafted".Translate();
        }

        if (pawn.mindState?.duty?.def == DutyDefOf.TravelOrLeave)
        {
            return "D2N_LovinReason_IsLeaving".Translate();
        }

        if (pawn.InAggroMentalState)
        {
            return "D2N_LovinReason_IsAggressive".Translate();
        }

        if (!orderedLovin && !global::Despicable.DespicableRuntimeConfig.DebugLoggingEnabled)
        {
            string text = pawn.CurJob?.def?.defName?.ToLower() ?? string.Empty;
            if (text.Contains("ritual") || text.Contains("ceremony") || text.Contains("speech") || text.Contains("wedding"))
            {
                return "D2N_LovinReason_IsBusy".Translate();
            }

            if (pawn.GetLord()?.LordJob is LordJob_Ritual)
            {
                return "D2N_LovinReason_IsBusy".Translate();
            }

            if (pawn.CurJob?.workGiverDef?.workType == WorkTypeDefOf.Doctor)
            {
                return "D2N_LovinReason_IsBusy".Translate();
            }

            if (pawn.CurJob?.workGiverDef?.workType == WorkTypeDefOf.Firefighter)
            {
                return "D2N_LovinReason_IsBusy".Translate();
            }

            Job curJob = pawn.CurJob;
            if (curJob != null && curJob.playerForced)
            {
                return "D2N_LovinReason_IsBusy".Translate();
            }

            if (pawn.needs?.mood?.thoughts?.memories?.GetFirstMemoryOfDef(ThoughtDefOf.GotSomeLovin) != null)
            {
                return "D2N_LovinReason_HadLovinRecently".Translate();
            }

            if (!(pawn.needs?.joy?.CurLevelPercentage < 0.6f))
            {
                return "D2N_LovinReason_DoesntWantLovin".Translate();
            }
        }

        return null;
    }

    private static string GetHealthCheckFailureReason(Pawn pawn)
    {
        if (pawn == null)
        {
            return "D2N_LovinReason_IsMissing".Translate();
        }

        return GetHealthCheckFailureReason(CreateHealthCheckSnapshot(pawn));
    }

    public static bool PassesLovinCheck(Pawn pawn, Pawn target, bool ordered = false)
    {
        if (IntegrationGuards.ShouldDeferLovinToIntimacy())
            return false;

        if (pawn == null || target == null)
            return false;

        if (target == pawn)
            return false;

        // Keep the basic safety gates symmetric.
        if (!CouldUseSomeLovin(pawn, ordered) || !CouldUseSomeLovin(target, ordered))
            return false;

        if (!PassesHealthCheck(pawn) || !PassesHealthCheck(target))
            return false;

        // Preference gates: configurable.
        Settings s = CommonUtil.GetSettings();
        bool mutual = s.lovinMutualConsent;
        bool respectIdeology = s.lovinRespectIdeology;

        if (!PassesOrientationCheck(pawn, target))
            return false;
        if (mutual && !PassesOrientationCheck(target, pawn))
            return false;

        if (!PassesRelationsCheck(pawn, target))
            return false;
        if (mutual && !PassesRelationsCheck(target, pawn))
            return false;

        if (respectIdeology)
        {
            if (!PassesIdeologyCheck(pawn, target))
                return false;
            if (mutual && !PassesIdeologyCheck(target, pawn))
                return false;
        }

        return true;
    }

    public static bool PassesIdeologyCheck(Pawn pawn, Pawn target)
    {
        if (!ModLister.IdeologyInstalled)
        {
            return true;
        }

        if (pawn == null)
        {
            return false;
        }

        if (global::Despicable.DespicableRuntimeConfig.DebugLoggingEnabled)
        {
            return true;
        }

        bool spouseOnly = pawn.Ideo?.GetPrecept(DefDatabase<PreceptDef>.GetNamed("Lovin_SpouseOnly_Strict")) != null;
        bool freeLovin = pawn.Ideo?.GetPrecept(DefDatabase<PreceptDef>.GetNamed("Lovin_FreeApproved")) != null;
        bool lovinHorrible = pawn.Ideo?.GetPrecept(DefDatabase<PreceptDef>.GetNamed("Lovin_Horrible")) != null;

        if (lovinHorrible ||
            (spouseOnly && pawn.relations?.DirectRelationExists(PawnRelationDefOf.Spouse, target) != true))
        {
            return false;
        }

        if (freeLovin)
        {
            return true;
        }

        return true;
    }

    public static bool PassesRelationsCheck(Pawn pawn, Pawn target)
    {
        if (pawn == null)
        {
            return false;
        }

        if (global::Despicable.DespicableRuntimeConfig.DebugLoggingEnabled)
        {
            return true;
        }

        if (pawn.relations?.DirectRelationExists(PawnRelationDefOf.Lover, target) == true ||
            pawn.relations?.DirectRelationExists(PawnRelationDefOf.Spouse, target) == true)
        {
            return true;
        }

        if (!(pawn.relations?.OpinionOf(target) >= LovinMinOpinion))
        {
            return false;
        }

        if (!(pawn.relations?.CompatibilityWith(target) >= LovinMinCompatibility))
        {
            return false;
        }

        return true;
    }

    public static bool PassesOrientationCheck(Pawn pawn, Pawn target)
    {
        if (pawn == null)
        {
            return false;
        }

        if (global::Despicable.DespicableRuntimeConfig.DebugLoggingEnabled)
        {
            return true;
        }

        bool gay = pawn.story?.traits?.HasTrait(TraitDefOf.Gay) == true;
        bool bisexual = pawn.story?.traits?.HasTrait(TraitDefOf.Bisexual) == true;

        if (gay && !PawnStateUtil.ComparePawnGenderToByte(pawn, (byte)target.gender))
        {
            return false;
        }

        if (!bisexual && !gay && PawnStateUtil.ComparePawnGenderToByte(pawn, (byte)target.gender))
        {
            return false;
        }

        return true;
    }

    public static bool PassesHealthCheck(Pawn pawn)
    {
        return pawn != null && GetHealthCheckFailureReason(pawn) == null;
    }

    private static string GetRelationsFailureReason(Pawn pawn, Pawn target, bool isOtherPawn)
    {
        // Match PassesRelationsCheck ordering so the reason is stable.
        if (pawn?.relations?.OpinionOf(target) < LovinMinOpinion)
        {
            return isOtherPawn
                ? "D2N_LovinReason_TheirOpinionTooLow".Translate()
                : "D2N_LovinReason_YourOpinionTooLow".Translate();
        }

        if (pawn?.relations?.CompatibilityWith(target) < LovinMinCompatibility)
        {
            return isOtherPawn
                ? "D2N_LovinReason_TheirCompatibilityTooLow".Translate()
                : "D2N_LovinReason_YourCompatibilityTooLow".Translate();
        }

        return isOtherPawn
            ? "D2N_LovinReason_TheirOpinionTooLow".Translate()
            : "D2N_LovinReason_YourOpinionTooLow".Translate();
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

    private static string GetPairHardFailureReason(Pawn orderedPawn, Pawn otherPawn)
    {
        if (orderedPawn == null || otherPawn == null)
            return "D2N_LovinReason_PairMissing".Translate();

        if (orderedPawn == otherPawn)
            return "D2N_LovinReason_SamePawn".Translate();

        string frag = GetHealthCheckFailureReason(CreateHealthCheckSnapshot(orderedPawn));
        if (frag == "D2N_LovinReason_IsUnderage".Translate() ||
            frag == "D2N_LovinReason_IsDead".Translate())
        {
            return BuildRoleReason(isOtherPawn: false, frag);
        }

        frag = GetHealthCheckFailureReason(CreateHealthCheckSnapshot(otherPawn));
        if (frag == "D2N_LovinReason_IsUnderage".Translate() ||
            frag == "D2N_LovinReason_IsDead".Translate())
        {
            return BuildRoleReason(isOtherPawn: true, frag);
        }

        if (orderedPawn.RaceProps?.Humanlike != true)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsNotHumanlike".Translate());

        if (otherPawn.RaceProps?.Humanlike != true)
            return BuildRoleReason(isOtherPawn: true, "D2N_LovinReason_IsNotHumanlike".Translate());

        return null;
    }

    private static string GetOrderedPawnManualFailureReason(Pawn orderedPawn)
    {
        if (orderedPawn == null)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsMissing".Translate());

        if (orderedPawn.InAggroMentalState)
            return BuildRoleReason(isOtherPawn: false, "D2N_LovinReason_IsAggressive".Translate());

        string frag = GetHealthCheckFailureReason(orderedPawn);
        if (frag != null)
            return BuildRoleReason(isOtherPawn: false, frag);

        return null;
    }

    private static string GetOtherPawnAvailabilityFailureReason(Pawn otherPawn)
    {
        string frag = GetCouldUseSomeLovinFailureReason(otherPawn, orderedLovin: false);
        if (frag != null)
            return BuildRoleReason(isOtherPawn: true, frag);

        frag = GetHealthCheckFailureReason(otherPawn);
        if (frag != null)
            return BuildRoleReason(isOtherPawn: true, frag);

        return null;
    }

    private static string GetOtherPawnManualAvailabilityFailureReason(Pawn otherPawn)
    {
        string frag = GetCouldUseSomeLovinFailureReason(otherPawn, orderedLovin: true);
        if (frag != null)
            return BuildRoleReason(isOtherPawn: true, frag);

        frag = GetHealthCheckFailureReason(otherPawn);
        if (frag != null)
            return BuildRoleReason(isOtherPawn: true, frag);

        return null;
    }

    private static string GetConsentPreferenceFailureReason(Pawn orderedPawn, Pawn otherPawn)
    {
        Settings s = CommonUtil.GetSettings();
        bool mutual = s.lovinMutualConsent;
        bool respectIdeology = s.lovinRespectIdeology;

        if (!PassesOrientationCheck(otherPawn, orderedPawn))
            return "D2N_LovinReason_OrientationMismatch".Translate();

        if (mutual && !PassesRelationsCheck(otherPawn, orderedPawn))
            return GetRelationsFailureReason(otherPawn, orderedPawn, isOtherPawn: true);

        if (respectIdeology && !PassesIdeologyCheck(otherPawn, orderedPawn))
            return "D2N_LovinReason_IdeologyForbids".Translate();

        return null;
    }

    internal static ManualLovinBoundaryCause GetOrderedPawnManualBoundaryCause(Pawn orderedPawn, Pawn otherPawn)
    {
        if (orderedPawn == null || otherPawn == null)
            return ManualLovinBoundaryCause.None;

        if (!PassesIdeologyCheck(orderedPawn, otherPawn))
            return ManualLovinBoundaryCause.Ideology;

        if (!PassesOrientationCheck(orderedPawn, otherPawn))
            return ManualLovinBoundaryCause.Orientation;

        if (!PassesRelationsCheck(orderedPawn, otherPawn))
            return ManualLovinBoundaryCause.Relation;

        return ManualLovinBoundaryCause.None;
    }

    public static bool PassesManualLovinCheck(Pawn orderedPawn, Pawn otherPawn, out string reason)
    {
        return !TryGetManualLovinDisabledReason(orderedPawn, otherPawn, out reason);
    }

    /// <summary>
    /// Returns true if the general manual lovin option should be disabled, along with a short player-facing reason.
    /// Reason is intended to be embedded into the option label (no tooltip).
    /// orderedPawn is the pawn the player directly commanded.
    /// otherPawn is the counterpart who must still be available to participate.
    /// </summary>
    public static bool TryGetManualLovinDisabledReason(Pawn orderedPawn, Pawn otherPawn, out string reason)
    {
        reason = GetPairHardFailureReason(orderedPawn, otherPawn);
        if (!reason.NullOrEmpty())
            return true;

        reason = GetOrderedPawnManualFailureReason(orderedPawn);
        if (!reason.NullOrEmpty())
            return true;

        reason = GetOtherPawnManualAvailabilityFailureReason(otherPawn);
        if (!reason.NullOrEmpty())
            return true;

        reason = GetConsentPreferenceFailureReason(orderedPawn, otherPawn);
        if (!reason.NullOrEmpty())
            return true;

        reason = null;
        return false;
    }
public static bool InSameBed(Pawn pawn, Pawn partner)
    {
        if (pawn == null)
        {
            return false;
        }

        return pawn.InBed() && partner.InBed() && pawn.CurrentBed() == partner.CurrentBed();
    }

    public static bool AloneInBed(Pawn pawn)
    {
        if (pawn == null)
        {
            return false;
        }

        return pawn.CurrentBed().CurOccupants.Count() == 1;
    }

    public static bool IsLovin(Pawn pawn)
    {
        if (pawn == null)
        {
            return false;
        }

        return pawn.CurJobDef == JobDefOf.Lovin ||
               pawn.CurJobDef == LovinModule_JobDefOf.Job_GiveLovin ||
               pawn.CurJobDef == LovinModule_JobDefOf.Job_GetLovin ||
               pawn.CurJobDef == LovinModule_JobDefOf.Job_GetBedLovin;
    }
}
