using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Despicable.Core.Compatibility.VSIECompat;
/// <summary>
/// Lightweight reflection-based bridge for Vanilla Social Interactions Expanded.
/// Keeps Core free of a hard dependency while exposing narrow, runtime-validated helpers
/// for player-directed VSIE actions and pair activities.
/// </summary>
internal static partial class VSIECompatUtility
{
    internal const string PackageId = "VanillaExpanded.VanillaSocialInteractionsExpanded";
    internal const string VentCommandId = "VSIE_VentToFriend";
    internal const string ActivityCommandPrefix = "VSIE_ACTIVITY:";

    private static readonly Type SettingsType = AccessTools.TypeByName("VanillaSocialInteractionsExpanded.VanillaSocialInteractionsExpandedSettings");
    private static readonly Type TeachingTopicType = AccessTools.TypeByName("VanillaSocialInteractionsExpanded.TeachingTopic");
    private static readonly Type SocialInteractionsManagerType = AccessTools.TypeByName("VanillaSocialInteractionsExpanded.SocialInteractionsManager");
    private static readonly Type UtilsType = AccessTools.TypeByName("VanillaSocialInteractionsExpanded.VSIE_Utils");

    private static readonly System.Reflection.PropertyInfo SocialInteractionsManagerProperty =
        AccessTools.Property(UtilsType, "SocialInteractionsManager");

    private static readonly System.Reflection.FieldInfo TeachersWithPupilsField =
        AccessTools.Field(SocialInteractionsManagerType, "teachersWithPupils");

    private static readonly System.Reflection.MethodInfo GetFriendsForMethod =
        AccessTools.Method(UtilsType, "GetFriendsFor");

    internal static bool IsLoaded() => ModsConfig.IsActive(PackageId);
    internal static bool IsVentingEnabled() => GetSettingsBool("EnableVenting", fallback: true);
    internal static bool IsTeachingEnabled() => GetSettingsBool("EnableTeaching", fallback: true);
    internal static JobDef VentToFriendJobDef => DefDatabase<JobDef>.GetNamedSilentFail("VSIE_VentToFriend");
    internal static InteractionDef TeachingInteractionDef => DefDatabase<InteractionDef>.GetNamedSilentFail("VSIE_Teaching");

    internal static bool CanAddManualOptionsFor(Pawn pawn, Pawn targetPawn)
    {
        if (!IsLoaded())
            return false;

        if (pawn == null || targetPawn == null || pawn == targetPawn)
            return false;
        return !PawnPairQuery.AreHostile(pawn, targetPawn);
    }

    internal static bool CanOrderVent(Pawn pawn, Pawn targetPawn, out string disabledReason)
    {
        disabledReason = null;

        if (!IsLoaded())
        {
            disabledReason = "InteractionReason_VSIE_NotInstalled".Translate();
            return false;
        }

        if (!IsVentingEnabled())
        {
            disabledReason = "InteractionReason_VSIE_FeatureDisabled".Translate();
            return false;
        }

        if (VentToFriendJobDef == null)
        {
            disabledReason = "InteractionReason_VSIE_NotAvailable".Translate();
            return false;
        }

        if (!ArePawnsAvailableForDirectedSocialJob(pawn, targetPawn, out disabledReason))
            return false;

        if (!pawn.CanReach(targetPawn, PathEndMode.InteractionCell, Danger.Deadly))
        {
            disabledReason = "InteractionReason_VSIE_TooFarAway".Translate();
            return false;
        }

        if (!IsVentFriendAccordingToVsie(pawn, targetPawn))
        {
            disabledReason = "InteractionReason_VSIE_NotFriends".Translate();
            return false;
        }

        return true;
    }

    internal static bool CanOrderTeaching(Pawn teacher, Pawn pupil, out string disabledReason)
    {
        disabledReason = null;

        if (!IsLoaded())
        {
            disabledReason = "InteractionReason_VSIE_NotInstalled".Translate();
            return false;
        }

        if (!IsTeachingEnabled())
        {
            disabledReason = "InteractionReason_VSIE_FeatureDisabled".Translate();
            return false;
        }

        if (TeachingInteractionDef?.Worker == null)
        {
            disabledReason = "InteractionReason_VSIE_NotAvailable".Translate();
            return false;
        }

        if (!ArePawnsAvailableForImmediateSocialInteraction(teacher, pupil, out disabledReason))
            return false;

        if (!IsTeachingAvailableAccordingToVsie(teacher, pupil))
        {
            disabledReason = "InteractionReason_VSIE_NotAvailable".Translate();
            return false;
        }

        if (GetTeachingSkillChoices(teacher, pupil).Count == 0)
        {
            disabledReason = "InteractionReason_VSIE_NoLesson".Translate();
            return false;
        }

        return true;
    }

    internal static List<SkillDef> GetTeachingSkillChoices(Pawn teacher, Pawn pupil)
    {
        var skillDefs = new List<SkillDef>();

        if (teacher?.skills == null || pupil?.skills == null)
            return skillDefs;

        var pupilSkills = pupil.skills.skills?
            .Where(skill => skill != null && !skill.TotallyDisabled)
            .ToDictionary(skill => skill.def, skill => skill);

        if (pupilSkills == null || pupilSkills.Count == 0)
            return skillDefs;

        skillDefs = teacher.skills.skills?
            .Where(skill => skill != null
                && !skill.TotallyDisabled
                && pupilSkills.TryGetValue(skill.def, out SkillRecord pupilSkill)
                && skill.Level > pupilSkill.Level + 3)
            .Select(skill => skill.def)
            .Distinct()
            .OrderBy(skill => skill.label)
            .ToList()
            ?? new List<SkillDef>();

        return skillDefs;
    }

