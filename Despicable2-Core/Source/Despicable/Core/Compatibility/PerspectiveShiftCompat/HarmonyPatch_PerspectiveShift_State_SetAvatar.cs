using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace Despicable.Core.Compatibility.PerspectiveShiftCompat;
internal static class HarmonyPatch_PerspectiveShift_State_SetAvatar
{
    private static readonly HashSet<string> PatchedMethods = new(StringComparer.Ordinal);
    private static readonly HarmonyMethod Postfix = new(typeof(HarmonyPatch_PerspectiveShift_State_SetAvatar), nameof(Postfix_SetAvatar));

    public static bool CanApply()
    {
        return ResolveTargetMethod() != null;
    }

    public static void Apply(Harmony harmony)
    {
        if (harmony == null)
            return;

        MethodInfo method = ResolveTargetMethod();
        if (method == null)
            return;

        string key = method.DeclaringType?.FullName + "." + method.Name;
        if (!PatchedMethods.Add(key))
            return;

        harmony.Patch(method, postfix: Postfix);
    }

    private static MethodInfo ResolveTargetMethod()
    {
        Type stateType = AccessTools.TypeByName("PerspectiveShift.State");
        if (stateType == null)
            return null;

        return AccessTools.Method(stateType, "SetAvatar", new[] { typeof(Pawn), typeof(bool) });
    }

    private static void Postfix_SetAvatar(Pawn pawn)
    {
        PerspectiveShiftCompatUtility.TryAssignHeroFromAvatar(pawn);
    }
}
