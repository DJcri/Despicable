using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using HarmonyLib;
using Despicable;
using Verse;
using Verse.AI;

namespace Despicable.Core.Compatibility.PerspectiveShiftCompat;
/// <summary>
/// Perspective Shift already injects the avatar pawn into right-click FloatMenu context when the
/// player is controlling a pawn directly. That means Despicable's social submenu is already
/// available there through the normal FloatMenuMakerMap patch.
///
/// This compat simply removes Perspective Shift's overlapping top-level Chat / Insult entries in
/// that same avatar context so the player sees one social entrypoint instead of duplicates.
/// </summary>
internal static class HarmonyPatch_PerspectiveShift_AvatarInteractionProviders
{
    private static readonly string[] ProviderTypeNames =
    {
        "PerspectiveShift.FloatMenuOptionProvider_AvatarChat",
        "PerspectiveShift.FloatMenuOptionProvider_AvatarInsult"
    };

    private static readonly HashSet<string> PatchedMethods = new(StringComparer.Ordinal);
    private static readonly HarmonyMethod Prefix = new(typeof(HarmonyPatch_PerspectiveShift_AvatarInteractionProviders), nameof(Prefix_GetSingleOptionFor));

    public static bool CanApply()
    {
        for (int i = 0; i < ProviderTypeNames.Length; i++)
        {
            if (ResolveTargetMethod(ProviderTypeNames[i]) != null)
                return true;
        }

        return false;
    }

    public static void Apply(Harmony harmony)
    {
        if (harmony == null)
            return;

        for (int i = 0; i < ProviderTypeNames.Length; i++)
        {
            MethodInfo method = ResolveTargetMethod(ProviderTypeNames[i]);
            if (method == null)
                continue;

            string key = method.DeclaringType?.FullName + "." + method.Name;
            if (!PatchedMethods.Add(key))
                continue;

            harmony.Patch(method, prefix: Prefix);
        }
    }

    private static MethodInfo ResolveTargetMethod(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return null;

        Type providerType = AccessTools.TypeByName(typeName);
        if (providerType == null)
            return null;

        return AccessTools.Method(providerType, "GetSingleOptionFor", new[] { typeof(Pawn), typeof(FloatMenuContext) });
    }

    private static bool Prefix_GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context, ref FloatMenuOption __result)
    {
        if (!ShouldSuppress(clickedPawn, context))
            return true;

        __result = null;
        return false;
    }

    private static bool ShouldSuppress(Pawn clickedPawn, FloatMenuContext context)
    {
        if (clickedPawn == null || context == null)
            return false;

        Pawn actingPawn = context.FirstSelectedPawn;
        if (actingPawn == null || actingPawn == clickedPawn)
            return false;

        if (!PawnQuery.IsVisibleHumanlikeSpawned(actingPawn))
            return false;

        if (!PawnQuery.IsVisibleHumanlikeSpawned(clickedPawn))
            return false;

        if (PawnPairQuery.AreHostile(actingPawn, clickedPawn))
            return false;

        if (!actingPawn.CanReach(clickedPawn, PathEndMode.ClosestTouch, Danger.Deadly))
            return false;

        return true;
    }
}