    internal static bool TryPrimeTeachingTopic(Pawn teacher, Pawn pupil, SkillDef skillDef)
    {
        if (teacher == null || pupil == null || skillDef == null)
            return false;

        if (TeachingTopicType == null || TeachersWithPupilsField == null || SocialInteractionsManagerProperty == null)
            return false;

        try
        {
            object manager = SocialInteractionsManagerProperty.GetValue(null, null);
            if (manager == null)
                return false;

            if (TeachersWithPupilsField.GetValue(manager) is not IDictionary dict)
            {
                object newDictionary = Activator.CreateInstance(TeachersWithPupilsField.FieldType);
                TeachersWithPupilsField.SetValue(manager, newDictionary);
                dict = newDictionary as IDictionary;
            }

            if (dict == null)
                return false;

            dict.Remove(teacher);
            object topic = Activator.CreateInstance(TeachingTopicType, pupil, skillDef);
            dict[teacher] = topic;
            return true;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE teaching priming failed: {e}");
            return false;
        }
    }

    private static bool ArePawnsAvailableForDirectedSocialJob(Pawn pawn, Pawn targetPawn, out string disabledReason)
    {
        disabledReason = null;

        if (pawn == null || targetPawn == null)
        {
            disabledReason = "InteractionReason_VSIE_NotAvailable".Translate();
            return false;
        }

        if (!pawn.Spawned || !targetPawn.Spawned || pawn.MapHeld != targetPawn.MapHeld)
        {
            disabledReason = "InteractionReason_VSIE_NotAvailable".Translate();
            return false;
        }

        if (pawn.Drafted || targetPawn.Drafted || pawn.InMentalState || targetPawn.InMentalState || !pawn.Awake() || !targetPawn.Awake())
        {
            disabledReason = "InteractionReason_VSIE_NotAvailable".Translate();
            return false;
        }

        return true;
    }

    private static bool ArePawnsAvailableForImmediateSocialInteraction(Pawn pawn, Pawn targetPawn, out string disabledReason)
    {
        disabledReason = null;

        if (!ArePawnsAvailableForDirectedSocialJob(pawn, targetPawn, out disabledReason))
            return false;

        if (pawn.interactions == null || !pawn.interactions.CanInteractNowWith(targetPawn))
        {
            disabledReason = IsTooFarAwayForImmediateInteraction(pawn, targetPawn)
                ? "InteractionReason_VSIE_TooFarAway".Translate()
                : "InteractionReason_VSIE_NotAvailable".Translate();
            return false;
        }

        return true;
    }

    private static bool IsVentFriendAccordingToVsie(Pawn pawn, Pawn targetPawn)
    {
        if (pawn == null || targetPawn == null)
            return false;

        foreach (Pawn friend in GetVentFriendsFor(pawn))
        {
            if (friend == targetPawn)
                return true;
        }

        return false;
    }

    private static IEnumerable<Pawn> GetVentFriendsFor(Pawn pawn)
    {
        if (pawn == null || GetFriendsForMethod == null)
            return Enumerable.Empty<Pawn>();

        try
        {
            if (GetFriendsForMethod.Invoke(null, new object[] { pawn }) is IEnumerable result)
                return result.Cast<object>().OfType<Pawn>().Where(friend => friend != null).ToList();
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE GetFriendsFor invocation failed: {e}");
        }

        return Enumerable.Empty<Pawn>();
    }

    private static bool IsTeachingAvailableAccordingToVsie(Pawn teacher, Pawn pupil)
    {
        InteractionDef teachingDef = TeachingInteractionDef;
        if (teachingDef?.Worker == null || teacher == null || pupil == null)
            return false;

        try
        {
            return teachingDef.Worker.RandomSelectionWeight(teacher, pupil) > 0f;
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE teaching RandomSelectionWeight invocation failed: {e}");
            return false;
        }
        finally
        {
            ClearTeachingTopic(teacher);
        }
    }

    private static void ClearTeachingTopic(Pawn teacher)
    {
        if (teacher == null || TeachersWithPupilsField == null || SocialInteractionsManagerProperty == null)
            return;

        try
        {
            object manager = SocialInteractionsManagerProperty.GetValue(null, null);
            if (manager == null)
                return;

            if (TeachersWithPupilsField.GetValue(manager) is IDictionary dict)
                dict.Remove(teacher);
        }
        catch (Exception e)
        {
            Log.Warning($"[Despicable2.Core] VSIE teaching cache cleanup failed: {e}");
        }
    }

    private static bool IsTooFarAwayForImmediateInteraction(Pawn pawn, Pawn targetPawn)
    {
        if (pawn == null || targetPawn == null)
            return false;
        if (!pawn.Spawned || !targetPawn.Spawned || pawn.MapHeld != targetPawn.MapHeld)
            return false;

        if (pawn.PositionHeld == targetPawn.PositionHeld)
            return false;
        return !pawn.PositionHeld.AdjacentTo8WayOrInside(targetPawn.PositionHeld);
    }

    private static bool GetSettingsBool(string fieldName, bool fallback)
    {
        if (!IsLoaded())
            return false;

        if (SettingsType == null)
            return fallback;

        var field = AccessTools.Field(SettingsType, fieldName);
        if (field != null && field.FieldType == typeof(bool))
            return (bool)field.GetValue(null);

        var prop = AccessTools.Property(SettingsType, fieldName);
        if (prop != null && prop.PropertyType == typeof(bool) && prop.CanRead)
            return (bool)prop.GetValue(null, null);

        return fallback;
    }
}
